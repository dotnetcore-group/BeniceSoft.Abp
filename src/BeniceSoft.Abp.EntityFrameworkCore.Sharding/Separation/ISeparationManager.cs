namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ISeparationManager
{
    SeparationContext? Current { get; }

    /// <summary>
    /// 创建路由scope
    /// </summary>
    /// <returns></returns>
    SeparationScope CreateScope();
}

internal sealed class SeparationManager(ISeparationAccessor accessor) : ISeparationManager
{
    public SeparationContext? Current => accessor.Context;

    public SeparationScope CreateScope()
    {
        var previous = accessor.Context;
        accessor.Context = new();
        return new SeparationScope(accessor, previous);
    }
}
