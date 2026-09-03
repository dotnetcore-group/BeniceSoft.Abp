using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingBootstrapper
{
    void Create();
}

internal sealed class ShardingBootstrapper(IShardingProvider shardingProvider) : IShardingBootstrapper
{
    private readonly OnceLock _lock = new();

    public void Create()
    {
        if (!_lock.IsAcquired)
        {
            return;
        }

        StartAutoShardingJob();
    }

    private void StartAutoShardingJob()
    {
        var runnerService = shardingProvider.GetRequiredService<JobRunnerService>(false);

        Task.Factory.StartNew(runnerService.StartAsync, TaskCreationOptions.LongRunning);
    }
}
