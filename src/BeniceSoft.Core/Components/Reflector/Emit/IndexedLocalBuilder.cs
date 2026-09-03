using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

internal readonly struct IndexedLocalBuilder(LocalBuilder localBuilder, int index)
{
    public LocalBuilder LocalBuilder { get; } = localBuilder;

    public Type LocalType { get; } = localBuilder.LocalType;

    public int Index { get; } = index;

    public int LocalIndex { get; } = localBuilder.LocalIndex;
}
