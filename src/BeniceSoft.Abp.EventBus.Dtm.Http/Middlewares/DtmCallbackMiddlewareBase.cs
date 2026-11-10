using System.Reflection;
using System.Security.Claims;
using System.Text;
using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public abstract class DtmCallbackMiddlewareBase : DtmMiddlewareBase
{
    protected readonly RequestDelegate Next;

    protected DtmCallbackMiddlewareBase(RequestDelegate next)
    {
        Next = next;
    }

    protected abstract string GetCallbackPathPrefix(DtmTransactionCallbackOptions options);

    protected abstract IDictionary<string, DtmTransactionCallbackRegistration> GetRegistrations(DtmTransactionCallbackOptions options);

    protected abstract string? ResolveMethodName(string op);

    protected async Task HandleAsync(
        HttpContext context,
        DtmTransactionCallbackOptions callbackOptions,
        IEnumerable<IDtmBranchBarrierManager> dtmBranchBarrierManagers,
        IActionApiTokenChecker actionApiTokenChecker,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger logger)
    {
        var prefix = NormalizePrefix(GetCallbackPathPrefix(callbackOptions));
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await Next(context);
            return;
        }

        if (!await EnsureTokenAsync(context, actionApiTokenChecker))
        {
            await WriteResponseAsync(context, 401, "Invalid ActionApiToken");
            return;
        }

        var gid = GetHeaderValue(context, "gid") ?? GetHeaderValue(context, "Gid");
        string? operation = null;

        try
        {
            if (!TryParseCallbackPath(path, prefix, out var handlerName, out var op))
            {
                throw new AbpException($"DTM Callback 路径不合法: {path}");
            }

            operation = op!.ToLowerInvariant();

            var registrations = GetRegistrations(callbackOptions);
            if (!registrations.TryGetValue(handlerName!, out var registration))
            {
                throw new AbpException($"DTM Callback 未注册处理器: {handlerName}");
            }

            var methodName = ResolveMethodName(op!);
            if (methodName is null)
            {
                throw new AbpException($"DTM Callback 不支持的操作: {op}");
            }

            var handler = context.RequestServices.GetService(registration.HandlerType)
                         ?? throw new AbpException($"DTM Callback 处理器未注册到DI: {registration.HandlerType.FullName}");

            var body = await ReadRequestBodyAsync(context, logger);
            object? request = JsonUtils.Deserialize(body ?? string.Empty, registration.RequestType);
            request ??= Activator.CreateInstance(registration.RequestType)
                       ?? throw new AbpException($"DTM Callback 无法创建请求对象: {registration.RequestType.FullName}");

            using var principalChange = TryChangeCurrentPrincipal(context, logger);
            using var tenantChange = TryChangeCurrentTenant(context, logger);
            using var uow = unitOfWorkManager.Begin(isTransactional: true, requiresNew: true);

            var shouldSkipBusinessHandler = await TryInsertBranchBarrierAsync(
                context,
                op!,
                dtmBranchBarrierManagers,
                logger);

            if (!shouldSkipBusinessHandler)
            {
                await InvokeHandlerMethodAsync(handler, methodName, request, context.RequestAborted);
            }

            await uow.CompleteAsync(context.RequestAborted);

            await WriteResponseAsync(context, 200, "SUCCESS");
        }
        catch (Exception ex) when (
            (operation is "try" or "action") &&
            (ex is BusinessException || ex is TargetInvocationException { InnerException: BusinessException }))
        {
            logger.LogWarning(ex, "DTM Callback: 业务拒绝，TraceId={TraceId}, Gid={Gid}, Op={Op}",
                context.TraceIdentifier, gid, operation);
            await WriteResponseAsync(context, 409, "FAILURE");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DTM Callback: 处理异常，TraceId={TraceId}, Gid={Gid}", context.TraceIdentifier, gid);
            await WriteResponseAsync(context, 500, $"FAILED: {ex.Message}");
        }
    }

    protected virtual async Task<bool> EnsureTokenAsync(HttpContext context, IActionApiTokenChecker actionApiTokenChecker)
    {
        var token = GetHeaderValue(context, DtmRequestHeaderNames.ActionApiToken);
        return !string.IsNullOrEmpty(token) && await actionApiTokenChecker.IsCorrectAsync(token);
    }

    protected virtual IDisposable? TryChangeCurrentPrincipal(HttpContext context, ILogger logger)
    {
        var userClaimsText = GetHeaderValue(context, DtmRequestHeaderNames.UserClaims);
        if (string.IsNullOrWhiteSpace(userClaimsText))
        {
            return null;
        }

        try
        {
            var claimsBytes = StringUtils.Hex36Bytes(userClaimsText);
            var claimItems = JsonUtils.DeserializeBytes<List<ClaimTransferItem>>(claimsBytes) ?? [];

            var claims = claimItems
                .Select(x => new Claim(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
                .ToList();

            var tenantId = GetHeaderValue(context, DtmRequestHeaderNames.TenantId);
            if (!string.IsNullOrWhiteSpace(tenantId) && claims.All(x => x.Type != AbpClaimTypes.TenantId))
            {
                claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "DtmCallback"));
            context.User = principal;

            var principalAccessor = context.RequestServices.GetService<ICurrentPrincipalAccessor>();
            return principalAccessor?.Change(principal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DTM Callback: 还原用户 Claims 失败，TraceId={TraceId}", context.TraceIdentifier);
            return null;
        }
    }

    protected virtual IDisposable? TryChangeCurrentTenant(HttpContext context, ILogger logger)
    {
        var tenantId = GetHeaderValue(context, DtmRequestHeaderNames.TenantId);
        var tenantGuid = tenantId.ToGuid();
        if (tenantGuid == Guid.Empty)
        {
            return null;
        }

        try
        {
            var currentTenant = context.RequestServices.GetRequiredService<ICurrentTenant>();
            return currentTenant.Change(tenantGuid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DTM Callback: 切换租户失败，TraceId={TraceId}, TenantId={TenantId}", context.TraceIdentifier, tenantId);
            return null;
        }
    }

    protected virtual string NormalizePrefix(string prefix)
    {
        var normalized = (prefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AbpException("DTM Callback 前缀不能为空。");
        }

        normalized = normalized.Trim('/');
        return $"/{normalized}";
    }

    protected virtual bool TryParseCallbackPath(string path, string prefix, out string? handlerName, out string? op)
    {
        handlerName = null;
        op = null;

        var suffix = path[prefix.Length..].Trim('/');
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        var segments = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        handlerName = segments[0];
        op = segments[1];
        return true;
    }

    protected virtual async Task<bool> TryInsertBranchBarrierAsync(
        HttpContext context,
        string op,
        IEnumerable<IDtmBranchBarrierManager> dtmBranchBarrierManagers,
        ILogger logger)
    {
        var barrierInfo = BuildBranchBarrierInfo(context, op)
                          ?? throw new AbpException("DTM Callback 缺少屏障必要参数(gid/trans_type/branch_id/op)。");

        var dbContextTypeName = GetHeaderValue(context, DtmRequestHeaderNames.DbContextType);
        var hashedConnectionString = GetHeaderValue(context, DtmRequestHeaderNames.HashedConnectionString);

        var managers = dtmBranchBarrierManagers.ToList();
        if (managers.Count == 0)
        {
            throw new AbpException("DTM Callback 未配置任何分支屏障管理器，拒绝继续执行业务处理。");
        }


        foreach (var barrierManager in managers)
        {
            var result = await barrierManager.TryInsertBarrierAsync(
                barrierInfo,
                dbContextTypeName,
                hashedConnectionString,
                context.RequestAborted);

            if (result == DtmBranchBarrierInsertResult.NotHandled)
            {
                continue;
            }

            if (result == DtmBranchBarrierInsertResult.Duplicated)
            {
                return true;
            }

            return false;
        }

        logger.LogError("DTM Callback: 屏障管理器无法处理该请求，TraceId={TraceId}, Gid={Gid}, TransType={TransType}, BranchId={BranchId}, Op={Op}",
            context.TraceIdentifier, barrierInfo.Gid, barrierInfo.TransType, barrierInfo.BranchId, barrierInfo.Op);

        throw new AbpException("DTM Callback 屏障未生效（全部返回 NotHandled），拒绝继续执行业务处理。");
    }

    protected virtual DtmBranchBarrierInfo? BuildBranchBarrierInfo(HttpContext context, string fallbackOp)
    {
        var gid = GetHeaderValue(context, "gid");
        var transType = GetHeaderValue(context, "trans_type");
        if (string.IsNullOrWhiteSpace(transType))
        {
            transType = GetHeaderValue(context, "TransType");
        }

        var branchId = GetHeaderValue(context, "branch_id");
        if (string.IsNullOrWhiteSpace(branchId))
        {
            branchId = GetHeaderValue(context, "BranchId");
        }

        var op = GetHeaderValue(context, "op");
        if (!string.IsNullOrWhiteSpace(op) && !string.Equals(op, fallbackOp, StringComparison.OrdinalIgnoreCase))
        {
            throw new AbpException("DTM Callback 的操作参数与回调路径不一致");
        }

        op = fallbackOp.ToLowerInvariant();
        var barrierId = GetHeaderValue(context, "barrier_id");
        if (string.IsNullOrWhiteSpace(barrierId))
        {
            barrierId = GetHeaderValue(context, "BarrierId");
        }

        if (string.IsNullOrWhiteSpace(barrierId))
        {
            barrierId = "01";
        }

        if (string.IsNullOrWhiteSpace(gid) ||
            string.IsNullOrWhiteSpace(transType) ||
            string.IsNullOrWhiteSpace(branchId) ||
            string.IsNullOrWhiteSpace(op))
        {
            return null;
        }

        return new DtmBranchBarrierInfo
        {
            Gid = gid,
            TransType = transType.ToLowerInvariant(),
            BranchId = branchId,
            Op = op,
            BarrierId = barrierId,
            Reason = op
        };
    }

    protected virtual async Task InvokeHandlerMethodAsync(object handler, string methodName, object request, CancellationToken cancellationToken)
    {
        var method = handler.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                     ?? throw new MissingMethodException($"Method {methodName} not found on {handler.GetType().FullName}.");

        var result = method.Invoke(handler, [request, cancellationToken]);
        if (result is Task task)
        {
            await task;
            return;
        }

        throw new InvalidOperationException($"Method {methodName} on {handler.GetType().FullName} must return Task.");
    }

    protected virtual async Task<string?> ReadRequestBodyAsync(HttpContext context, ILogger logger)
    {
        context.Request.EnableBuffering();

        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return await reader.ReadToEndAsync(context.RequestAborted);
        }
        finally
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }
    }

    protected sealed record ClaimTransferItem(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}

