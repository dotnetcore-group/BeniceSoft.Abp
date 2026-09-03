using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.OperationLogging.Abstractions;
using BeniceSoft.Core.Reflector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.OperationLogging;

public class OperationLogInterceptor : IAbpInterceptor, ITransientDependency
{
    private readonly IOperationLogEventDispatcher _eventDispatcher;
    private readonly BeniceSoftOperationLogOptions _options;
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly ILogger<OperationLogInterceptor> _logger;

    public OperationLogInterceptor(
        IOperationLogEventDispatcher eventDispatcher,
        IOptions<BeniceSoftOperationLogOptions> options,
        IBeniceSoftCurrentUser currentUser,
        ILogger<OperationLogInterceptor> logger)
    {
        _eventDispatcher = eventDispatcher;
        _currentUser = currentUser;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        var targetMethod = invocation.Method;
        var operationLogAttribute = targetMethod.GetReflector().GetCustomAttribute<OperationLogAttribute>();
        if (operationLogAttribute is null)
        {
            await invocation.ProceedAsync();
            return;
        }

        var context = new OperationLogContext();
        var parameters = targetMethod.GetParameters();
        if (parameters.LastOrDefault()?.ParameterType == typeof(OperationLogContext))
        {
            invocation.Arguments[^1] = context;
        }

        Exception? bizException = null;
        try
        {
            await invocation.ProceedAsync();
        }
        catch (Exception ex)
        {
            bizException = ex;
        }
        finally
        {
            // 无论业务方法是否执行成功，都记录操作日志
            var log = new OperationLogInfo
            {
                ServiceName = _options.ServiceName,
                OperationType = operationLogAttribute.OperationType,
                BizModule = operationLogAttribute.BizModule,
                BizId = context.BizId ?? operationLogAttribute.BizId,
                BizCode = context.BizCode ?? string.Empty,
                OperatorId = _currentUser.Id,
                OperatorName = _currentUser.Name ?? string.Empty,
                OperationTime = DateTimeOffset.UtcNow,
                Remark = context.Remark ?? string.Empty,
                ExtraData = context.ExtraData
            };

            try
            {
                await _eventDispatcher.DispatchAsync(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "操作日志分发失败: BizModule={BizModule}, OperationType={OperationType}",
                    log.BizModule, log.OperationType);
            }
        }

        // 业务异常继续向上抛出
        if (bizException is not null)
        {
            throw bizException;
        }
    }
}