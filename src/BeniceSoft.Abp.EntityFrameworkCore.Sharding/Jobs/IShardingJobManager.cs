namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingJobManager
{
    void AddJob(ShardingJobEntry jobEntry);

    bool HasAnyJob();

    IEnumerable<ShardingJobEntry> GetNowRunJobs();

    DateTimeOffset? GetNextUtcTime();
}

internal sealed class ShardingJobManager : IShardingJobManager
{
    private readonly List<ShardingJobEntry> _jobs = [];

    public void AddJob(ShardingJobEntry jobEntry)
    {
        if (_jobs.Exists(job => job.Name == jobEntry.Name))
        {
            throw new ArgumentException($"发现重复的任务名称:{jobEntry.Name},请确认");
        }

        _jobs.Add(jobEntry);
    }

    public DateTimeOffset? GetNextUtcTime()
    {
        var waitRunJobs = _jobs.FindAll(o => o.NextUtcTime.HasValue && !o.Running);
        if (waitRunJobs.Count == 0)
        {
            return null;
        }

        return waitRunJobs.Select(o => o.NextUtcTime!.Value).Min();
    }

    public IEnumerable<ShardingJobEntry> GetNowRunJobs()
    {
        var now = DateTime.UtcNow;
        return _jobs.FindAll(o => o.NextUtcTime.HasValue && o.NextUtcTime.Value <= now && !o.Running);
    }

    public bool HasAnyJob()
    {
        return _jobs.Count != 0;
    }
}

