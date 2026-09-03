namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IStreamMergeCombine
{
    IStreamMergeEnumerator<T> Combine<T>(StreamMergeContext context, IStreamMergeEnumerator<T>[] enumerators);
}
