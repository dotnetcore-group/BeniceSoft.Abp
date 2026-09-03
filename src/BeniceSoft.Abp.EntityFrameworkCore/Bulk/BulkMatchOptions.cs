using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Bulk;

/// <summary>
/// Update / Delete / Merge 的匹配列配置
/// </summary>
public class BulkMatchOptions<T>
    where T : class
{
    private readonly EfCoreBulkAtom<T> _atom;

    public BulkMatchOptions(EfCoreBulkAtom<T> atom)
    {
        _atom = atom;
    }

    /// <summary>MERGE / JOIN 中源表别名</summary>
    public string SourceAlias { get; set; } = "Source";

    /// <summary>MERGE / JOIN 中目标表别名</summary>
    public string TargetAlias { get; set; } = "Target";

    /// <summary>显式指定的匹配列（数据库列映射）</summary>
    public ICollection<Microsoft.EntityFrameworkCore.Metadata.IColumnMapping> MatchColumns { get; } = [];

    /// <summary>
    /// 指定用于对齐目标表的业务主键
    /// 例：<c>m => m.MatchTargetOn(x => x.Id)</c> 或复合键 <c>x => new { x.A, x.B }</c>。
    /// </summary>
    public void MatchTargetOn(Expression<Func<T, object>> keyExpression)
    {
        var properties = keyExpression.GetProperties();
        foreach (var property in properties)
        {
            var mapping = _atom.ColumnMappings.FirstOrDefault(t => t.Property.PropertyInfo?.Name == property.Name)
                          ?? throw new InvalidDataException($"Property '{property.Name}' not found in ColumnMappings");
            MatchColumns.Add(mapping);
        }
    }

    /// <summary>解析最终匹配列名：优先显式 MatchColumns，否则取实体第一个主键</summary>
    public IList<string> GetMatchColumns()
    {
        if (MatchColumns.IsNotNull())
        {
            return MatchColumns.Select(t => t.Column.Name).ToArray();
        }

        var key = _atom.EntityType.GetKeys().FirstOrDefault()
                  ?? throw new InvalidDataException("Entity has no primary key for match columns.");

        var columns = new List<string>();
        foreach (var property in key.Properties)
        {
            var mapping = _atom.ColumnMappings.FirstOrDefault(t => t.Property == property)
                          ?? throw new InvalidDataException($"PrimaryKey Property '{property.Name}' not found in ColumnMappings");
            columns.Add(mapping.Column.Name);
        }

        if (columns.Count == 0)
        {
            throw new InvalidDataException(
                "MatchTargetOn list is empty when it's required for this operation. This is usually the primary key of your entity.");
        }

        return columns;
    }
}
