using Amazon.S3;
using Amazon.S3.Model;
using GymOS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace GymOS.Infrastructure.Storage;

/// <summary>Works against any S3-compatible endpoint — MinIO for local/dev parity, a real bucket after client approval — via S3ServiceUrl.</summary>
public class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly StorageSettings _settings;

    public S3ObjectStorage(IOptions<StorageSettings> options)
    {
        _settings = options.Value;

        var config = new AmazonS3Config
        {
            ServiceURL = _settings.S3ServiceUrl,
            ForcePathStyle = true
        };

        _client = new AmazonS3Client(_settings.S3AccessKey, _settings.S3SecretKey, config);
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _settings.S3BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, cancellationToken);

        return GetPublicUrl(key);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(_settings.S3BucketName, key, cancellationToken);
        return response.ResponseStream;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => _client.DeleteObjectAsync(_settings.S3BucketName, key, cancellationToken);

    public string GetPublicUrl(string key) => $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key.TrimStart('/')}";
}
