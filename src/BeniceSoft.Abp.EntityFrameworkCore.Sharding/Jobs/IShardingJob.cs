namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingJob
{
    /// <summary>
    /// 任务名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行周期
    /// </summary>
    IEnumerable<string> CronExpression { get; }

    /// <summary>
    /// 任务是否需要添加到默认的任务里面
    /// </summary>
    bool Appended { get; }

    /// <summary>
    /// 如何执行任务
    /// </summary>
    /// <returns></returns>
    Task ExecuteAsync();
}
