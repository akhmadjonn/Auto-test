using Amazon.S3;

namespace AutoTest.Infrastructure.Services;

// Holds a second IAmazonS3 whose ServiceURL points at the PUBLIC MinIO host
// (e.g. https://cdn.avtolider.uz). Used only for generating presigned URLs
// that browsers can open. The default IAmazonS3 stays on the internal Docker
// hostname (e.g. http://minio:9000) for server-side uploads + bucket ops, so
// container traffic never has to hairpin through the host's public IP.
public sealed class MinioPresignClient(IAmazonS3 client)
{
    public IAmazonS3 Client { get; } = client;
}
