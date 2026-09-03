using System.Reflection;
using Dtmcli;
using DtmCommon;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface IDtmTransFactory
{
    /// <summary>
    /// 创建 MSG 消息事务
    /// </summary>
    Dtmcli.Msg NewMsg(string gid);

    /// <summary>
    /// 创建 Saga 事务
    /// </summary>
    Dtmcli.Saga NewSaga(string gid);

    /// <summary>
    /// 创建 TCC 事务
    /// </summary>
    Dtmcli.Tcc NewTcc(string gid);
}

public class AbpDtmTransFactory : IDtmTransFactory, ITransientDependency
{
    protected IDtmClient DtmClient { get; }
    protected IBranchBarrierFactory BarrierFactory { get; }
    protected DtmHttpOptions DtmHttpOptions { get; }

    public AbpDtmTransFactory(
        IDtmClient dtmClient,
        IBranchBarrierFactory barrierFactory,
        IOptions<DtmHttpOptions> dtmHttpOptions)
    {
        DtmClient = dtmClient;
        BarrierFactory = barrierFactory;
        DtmHttpOptions = dtmHttpOptions.Value;
    }

    public virtual Dtmcli.Msg NewMsg(string gid)
    {
        var msg = new Dtmcli.Msg(DtmClient, BarrierFactory, gid);

        if (DtmHttpOptions.MessageTimeoutToFail > 0)
        {
            msg = InvokeOptionalConfig(msg, "TimeoutToFail", DtmHttpOptions.MessageTimeoutToFail);
        }

        if (DtmHttpOptions.MessageRetryInterval > 0)
        {
            msg = InvokeOptionalConfig(msg, "RetryInterval", DtmHttpOptions.MessageRetryInterval);
        }

        if (DtmHttpOptions.MessageRetryLimit > 0)
        {
            msg = InvokeOptionalConfig(msg, "RetryLimit", DtmHttpOptions.MessageRetryLimit);
        }

        return msg;
    }

    private static Dtmcli.Msg InvokeOptionalConfig(Dtmcli.Msg msg, string methodName, int value)
    {
        var method = typeof(Dtmcli.Msg).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);
        if (method == null)
        {
            return msg;
        }

        return method.Invoke(msg, [value]) as Dtmcli.Msg ?? msg;
    }

    public virtual Dtmcli.Saga NewSaga(string gid)
    {
        var saga = new Dtmcli.Saga(DtmClient, gid);
        return saga;
    }

    public virtual Dtmcli.Tcc NewTcc(string gid)
    {
        var tcc = new Dtmcli.Tcc(DtmClient, TransBase.NewTransBase(gid, DtmCommon.Constant.TYPE_TCC, "", ""));
        return tcc;
    }
}
