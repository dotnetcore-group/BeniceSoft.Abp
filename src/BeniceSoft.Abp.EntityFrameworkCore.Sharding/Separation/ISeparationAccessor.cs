namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ISeparationAccessor
{
    SeparationContext? Context { get; set; }
}

internal sealed class SeparationAccessor : ISeparationAccessor
{
    private static readonly AsyncLocal<SeparationContext?> _local = new();

    public SeparationContext? Context
    {
        get => _local.Value;
        set => _local.Value = value;
    }
}

public sealed class SeparationScope(ISeparationAccessor accessor, SeparationContext? previous) : IDisposable
{
    public ISeparationAccessor Accessor { get; } = accessor;

    public void Dispose()
    {
        Accessor.Context = previous;
        GC.SuppressFinalize(this);
    }
}
