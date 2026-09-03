namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IMergeAccessor
{
    MergeContext? Context { get; set; }
}

internal sealed class MergeAccessor : IMergeAccessor
{
    private static readonly AsyncLocal<MergeContext?> _local = new();

    public MergeContext? Context
    {
        get => _local.Value;
        set => _local.Value = value;
    }
}
