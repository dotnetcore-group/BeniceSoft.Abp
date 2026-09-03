using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// ForceSave / ExecuteStrategy
/// </summary>
public static class DbContextSaveExtensions
{
    /// <summary>
    /// Saves changes and retries on concurrency conflict by refreshing OriginalValues (client wins for non-token fields).
    /// </summary>
    public static int ForceSaveChange(this DbContext ctx, int retryCount = 2)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        var attempt = 0;
        while (true)
        {
            try
            {
                return ctx.SaveChanges();
            }
            catch (Exception ex) when (attempt < retryCount && TryGetConcurrencyException(ex, out var concurrency))
            {
                attempt++;
                RefreshOriginalValues(concurrency);
                Thread.Sleep(500);
            }
        }
    }

    /// <inheritdoc cref="ForceSaveChange"/>
    public static async Task<int> ForceSaveChangeAsync(this DbContext ctx, int retryCount = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        var attempt = 0;
        while (true)
        {
            try
            {
                return await ctx.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < retryCount && TryGetConcurrencyException(ex, out var concurrency))
            {
                attempt++;
                RefreshOriginalValues(concurrency);
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes <paramref name="operation"/> inside EF execution strategy.
    /// Reuses the current transaction when one exists; otherwise begins and commits a new one.
    /// </summary>
    public static async Task<T> ExecuteStrategyAsync<T>(this DbContext ctx, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async cts =>
        {
            var hasOuterTransaction = ctx.Database.CurrentTransaction != null;
            IDbContextTransaction? owned = null;
            if (!hasOuterTransaction)
            {
                owned = await ctx.Database.BeginTransactionAsync(cts);
            }

            try
            {
                var result = await operation(cts);
                if (owned != null)
                {
                    await owned.CommitAsync(cts);
                }

                return result;
            }
            catch
            {
                if (owned != null)
                {
                    await owned.RollbackAsync(cts);
                }

                throw;
            }
            finally
            {
                if (owned != null)
                {
                    await owned.DisposeAsync();
                }
            }
        }, cancellationToken);
    }

    /// <inheritdoc cref="ExecuteStrategyAsync{T}"/>
    public static Task ExecuteStrategyAsync(this DbContext ctx, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        return ctx.ExecuteStrategyAsync(async cts =>
        {
            await operation(cts);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc cref="ExecuteStrategyAsync{T}"/>
    public static T ExecuteStrategy<T>(this DbContext ctx, Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = ctx.Database.CreateExecutionStrategy();
        return strategy.Execute(() =>
        {
            var hasOuterTransaction = ctx.Database.CurrentTransaction != null;
            IDbContextTransaction? owned = null;
            if (!hasOuterTransaction)
            {
                owned = ctx.Database.BeginTransaction();
            }

            try
            {
                var result = operation();
                owned?.Commit();
                return result;
            }
            catch
            {
                owned?.Rollback();
                throw;
            }
            finally
            {
                owned?.Dispose();
            }
        });
    }

    /// <inheritdoc cref="ExecuteStrategy{T}"/>
    public static void ExecuteStrategy(this DbContext ctx, Action operation)
    {
        ctx.ExecuteStrategy(() =>
        {
            operation();
            return true;
        });
    }

    private static bool TryGetConcurrencyException(Exception ex, out DbUpdateConcurrencyException concurrency)
    {
        for (var current = ex; current != null; current = current.InnerException!)
        {
            if (current is DbUpdateConcurrencyException dbEx)
            {
                concurrency = dbEx;
                return true;
            }
        }

        concurrency = null!;
        return false;
    }

    private static void RefreshOriginalValues(DbUpdateConcurrencyException ex)
    {
        foreach (var entry in ex.Entries)
        {
            var databaseValues = entry.GetDatabaseValues();
            if (databaseValues == null)
            {
                continue;
            }

            entry.OriginalValues.SetValues(databaseValues);

            // Keep business field CurrentValues; sync concurrency tokens to DB so the retry can succeed.
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsConcurrencyToken)
                {
                    property.CurrentValue = databaseValues[property.Metadata.Name];
                }
            }
        }
    }
}
