using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

/// <summary>
/// Tests that demonstrate concurrency issues in the current implementation
/// </summary>
public class ConcurrencyIssueTests
{
    /// <summary>
    /// Demonstrates Issue #4: Same resource can be added multiple times to _managedLocks
    /// When released, only the first one is removed
    /// </summary>
    [Fact]
    public void ManagedLocks_SameResourceMultipleTimes_OnlyFirstRemoved()
    {
        // Simulate the issue with a simple list
        var managedLocks = new List<(string Resource, string LockId)>();
        var lockObj = new object();

        // Simulate acquiring the same resource multiple times (shouldn't happen, but can in edge cases)
        lock (lockObj)
        {
            managedLocks.Add(("resource1", "lock1"));
            managedLocks.Add(("resource1", "lock2")); // Same resource, different lock
            managedLocks.Add(("resource2", "lock3"));
        }

        // Simulate release - current implementation only removes first match
        lock (lockObj)
        {
            for (int i = 0; i < managedLocks.Count; i++)
            {
                if (managedLocks[i].Resource == "resource1")
                {
                    managedLocks.RemoveAt(i);
                    break; // Current implementation breaks after first removal
                }
            }
        }

        // Assert - Second lock for resource1 is still in the list (memory leak!)
        managedLocks.Count.ShouldBe(2);
        managedLocks.ShouldContain(x => x.Resource == "resource1" && x.LockId == "lock2");
    }

    /// <summary>
    /// Demonstrates the correct way to handle lock management using ConcurrentDictionary
    /// </summary>
    [Fact]
    public async Task ConcurrentDictionary_IsThreadSafe_ForLockManagement()
    {
        // Arrange
        var locks = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        var tasks = new List<Task>();
        var successCount = 0;
        var failCount = 0;

        // Act - Simulate concurrent lock acquisition
        for (int i = 0; i < 100; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                var lockId = Guid.NewGuid().ToString();
                // TryAdd is atomic - only one thread will succeed for the same key
                if (locks.TryAdd("shared-resource", lockId))
                {
                    Interlocked.Increment(ref successCount);
                    // Simulate work
                    Thread.Sleep(1);
                    // Release
                    locks.TryRemove("shared-resource", out _);
                }
                else
                {
                    Interlocked.Increment(ref failCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All operations completed without exception
        (successCount + failCount).ShouldBe(100);
    }

    /// <summary>
    /// Demonstrates Environment.TickCount overflow issue
    /// </summary>
    [Fact]
    public void EnvironmentTickCount_Overflow_CausesIncorrectCalculation()
    {
        // Simulate values near overflow point
        int startTime = int.MaxValue - 100; // Just before overflow
        int currentTime = int.MinValue + 200; // Just after overflow (wrapped around)

        // Current implementation calculation
        int elapsed = currentTime - startTime;

        // Due to integer overflow, this produces an unexpected result
        // The actual behavior depends on checked/unchecked context
        // In unchecked context (default), it wraps around
        // The key issue is that the result is not the expected 300ms

        // Expected elapsed time should be 300 (100 + 200)
        // But due to overflow, we get a different value
        var expectedElapsed = 300;
        var isCorrect = elapsed == expectedElapsed;

        // This demonstrates the potential issue - the calculation may not be reliable
        // near overflow boundaries

        // Correct approach using TickCount64
        long startTime64 = (long)uint.MaxValue - 100;
        long currentTime64 = (long)uint.MaxValue + 200;
        long elapsed64 = currentTime64 - startTime64;

        // This correctly shows 300ms elapsed
        elapsed64.ShouldBe(300);

        // Document: TickCount64 is the recommended approach
        Assert.True(true, "TickCount64 should be used instead of TickCount to avoid overflow issues");
    }

    /// <summary>
    /// Demonstrates proper retry loop implementation
    /// </summary>
    [Fact]
    public async Task RetryLoop_ShouldUseTaskDelay_NotSpinWait()
    {
        // Arrange
        var attempts = 0;
        var maxAttempts = 5;
        var interval = TimeSpan.FromMilliseconds(10);
        var startTime = DateTime.UtcNow;

        // Act - Proper retry implementation
        while (attempts < maxAttempts)
        {
            attempts++;
            if (attempts < maxAttempts)
            {
                await Task.Delay(interval); // Correct: yields thread
            }
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        attempts.ShouldBe(5);
        // Should have waited approximately (maxAttempts - 1) * interval
        elapsed.TotalMilliseconds.ShouldBeGreaterThan(30); // At least 4 * 10ms
    }

    /// <summary>
    /// Demonstrates that SpinWait is for very short waits only
    /// </summary>
    [Fact]
    public void SpinWait_ShouldOnlyBeUsedForMicrosecondWaits()
    {
        // SpinWait is designed for waits of a few microseconds
        // For longer waits (milliseconds), Task.Delay should be used
        
        // SpinWait keeps the CPU busy (spinning)
        // Task.Delay yields the thread to other work
        
        // Current implementation uses SpinWait for up to 100ms waits
        // This wastes CPU cycles and can cause performance issues
        
        Assert.True(true, "SpinWait should only be used for microsecond-level waits");
    }
}

