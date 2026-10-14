namespace BeniceSoft.Abp.Core;

/// <summary>
/// 基础数据值填充（按特性把 Id 对应的名称写回 DTO）
/// </summary>
public interface IBasicDataValueFiller
{
    /// <summary>
    /// 单个填充
    /// </summary>
    Task FillAsync<T>(T source) where T : class;

    /// <summary>
    /// 批量填充
    /// </summary>
    Task FillManyAsync<T>(IReadOnlyList<T> source) where T : class;
}
