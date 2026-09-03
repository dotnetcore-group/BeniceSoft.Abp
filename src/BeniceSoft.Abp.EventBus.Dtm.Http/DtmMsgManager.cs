using Dtmcli;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmMsgManager : IDtmMsgManager, ITransientDependency
{
    protected ICurrentTenant CurrentTenant { get; }

    protected IDtmGidProvider GidProvider { get; }

    protected IDtmTransFactory DtmTransFactory { get; }

    protected IServiceProvider ServiceProvider { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected IConnectionStringHasher ConnectionStringHasher { get; }

    protected IConnectionStringResolver ConnectionStringResolver { get; }

    protected DtmHttpOptions DtmHttpOptions { get; }

    protected ILogger<DtmMsgManager> Logger { get; }

    public DtmMsgManager(ICurrentTenant currentTenant,
        IDtmGidProvider gidProvider,
        IDtmTransFactory dtmTransFactory,
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager,
        IConnectionStringHasher connectionStringHasher,
        IConnectionStringResolver connectionStringResolver,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        ILogger<DtmMsgManager> logger)
    {
        CurrentTenant = currentTenant;
        GidProvider = gidProvider;
        DtmTransFactory = dtmTransFactory;
        ServiceProvider = serviceProvider;
        UnitOfWorkManager = unitOfWorkManager;
        ConnectionStringHasher = connectionStringHasher;
        ConnectionStringResolver = connectionStringResolver;
        DtmHttpOptions = dtmHttpOptions.Value;
        Logger = logger;
    }

    public async Task AddEventAsync(DtmOutboxEventBag eventBag,
        object dbContext,
        [NotNull] string connectionString,
        [CanBeNull] object? transObj,
        OutgoingEventInfo eventInfo)
    {
        var dbContextType = dbContext.GetType();
        var hashedConnectionString = await ConnectionStringHasher.HashAsync(connectionString);

        var model = GetOrCreateDtmMsgInfoModel(eventBag, transObj, dbContextType, hashedConnectionString);

        model.EventInfos.Add(eventInfo);
    }

    public async Task PrepareAndInsertBarriersAsync(DtmOutboxEventBag eventBag, CancellationToken cancellationToken = default)
    {
        await AddEventsPublishingActionAsync(eventBag);
        await PrepareTransMessagesAsync(eventBag, cancellationToken);

        await InsertTransMessagesBarriersAsync(eventBag, cancellationToken);
    }

    public async Task SubmitAsync(DtmOutboxEventBag eventBag, CancellationToken cancellationToken = default)
    {
        if (eventBag.DefaultMessage is not null)
        {
            var message = eventBag.DefaultMessage.DtmMessage as Msg;
            await message!.Submit(cancellationToken);
        }

        foreach (var model in eventBag.TransMessages.Values)
        {
            var msg = model.DtmMessage as Msg;
            await msg!.Submit(cancellationToken);
        }
    }

    protected virtual IDtmMsgInfoModel GetOrCreateDtmMsgInfoModel(
        DtmOutboxEventBag eventBag,
        [CanBeNull] object? transObj,
        Type dbContextType,
        string hashedConnectionString)
    {
        if (transObj is null)
        {
            return eventBag.DefaultMessage ??= CreateDtmMessageInfoModel(dbContextType, hashedConnectionString);
        }

        return eventBag.TransMessages.GetOrAdd(transObj,
            _ => CreateDtmMessageInfoModel(dbContextType, hashedConnectionString));
    }

    protected virtual IDtmMsgInfoModel CreateDtmMessageInfoModel(Type dbContextType, string hashedConnectionString)
    {
        var gid = GidProvider.Create();

        return new DtmMsgInfoModel(gid,
            DtmTransFactory.NewMsg(gid),
            new DbConnectionLookupInfoModel(dbContextType, CurrentTenant.Id, hashedConnectionString),
            ServiceProvider.GetService<IDtmRequestHeadersBuilder>()!);
    }

    protected virtual async Task AddEventsPublishingActionAsync(DtmOutboxEventBag eventBag)
    {
        Logger.LogInformation("DTM message addresses: PublishEvents={PublishEvents}, QueryPrepared={QueryPrepared}",
            DtmHttpOptions.GetPublishEventsAddress(), DtmHttpOptions.GetQueryPreparedAddress());

        if (eventBag.DefaultMessage is DtmMsgInfoModel defaultMessage)
        {
            Logger.LogInformation("DTM default message: Gid={Gid}, Events={Count}", defaultMessage.Gid, defaultMessage.EventInfos.Count);
            await defaultMessage.AddEventsPublishingActionAsync(DtmHttpOptions);
        }
        foreach (var model in eventBag.TransMessages.Values.Select(model => model as DtmMsgInfoModel))
        {
            Logger.LogInformation("DTM trans message: Gid={Gid}, Events={Count}", model!.Gid, model.EventInfos.Count);
            await model.AddEventsPublishingActionAsync(DtmHttpOptions);
        }
    }

    protected virtual async Task PrepareTransMessagesAsync(DtmOutboxEventBag eventBag,
        CancellationToken cancellationToken = default)
    {
        foreach (var model in eventBag.TransMessages.Values)
        {
            var msg = (model.DtmMessage as Msg)!;
            await msg.Prepare(DtmHttpOptions.GetQueryPreparedAddress(), cancellationToken);
        }
    }

    protected virtual async Task InsertTransMessagesBarriersAsync(DtmOutboxEventBag eventBag,
        CancellationToken cancellationToken = default)
    {
        foreach (var model in eventBag.TransMessages.Values)
        {
            var barrierManagers = ServiceProvider.GetServices<IDtmMsgBarrierManager>();

            var databaseApi = await GetDatabaseApiAsync(model.DbConnectionLookupInfo.DbContextType);

            var inserted = false;

            foreach (var barrierManager in barrierManagers)
            {
                if (await barrierManager.TryInvokeEnsureInsertBarrierAsync(databaseApi, model.Gid, cancellationToken))
                {
                    inserted = true;
                    break;
                }
            }

            if (!inserted)
            {
                throw new AbpException(
                    $"No match DTM message barrier manager to {model.DbConnectionLookupInfo.DbContextType.Name}.");
            }
        }
    }

    protected virtual async Task<IDatabaseApi> GetDatabaseApiAsync(Type targetDbContextType)
    {
        var connectionString = await ConnectionStringResolver.ResolveAsync(targetDbContextType);

        var databaseApiKey = $"{targetDbContextType.FullName}_{connectionString}";

        var databaseApi = UnitOfWorkManager.Current?.FindDatabaseApi(databaseApiKey);

        Check.NotNull(databaseApi, nameof(databaseApi));

        return databaseApi;
    }
}
