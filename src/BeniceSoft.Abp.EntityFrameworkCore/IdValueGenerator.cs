using BeniceSoft.Core.Reflector;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace BeniceSoft.Abp.EntityFrameworkCore;

internal sealed class IdValueGenerator : ValueGenerator<string>
{
    private readonly IIdGenerator generator;
    private readonly string prefix;

    public IdValueGenerator(IIdGenerator generator, string prefix)
    {
        this.generator = generator;
        this.prefix = prefix;
    }

    public override bool GeneratesTemporaryValues { get; }

    public override string Next(EntityEntry entry)
    {
        var entity = entry.Entity;
        var idProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();

        if (idProperty != null)
        {
            var propertyInfo = entity.GetType().GetProperty(idProperty.Name);
            if (propertyInfo != null)
            {
                var currentValue = propertyInfo.GetReflector().GetValue(entity) as string;
                if (!string.IsNullOrEmpty(currentValue))
                {
                    return currentValue;
                }
            }
        }

        return generator.NewId(prefix);
    }
}

internal sealed class Int64ValueGenerator(IIdGenerator generator) : ValueGenerator<long>
{
    public override bool GeneratesTemporaryValues { get; }

    public override long Next(EntityEntry entry)
    {
        var entity = entry.Entity;
        var idProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();

        if (idProperty != null)
        {
            var propertyInfo = entity.GetType().GetProperty(idProperty.Name);
            if (propertyInfo != null)
            {
                var currentValue = propertyInfo.GetReflector().GetValue(entity);
                if (currentValue is long longValue && longValue != 0)
                {
                    return longValue;
                }
            }
        }

        return generator.NewSequenceId();
    }
}
