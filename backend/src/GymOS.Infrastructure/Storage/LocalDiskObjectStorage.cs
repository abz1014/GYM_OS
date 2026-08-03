using GymOS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace GymOS.Infrastructure.Storage;

public class LocalDiskObjectStorage(IOptions<StorageSettings> options) : IObjectStorage
{
    private readonly StorageSettings _settings = options.Value;

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);

        return GetPublicUrl(key);
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(ResolvePath(key));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key.TrimStart('/')}";

    private string ResolvePath(string key) => Path.Combine(_settings.LocalBasePath, key.Replace('/', Path.DirectorySeparatorChar));
}
