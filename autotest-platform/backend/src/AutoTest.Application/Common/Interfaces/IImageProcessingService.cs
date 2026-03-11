namespace AutoTest.Application.Common.Interfaces;

public interface IImageProcessingService
{
    Task<ImageProcessingResult> ProcessImageAsync(Stream imageStream, string fileName, CancellationToken ct = default);
}

public record ImageProcessingResult(Stream ProcessedImage, Stream Thumbnail, string ContentType);
