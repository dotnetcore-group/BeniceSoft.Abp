namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ISeparationConnectionFactory
{
    ISeparationConnection Create(SeparationReadStrategy strategy, string dataSource, SeparationReadNode[] readNodes);
}

internal sealed class SeparationConnectionFactory : ISeparationConnectionFactory
{
    public ISeparationConnection Create(SeparationReadStrategy strategy, string dataSource, SeparationReadNode[] readNodes)
    {
        return strategy switch
        {
            SeparationReadStrategy.Loop => new SeparationLoopConnection(dataSource, readNodes),
            SeparationReadStrategy.Random => new SeparationRandomConnection(dataSource, readNodes),
            _ => throw new ShardingInvalidOperationException($"unknown read write strategy:[{strategy}]"),
        };
    }
}
