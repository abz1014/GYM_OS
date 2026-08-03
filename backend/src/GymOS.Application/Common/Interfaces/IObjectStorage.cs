namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over file storage (profile photos, progress photos, equipment manuals/photos).
/// LocalDiskObjectStorage backs this in dev; S3ObjectStorage points at any S3-compatible endpoint
/// (MinIO now, a real bucket later) via the same interface.
/// </summary>
public interface IObjectStorage
{
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    string GetPublicUrl(string key);
}
