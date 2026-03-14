using System.IO.Compression;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

public record BulkImportQuestionsCommand(
    Stream ExcelStream,
    Stream? ZipImagesStream) : IRequest<ApiResponse<BulkImportResultDto>>;

public record BulkImportResultDto(int Imported, int Skipped, int Errors, List<string> ErrorMessages);

public class BulkImportQuestionsCommandValidator : AbstractValidator<BulkImportQuestionsCommand>
{
    public BulkImportQuestionsCommandValidator()
    {
        RuleFor(x => x.ExcelStream).NotNull().WithMessage("Excel file is required");
    }
}

public class BulkImportQuestionsCommandHandler(
    IApplicationDbContext db,
    IQuestionImportService parser,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<BulkImportQuestionsCommandHandler> logger) : IRequestHandler<BulkImportQuestionsCommand, ApiResponse<BulkImportResultDto>>
{
    private const int BatchSize = 100;

    public async Task<ApiResponse<BulkImportResultDto>> Handle(BulkImportQuestionsCommand request, CancellationToken ct)
    {
        var parseResult = await parser.ParseExcelAsync(request.ExcelStream, ct);

        if (parseResult.Questions.Count == 0 && parseResult.Errors.Count > 0)
            return ApiResponse<BulkImportResultDto>.Fail("PARSE_FAILED", "Excel parsing failed with errors.");

        // Extract images from ZIP if provided
        var imageCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (request.ZipImagesStream is not null)
            await ExtractImagesAsync(request.ZipImagesStream, imageCache, ct);

        // Load existing categories
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Slug, c => c, ct);

        // Load existing questions for deduplication (ticket + order)
        var existingKeys = await db.Questions
            .AsNoTracking()
            .Select(q => new { q.TicketNumber, Order = 0 })
            .ToListAsync(ct);

        var existingSet = existingKeys.Select(e => e.TicketNumber).ToHashSet();

        var imported = 0;
        var skipped = 0;
        var errorMessages = new List<string>(parseResult.Errors.Select(e => $"Row {e.Row} [{e.Column}]: {e.Error}"));
        var batch = new List<Question>(BatchSize);

        foreach (var dto in parseResult.Questions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!categories.TryGetValue(dto.CategorySlug, out var category))
                {
                    errorMessages.Add($"Ticket {dto.TicketNumber}: Category '{dto.CategorySlug}' not found. Skipped.");
                    skipped++;
                    continue;
                }

                var difficulty = (Difficulty)dto.Difficulty;
                var licenseCategory = Enum.TryParse<LicenseCategory>(dto.LicenseCategory, true, out var lc) ? lc : LicenseCategory.AB;

                var question = new Question
                {
                    Id = Guid.NewGuid(),
                    CategoryId = category.Id,
                    Text = new LocalizedText(dto.TextUz, dto.TextUzLatin, dto.TextRu),
                    Explanation = new LocalizedText(dto.ExplanationUz, dto.ExplanationUzLatin, dto.ExplanationRu),
                    Difficulty = difficulty,
                    TicketNumber = dto.TicketNumber,
                    LicenseCategory = licenseCategory,
                    IsActive = dto.IsActive,
                    CreatedAt = dateTime.UtcNow,
                    UpdatedAt = dateTime.UtcNow
                };

                // Upload question image
                if (dto.QuestionImageFileName is not null && imageCache.TryGetValue(dto.QuestionImageFileName, out var qImgBytes))
                {
                    using var stream = new MemoryStream(qImgBytes);
                    var processed = await imageProcessor.ProcessImageAsync(stream, dto.QuestionImageFileName, ct);
                    var guid = Guid.NewGuid().ToString();
                    question.ImageUrl = await storage.UploadQuestionImageAsync(processed.ProcessedImage, $"{guid}.webp", category.Slug, ct);
                    question.ThumbnailUrl = await storage.UploadQuestionImageAsync(processed.Thumbnail, $"{guid}_thumb.webp", category.Slug, ct);
                }

                var options = new List<AnswerOption>();
                foreach (var optDto in dto.Options)
                {
                    var option = new AnswerOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question.Id,
                        Text = new LocalizedText(optDto.TextUz, optDto.TextUzLatin, optDto.TextRu),
                        IsCorrect = string.Equals(optDto.TextUz, dto.CorrectAnswer, StringComparison.OrdinalIgnoreCase),
                        CreatedAt = dateTime.UtcNow,
                        UpdatedAt = dateTime.UtcNow
                    };

                    if (optDto.ImageFileName is not null && imageCache.TryGetValue(optDto.ImageFileName, out var optImgBytes))
                    {
                        using var stream = new MemoryStream(optImgBytes);
                        var processed = await imageProcessor.ProcessImageAsync(stream, optDto.ImageFileName, ct);
                        var guid = Guid.NewGuid().ToString();
                        option.ImageUrl = await storage.UploadAnswerOptionImageAsync(processed.ProcessedImage, $"{guid}.webp", category.Slug, ct);
                    }

                    options.Add(option);
                }

                question.AnswerOptions = options;
                batch.Add(question);
                imported++;

                if (batch.Count >= BatchSize)
                    await FlushBatchAsync(batch, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing ticket {Ticket}", dto.TicketNumber);
                errorMessages.Add($"Ticket {dto.TicketNumber}: {ex.Message}");
                skipped++;
            }
        }

        if (batch.Count > 0)
            await FlushBatchAsync(batch, ct);

        if (imported > 0)
            await CreateQuestionCommandHandler.InvalidateQuestionCachesAsync(cache, ct);

        logger.LogInformation("BulkImport: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errorMessages.Count);

        return ApiResponse<BulkImportResultDto>.Ok(new BulkImportResultDto(imported, skipped, parseResult.Errors.Count, errorMessages));
    }

    private async Task FlushBatchAsync(List<Question> batch, CancellationToken ct)
    {
        db.Questions.AddRange(batch);
        await db.SaveChangesAsync(ct);
        batch.Clear();
    }

    private static async Task ExtractImagesAsync(Stream zipStream, Dictionary<string, byte[]> cache, CancellationToken ct)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in zip.Entries)
        {
            if (entry.Length == 0 || string.IsNullOrEmpty(entry.Name))
                continue;
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms, ct);
            cache[entry.Name] = ms.ToArray();
        }
    }
}
