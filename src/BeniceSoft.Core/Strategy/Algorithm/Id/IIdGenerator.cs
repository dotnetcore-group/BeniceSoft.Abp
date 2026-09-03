namespace BeniceSoft.Core.Strategy;

public interface IIdGenerator
{
    long NewSequenceId();

    public long[] NewSequenceId(int count)
    {
        var array = new long[count];
        foreach (var i in count)
        {
            array[i] = NewSequenceId();
        }

        return array;
    }

    public Task<long> NewSequenceIdAsync()
    {
        return Task.FromResult(NewSequenceId());
    }

    public Task<long[]> NewSequenceIdAsync(int count)
    {
        return Task.FromResult(NewSequenceId(count));
    }
}
