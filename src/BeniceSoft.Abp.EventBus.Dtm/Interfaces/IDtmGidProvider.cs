using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmGidProvider
{
    string Create();
}

public class DtmGidProvider : IDtmGidProvider, ITransientDependency
{
    private readonly IGuidGenerator _guidGenerator;

    public DtmGidProvider(IGuidGenerator guidGenerator)
    {
        _guidGenerator = guidGenerator;
    }

    public string Create()
    {
        return _guidGenerator.Create().ToString();
    }
}