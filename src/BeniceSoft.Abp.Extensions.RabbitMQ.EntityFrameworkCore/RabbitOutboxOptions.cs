namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

public class RabbitOutboxOptions
{
    /// <summary>
    /// DbContext used by the dispatcher, cleanup and inbox. Auto-detected when the host registers exactly one
    /// DbContext; set it explicitly when there are several. Outbox writes always prefer the DbContext already
    /// active in the current unit of work.
    /// </summary>
    public Type? DbContextType { get; set; }

    /// <summary>Optional schema (PostgreSQL/SQL Server schema, MySQL database). Null = connection default.</summary>
    public string? Schema { get; set; }

    public string OutboxTableName { get; set; } = "rabbit_outbox_messages";

    public string InboxTableName { get; set; } = "rabbit_inbox_messages";

    /// <summary>Run the background dispatcher in this process. Default true.</summary>
    public bool DispatcherEnabled { get; set; } = true;

    /// <summary>Idle polling interval; commits also wake the dispatcher immediately.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 100;

    /// <summary>Row claim lease. A crashed instance's rows are picked up again after this.</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>After this many failed sends the row becomes Failed (status 2).</summary>
    public int MaxRetryCount { get; set; } = 20;

    /// <summary>Backoff = min(RetryBaseDelay * 2^(retry-1), RetryMaxDelay).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan SentRetention { get; set; } = TimeSpan.FromDays(3);

    public TimeSpan InboxRetention { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    internal string OutboxTable => Schema is null or "" ? OutboxTableName : $"{Schema}.{OutboxTableName}";

    internal string InboxTable => Schema is null or "" ? InboxTableName : $"{Schema}.{InboxTableName}";
}

public static class RabbitOutboxStatus
{
    public const int Pending = 0;
    public const int Sent = 1;

    /// <summary>MaxRetryCount reached; kept for inspection. Set status back to 0 to resend.</summary>
    public const int Failed = 2;
}