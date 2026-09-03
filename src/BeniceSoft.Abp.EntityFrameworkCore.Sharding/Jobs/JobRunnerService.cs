using BeniceSoft.Core;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class JobRunnerService(IShardingJobManager jobManager, ILogger<JobRunnerService> logger)
{
    private const int DefaultDelay = 1000;
    /// <summary>
    /// 最大休眠时间30秒
    /// </summary>
    private const int MaxDelay = 30000;

    private readonly ILogger<JobRunnerService> _logger = logger;
    private readonly IShardingJobManager _jobManager = jobManager;
    private readonly CancellationTokenSource _cts = new();

    public async Task StartAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            var delayMs = 0;
            try
            {
                delayMs = LoopJobAndGetWaitMillis().ToInt32();
            }
            catch (Exception e)
            {
                _logger.LogError($"job runner service exception : {e}");
                await Task.Delay(DefaultDelay, _cts.Token);
            }

            if (delayMs > 0)
            {
                await Task.Delay(Math.Min(MaxDelay, delayMs), _cts.Token); //最大休息为MAX_DELAY_MILLIS
            }
        }
    }

    public Task StopAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// get wait time
    /// </summary>
    /// <returns>next utc time that job when restart</returns>
    private long LoopJobAndGetWaitMillis()
    {
        var beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var runJobs = _jobManager.GetNowRunJobs();
        long costTime;
        if (!runJobs.Any())
        {
            var minJobUtcTime = _jobManager.GetNextUtcTime();
            if (!minJobUtcTime.HasValue)
            {
                //return wait one second
                costTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - beginTime;
                if (DefaultDelay < costTime)
                {
                    return 0L;
                }

                return DefaultDelay - costTime;
            }
            else
            {
                //return next job run time
                return minJobUtcTime.Value.ToUnixTimeMilliseconds() - beginTime;
            }
        }

        foreach (var job in runJobs)
        {
            DoJob(job);
        }

        costTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - beginTime;
        if (costTime > DefaultDelay)
        {
            return 0L;
        }

        return DefaultDelay - costTime;
    }

    private void DoJob(ShardingJobEntry jobEntry)
    {
        if (jobEntry.Start())
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var job = jobEntry.Instance;
                    if (job == null)
                    {
                        _logger.LogWarning($"###  job  [{jobEntry.Name}] is null ");
                        return;
                    }

                    _logger.LogInformation($"###  job  [{jobEntry.Name}]  start success.");
                    await job.ExecuteAsync();
                    _logger.LogInformation($"###  job  [{jobEntry.Name}]  invoke complete.");
                    jobEntry.CalcNextUtcTime();

                    if (!jobEntry.NextUtcTime.HasValue)
                    {
                        _logger.LogWarning($"###  job [{jobEntry.Name}] is stopped.");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"###  job [{jobEntry.Name}]  invoke fail : {e}.");
                }
                finally
                {
                    jobEntry.Complete();
                }
            });
        }
    }
}
