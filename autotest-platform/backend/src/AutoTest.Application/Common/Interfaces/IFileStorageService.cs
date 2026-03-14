namespace AutoTest.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadQuestionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default);
    Task<string> UploadAnswerOptionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken ct = default);
    Task<string> GetThumbnailUrlAsync(string objectKey, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetPresignedUrlsBatchAsync(IEnumerable<string> objectKeys, CancellationToken ct = default);
    Task DeleteAsync(string objectKey, CancellationToken ct = default);
    Task DeleteManyAsync(IEnumerable<string> objectKeys, CancellationToken ct = default);
}
