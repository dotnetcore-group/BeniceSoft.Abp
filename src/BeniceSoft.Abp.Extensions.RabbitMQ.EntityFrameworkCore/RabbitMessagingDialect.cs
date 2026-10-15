namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>Provider-specific DDL / paging (same provider names as the DTM barrier tables).</summary>
public abstract class RabbitMessagingDialect
{
    private static readonly RabbitMessagingDialect MySql = new MySqlDialect();
    private static readonly RabbitMessagingDialect PostgreSql = new PostgreSqlDialect();
    private static readonly RabbitMessagingDialect SqlServer = new SqlServerDialect();
    private static readonly RabbitMessagingDialect Sqlite = new SqliteDialect();

    public static Dictionary<string, RabbitMessagingDialect> ProviderMapping { get; } = new()
    {
        { "Pomelo.EntityFrameworkCore.MySql", MySql },
        { "MySql.EntityFrameworkCore", MySql },
        { "Devart.Data.MySql.EFCore", MySql },
        { "Npgsql.EntityFrameworkCore.PostgreSQL", PostgreSql },
        { "Devart.Data.PostgreSql.EFCore", PostgreSql },
        { "Microsoft.EntityFrameworkCore.SqlServer", SqlServer },
        { "Microsoft.EntityFrameworkCore.Sqlite", Sqlite }
    };

    public static RabbitMessagingDialect For(string? providerName)
        => providerName != null && ProviderMapping.TryGetValue(providerName, out var dialect)
            ? dialect
            : throw new NotSupportedException($"Database provider {providerName} is not supported by the RabbitMQ outbox/inbox");

    /// <summary>
    /// True: DDL runs on the business connection/transaction (SQLite; required for in-memory DBs).
    /// False: DDL runs on a separate connection (avoids MySQL implicit commit inside the business transaction).
    /// </summary>
    public virtual bool DdlOnBusinessConnection => false;

    public abstract string CreateTablesSql(RabbitOutboxOptions options);

    /// <summary>List predicate for a Dapper list parameter.</summary>
    public virtual string InList(string column, string parameter) => $"{column} IN @{parameter}";

    /// <summary>SELECT id ... with a row limit parameter @take.</summary>
    public virtual string ClaimCandidatesSql(string outboxTable)
        => $"SELECT id FROM {outboxTable} WHERE status = 0 AND next_retry_time <= @now AND (locked_until IS NULL OR locked_until < @now) ORDER BY creation_time LIMIT @take";

    private sealed class MySqlDialect : RabbitMessagingDialect
    {
        public override string CreateTablesSql(RabbitOutboxOptions o) => $@"
CREATE TABLE IF NOT EXISTS {o.OutboxTable} (
  id varchar(36) NOT NULL PRIMARY KEY,
  message_id varchar(64) NOT NULL,
  exchange_name varchar(200) NOT NULL,
  routing_key varchar(200) NOT NULL,
  connection_name varchar(64) NULL,
  body longblob NOT NULL,
  headers longtext NULL,
  correlation_id varchar(64) NULL,
  message_type varchar(200) NULL,
  status int NOT NULL,
  retry_count int NOT NULL,
  creation_time datetime(6) NOT NULL,
  next_retry_time datetime(6) NOT NULL,
  sent_time datetime(6) NULL,
  lock_token varchar(36) NULL,
  locked_until datetime(6) NULL,
  last_error varchar(1000) NULL,
  KEY ix_{o.OutboxTableName}_status (status, next_retry_time)
);
CREATE TABLE IF NOT EXISTS {o.InboxTable} (
  message_id varchar(64) NOT NULL,
  consumer_name varchar(200) NOT NULL,
  processed_time datetime(6) NOT NULL,
  PRIMARY KEY (message_id, consumer_name),
  KEY ix_{o.InboxTableName}_time (processed_time)
);";
    }

    private sealed class PostgreSqlDialect : RabbitMessagingDialect
    {
        // Dapper passes arrays natively to Npgsql (no IN-list expansion).
        public override string InList(string column, string parameter) => $"{column} = ANY(@{parameter})";

        public override string CreateTablesSql(RabbitOutboxOptions o) => $@"
CREATE TABLE IF NOT EXISTS {o.OutboxTable} (
  id varchar(36) NOT NULL PRIMARY KEY,
  message_id varchar(64) NOT NULL,
  exchange_name varchar(200) NOT NULL,
  routing_key varchar(200) NOT NULL,
  connection_name varchar(64) NULL,
  body bytea NOT NULL,
  headers text NULL,
  correlation_id varchar(64) NULL,
  message_type varchar(200) NULL,
  status integer NOT NULL,
  retry_count integer NOT NULL,
  creation_time timestamptz NOT NULL,
  next_retry_time timestamptz NOT NULL,
  sent_time timestamptz NULL,
  lock_token varchar(36) NULL,
  locked_until timestamptz NULL,
  last_error varchar(1000) NULL
);
CREATE INDEX IF NOT EXISTS ix_{o.OutboxTableName}_status ON {o.OutboxTable} (status, next_retry_time);
CREATE TABLE IF NOT EXISTS {o.InboxTable} (
  message_id varchar(64) NOT NULL,
  consumer_name varchar(200) NOT NULL,
  processed_time timestamptz NOT NULL,
  PRIMARY KEY (message_id, consumer_name)
);
CREATE INDEX IF NOT EXISTS ix_{o.InboxTableName}_time ON {o.InboxTable} (processed_time);";
    }

    private sealed class SqlServerDialect : RabbitMessagingDialect
    {
        public override string ClaimCandidatesSql(string outboxTable)
            => $"SELECT TOP (@take) id FROM {outboxTable} WHERE status = 0 AND next_retry_time <= @now AND (locked_until IS NULL OR locked_until < @now) ORDER BY creation_time";

        public override string CreateTablesSql(RabbitOutboxOptions o) => $@"
IF OBJECT_ID(N'{o.OutboxTable}', N'U') IS NULL
BEGIN
  CREATE TABLE {o.OutboxTable} (
    id nvarchar(36) NOT NULL PRIMARY KEY,
    message_id nvarchar(64) NOT NULL,
    exchange_name nvarchar(200) NOT NULL,
    routing_key nvarchar(200) NOT NULL,
    connection_name nvarchar(64) NULL,
    body varbinary(max) NOT NULL,
    headers nvarchar(max) NULL,
    correlation_id nvarchar(64) NULL,
    message_type nvarchar(200) NULL,
    status int NOT NULL,
    retry_count int NOT NULL,
    creation_time datetime2 NOT NULL,
    next_retry_time datetime2 NOT NULL,
    sent_time datetime2 NULL,
    lock_token nvarchar(36) NULL,
    locked_until datetime2 NULL,
    last_error nvarchar(1000) NULL
  );
  CREATE INDEX ix_{o.OutboxTableName}_status ON {o.OutboxTable} (status, next_retry_time);
END;
IF OBJECT_ID(N'{o.InboxTable}', N'U') IS NULL
BEGIN
  CREATE TABLE {o.InboxTable} (
    message_id nvarchar(64) NOT NULL,
    consumer_name nvarchar(200) NOT NULL,
    processed_time datetime2 NOT NULL,
    CONSTRAINT pk_{o.InboxTableName} PRIMARY KEY (message_id, consumer_name)
  );
  CREATE INDEX ix_{o.InboxTableName}_time ON {o.InboxTable} (processed_time);
END;";
    }

    private sealed class SqliteDialect : RabbitMessagingDialect
    {
        public override bool DdlOnBusinessConnection => true;

        public override string CreateTablesSql(RabbitOutboxOptions o) => $@"
CREATE TABLE IF NOT EXISTS {o.OutboxTable} (
  id TEXT NOT NULL PRIMARY KEY,
  message_id TEXT NOT NULL,
  exchange_name TEXT NOT NULL,
  routing_key TEXT NOT NULL,
  connection_name TEXT NULL,
  body BLOB NOT NULL,
  headers TEXT NULL,
  correlation_id TEXT NULL,
  message_type TEXT NULL,
  status INTEGER NOT NULL,
  retry_count INTEGER NOT NULL,
  creation_time TEXT NOT NULL,
  next_retry_time TEXT NOT NULL,
  sent_time TEXT NULL,
  lock_token TEXT NULL,
  locked_until TEXT NULL,
  last_error TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_{o.OutboxTableName}_status ON {o.OutboxTable} (status, next_retry_time);
CREATE TABLE IF NOT EXISTS {o.InboxTable} (
  message_id TEXT NOT NULL,
  consumer_name TEXT NOT NULL,
  processed_time TEXT NOT NULL,
  PRIMARY KEY (message_id, consumer_name)
);
CREATE INDEX IF NOT EXISTS ix_{o.InboxTableName}_time ON {o.InboxTable} (processed_time);";
    }
}