using Amazon.S3;
using Amazon.S3.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Avtolider.DataMigration.Services;

/// <summary>
/// Handles local PNG file → ImageSharp processing → MinIO upload for migration.
/// Converts to WebP, generates 200x200 thumbnail.
/// Returns (imageKey, thumbKey) on success, null on failure.
/// </summary>
public sealed class ImageMigrationService(IAmazonS3 s3, string bucket)
{
    private const int MaxDimension = 1200;
    private const int ThumbnailSize = 200;

    /// <summary>
    /// Processes a local image file and uploads main + thumbnail to MinIO.
    /// Returns (imageKey, thumbKey) or null if upload fails.
    /// </summary>
    public async Task<(string ImageKey, string ThumbKey)?> UploadAsync(
        string localFilePath,
        string categorySlug,
        CancellationToken ct = default)
    {
        if (!File.Exists(localFilePath))
        {
            Console.WriteLine($"  [WARN] Image file not found: {localFilePath}");
            return null;
        }

        try
        {
            var guid = Guid.NewGuid().ToString("N");
            var imageKey = $"questions/{categorySlug}/{guid}.webp";
            var thumbKey = $"questions/{categorySlug}/{guid}_thumb.webp";

            await using var fileStream = File.OpenRead(localFilePath);
            using var image = await Image.LoadAsync(fileStream, ct);

            // Resize main image if oversized
            if (image.Width > MaxDimension || image.Height > MaxDimension)
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxDimension, MaxDimension),
                    Mode = ResizeMode.Max
                }));

            using var mainStream = new MemoryStream();
            await image.SaveAsync(mainStream, new WebpEncoder { Quality = 85 }, ct);
            mainStream.Position = 0;

            using var thumbImage = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(ThumbnailSize, ThumbnailSize),
                Mode = ResizeMode.Crop
            }));
            using var thumbStream = new MemoryStream();
            await thumbImage.SaveAsync(thumbStream, new WebpEncoder { Quality = 75 }, ct);
            thumbStream.Position = 0;

            await UploadToS3Async(imageKey, mainStream, "image/webp", ct);
            await UploadToS3Async(thumbKey, thumbStream, "image/webp", ct);

            return (imageKey, thumbKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARN] Image upload failed for '{localFilePath}': {ex.Message}");
            return null;
        }
    }

    private async Task UploadToS3Async(string key, Stream stream, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await s3.PutObjectAsync(request, ct);
    }

    /// <summary>
    /// Ensures the target bucket exists; creates it if not.
    /// Call once before starting any uploads.
    /// </summary>
    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        try
        {
            await s3.GetBucketLocationAsync(bucket, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            await s3.PutBucketAsync(bucket, ct);
            Console.WriteLine($"  Created MinIO bucket: {bucket}");
        }
    }

    /// <summary>
    /// Builds a lookup dictionary: mediaName (e.g. "1") → first matching file path.
    /// Multiple files with same prefix (e.g. 1.08ed0eeb.png, 1.361f1c6f.png) → takes alphabetically first.
    /// </summary>
    public static Dictionary<string, string> BuildImageMap(string imgDirectory)
    {
        if (!Directory.Exists(imgDirectory))
            return [];

        return Directory.GetFiles(imgDirectory, "*.png")
            .GroupBy(f =>
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(f); // "1.08ed0eeb"
                var dotIndex = nameWithoutExt.IndexOf('.');
                return dotIndex >= 0 ? nameWithoutExt[..dotIndex] : nameWithoutExt;
            })
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(f => f).First()); // deterministic: take alphabetically first
    }
}
