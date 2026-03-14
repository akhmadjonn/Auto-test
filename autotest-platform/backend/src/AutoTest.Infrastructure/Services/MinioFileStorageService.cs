using Amazon.S3;
using Amazon.S3.Model;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class MinioFileStorageService(
    IAmazonS3 s3,
    IConfiguration configuration,
    ILogger<MinioFileStorageService> logger) : IFileStorageService
{
    private string Bucket => configuration["MinioSettings:BucketName"] ?? "autotest-images";
    private Protocol S3Protocol => bool.TryParse(configuration["MinioSettings:UseSSL"], out var ssl) && ssl
        ? Protocol.HTTPS
        : Protocol.HTTP;

    public async Task<string> UploadQuestionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default)
    {
        var key = $"questions/{category}/{fileName}";
        await UploadAsync(stream, key, "image/webp", ct);
        return key;
    }

    public async Task<string> UploadAnswerOptionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default)
    {
        var key = $"questions/{category}/answers/{fileName}";
        await UploadAsync(stream, key, "image/webp", ct);
        return key;
    }

    public async Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddHours(1),
            Protocol = S3Protocol
        };
        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<string> GetThumbnailUrlAsync(string objectKey, CancellationToken ct = default)
    {
        var thumbKey = GetThumbnailKey(objectKey);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = thumbKey,
            Expires = DateTime.UtcNow.AddHours(1),
            Protocol = S3Protocol
        };
        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<Dictionary<string, string>> GetPresignedUrlsBatchAsync(IEnumerable<string> objectKeys, CancellationToken ct = default)
    {
        var keys = objectKeys.Where(k => k is not null).Distinct().ToList();
        if (keys.Count == 0)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>(keys.Count);
        var tasks = keys.Select(async key =>
        {
            var url = await GetPresignedUrlAsync(key, ct);
            return (key, url);
        });

        foreach (var (key, url) in await Task.WhenAll(tasks))
            result[key] = url;

        return result;
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await s3.DeleteObjectAsync(Bucket, objectKey, ct);
        // Also delete thumbnail
        var thumbKey = GetThumbnailKey(objectKey);
        await s3.DeleteObjectAsync(Bucket, thumbKey, ct);
    }

    public async Task DeleteManyAsync(IEnumerable<string> objectKeys, CancellationToken ct = default)
    {
        var keys = objectKeys.ToList();
        if (keys.Count == 0)
            return;

        var allKeys = keys
            .SelectMany(k => new[] { k, GetThumbnailKey(k) })
            .Select(k => new KeyVersion { Key = k })
            .ToList();

        var request = new DeleteObjectsRequest
        {
            BucketName = Bucket,
            Objects = allKeys
        };
        await s3.DeleteObjectsAsync(request, ct);
    }

    public async Task EnsureBucketExistsAsync(CancellationToken ct = default)
    {
        try
        {
            await s3.GetBucketLocationAsync(Bucket, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            await s3.PutBucketAsync(Bucket, ct);
            logger.LogInformation("Created MinIO bucket: {Bucket}", Bucket);
        }
    }

    private async Task UploadAsync(Stream stream, string key, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await s3.PutObjectAsync(request, ct);
    }

    private static string GetThumbnailKey(string objectKey)
    {
        var ext = Path.GetExtension(objectKey);
        var name = Path.GetFileNameWithoutExtension(objectKey);
        var dir = Path.GetDirectoryName(objectKey)?.Replace('\\', '/') ?? "";
        return string.IsNullOrEmpty(dir)
            ? $"{name}_thumb{ext}"
            : $"{dir}/{name}_thumb{ext}";
    }
}
