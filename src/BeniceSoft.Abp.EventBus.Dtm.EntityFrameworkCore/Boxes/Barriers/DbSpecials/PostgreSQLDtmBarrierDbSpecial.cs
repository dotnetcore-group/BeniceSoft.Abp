using DtmCommon;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public class PostgreSQLDtmBarrierDbSpecial : PostgresDBSpecial, IDtmBarrierDbSpecial
{
    public static string DefaultBarrierTableName { get; set; } = "dtm.barrier";
    public static string DtmSequenceName { get; set; } = "barrier_seq";

    public static string DtmBarrierTableAndValueSqlFormat { get; set; } =
        "{0}(trans_type, gid, branch_id, op, barrier_id, reason) values(@trans_type,@gid,@branch_id,@op,@barrier_id,@reason)";

    public static string QueryPreparedSqlFormat { get; set; } =
        "select reason from {0} where gid=@gid and branch_id=@branch_id and op=@op and barrier_id=@barrier_id";

    public virtual string GetCreateBarrierTableSql(DtmEventBoxesOptions options)
    {
        var configuredTableName = ResolveBarrierTableName(options.BarrierTableName);
        var split = configuredTableName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);


        var schemaName = split.Length == 2 ? $"\"{split[0]}\"" : null;
        var tableName = split.Length == 2 ? $"\"{split[1]}\"" : $"\"{configuredTableName}\"";
        var tableFullName = schemaName is null ? tableName : $"{schemaName}.{tableName}";
        var sequenceName = $"\"{DtmSequenceName}\"";
        var sequenceFullName = schemaName is null ? sequenceName : $"{schemaName}.{sequenceName}";

        var sql = string.Empty;

        if (schemaName is not null)
        {
            sql += $@"
create schema if not exists {schemaName};
";
        }

        sql += $@"
CREATE SEQUENCE if not EXISTS {sequenceFullName};
create table if not exists {tableFullName}(
  id bigint NOT NULL DEFAULT NEXTVAL ('{sequenceFullName}'),
  trans_type varchar(45) default '',
  gid varchar(128) default '',
  branch_id varchar(128) default '',
  op varchar(45) default '',
  barrier_id varchar(45) default '',
  reason varchar(45) default '',
  create_time timestamp(0) with time zone DEFAULT NULL,
  update_time timestamp(0) with time zone DEFAULT NULL,
  PRIMARY KEY(id),
  CONSTRAINT uniq_barrier unique(gid, branch_id, op, barrier_id)
);
";
        return sql;
    }

    public virtual string GetInsertIgnoreTemplate(string tableName)
    {
        return base.GetInsertIgnoreTemplate(
            string.Format(DtmBarrierTableAndValueSqlFormat, ResolveBarrierTableName(tableName)),
            Constant.Barrier.PG_CONSTRAINT);
    }

    public virtual string GetQueryPreparedSql(string tableName)
    {
        return string.Format(QueryPreparedSqlFormat, ResolveBarrierTableName(tableName));
    }

    private static string ResolveBarrierTableName(string? tableName)
    {
        return string.IsNullOrWhiteSpace(tableName) ? DefaultBarrierTableName : tableName.Trim();
    }
}
