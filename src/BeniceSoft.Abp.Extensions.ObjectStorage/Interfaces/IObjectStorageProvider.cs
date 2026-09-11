namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public interface IObjectStorageProvider
{
    StorageProviderType ProviderType { get; }

    Task PutAsync(ObjectPutRequest request, CancellationToken cancellationToken = default);

    Task<Stream> GetAsync(string bucket, string objectKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default);

    Task<ObjectMetadataInfo?> GetMetadataAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expires,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    Task<UploadCredentialResult> GetUploadCredentialAsync(
        UploadCredentialRequest request,
        CancellationToken cancellationToken = default);

    Task<string> InitiateMultipartAsync(
        string bucket,
        string objectKey,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<MultipartUploadedPart> UploadPartAsync(
        MultipartUploadPartRequest request,
        CancellationToken cancellationToken = default);

    Task CompleteMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default);

    Task AbortMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default);
}
