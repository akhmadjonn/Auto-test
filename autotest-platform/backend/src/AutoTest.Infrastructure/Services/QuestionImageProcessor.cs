using AutoTest.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AutoTest.Infrastructure.Services;

public class QuestionImageProcessor : IImageProcessingService
{
    private const int MaxDimension = 1200;
    private const int ThumbnailSize = 200;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly byte[][] AllowedMagicBytes =
    [
        [0xFF, 0xD8, 0xFF],         // JPEG
        [0x89, 0x50, 0x4E, 0x47],   // PNG
        [0x47, 0x49, 0x46],         // GIF
        [0x52, 0x49, 0x46, 0x46],   // WebP (RIFF)
    ];

    public async Task<ImageProcessingResult> ProcessImageAsync(Stream imageStream, string fileName, CancellationToken ct = default)
    {
        if (imageStream.CanSeek && imageStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"Image file too large. Maximum size is {MaxFileSizeBytes / 1024 / 1024}MB.");

        await ValidateMagicBytesAsync(imageStream, ct);
        imageStream.Position = 0;

        using var image = await Image.LoadAsync(imageStream, ct);

        // Resize if oversized
        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxDimension, MaxDimension),
                Mode = ResizeMode.Max
            }));
        }

        // Encode main image to WebP
        var processedStream = new MemoryStream();
        await image.SaveAsync(processedStream, new WebpEncoder { Quality = 85 }, ct);
        processedStream.Position = 0;

        // Generate 200x200 thumbnail
        var thumbStream = new MemoryStream();
        using var thumb = image.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(ThumbnailSize, ThumbnailSize),
            Mode = ResizeMode.Crop
        }));
        await thumb.SaveAsync(thumbStream, new WebpEncoder { Quality = 75 }, ct);
        thumbStream.Position = 0;

        return new ImageProcessingResult(processedStream, thumbStream, "image/webp");
    }

    private static async Task ValidateMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(0, 8), ct);

        var valid = AllowedMagicBytes.Any(magic =>
            read >= magic.Length && header.Take(magic.Length).SequenceEqual(magic));

        if (!valid)
            throw new InvalidOperationException("Unsupported image format. Only JPEG, PNG, GIF, and WebP are allowed.");
    }
}
