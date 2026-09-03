using BeniceSoft.Core;
using CronExpr = BeniceSoft.Core.Strategy.CronExpression;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class ShardingJobEntry
{
    /// <summary>
    /// 保证多线程只有一个清理操作
    /// </summary>
    private const int RunningStatus = 1;

    /// <summary>
    /// 为运行
    /// </summary>
    private const int UnrunningStatus = 0;

    private int _runStatus;

    public ShardingJobEntry(IShardingJob job)
    {
        Instance = job;
        Name = job.Name;
        CronExpression = job.CronExpression;
        if (CronExpression.IsNull())
        {
            throw new ArgumentException($" {nameof(CronExpression)} is null");
        }

        if (CronExpression.Any(o => o.IsNull()))
        {
            throw new ArgumentException($"{nameof(CronExpression)} has null element");
        }
    }

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 任务实例
    /// </summary>
    public IShardingJob Instance { get; set; }

    /// <summary>
    /// 如果正在运行是否跳过
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// 下次运行时间
    /// </summary>
    public DateTimeOffset? NextUtcTime { get; private set; }

    /// <summary>
    /// 执行周期（Quartz cron 表达式列表）
    /// </summary>
    public IEnumerable<string> CronExpression { get; set; }

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool Running => _runStatus == RunningStatus;

    public bool Start()
    {
        if (Skip)
        {
            return Interlocked.CompareExchange(ref _runStatus, RunningStatus, UnrunningStatus) == UnrunningStatus;
        }

        return true;
    }

    public void Complete()
    {
        if (Skip)
        {
            _runStatus = UnrunningStatus;
        }
    }

    /// <summary>
    /// 计算下一次执行时间
    /// </summary>
    internal void CalcNextUtcTime()
    {
        NextUtcTime = CronExpression.Select(cron => new CronExpr(cron).GetTimeAfter(DateTime.UtcNow)).Min();
    }
}

internal sealed class ShardingJobEntryFactory
{
    public static ShardingJobEntry Create(IShardingJob job)
    {
        var jobEntry = new ShardingJobEntry(job);
        jobEntry.CalcNextUtcTime();
        return jobEntry;
    }
}
