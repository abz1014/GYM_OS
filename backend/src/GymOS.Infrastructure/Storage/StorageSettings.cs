namespace GymOS.Infrastructure.Storage;

public class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>"Local" or "S3" — Local is the dev default; S3 works against any S3-compatible endpoint (MinIO now, a real bucket later).</summary>
    public string Provider { get; set; } = "Local";

    public string LocalBasePath { get; set; } = "App_Data/uploads";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000/uploads";

    public string? S3BucketName { get; set; }

    public string? S3ServiceUrl { get; set; }

    public string? S3AccessKey { get; set; }

    public string? S3SecretKey { get; set; }
}
