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

public record UpdateQuestionCommand(
    Guid QuestionId,
    string TextUz,
    string TextUzLatin,
    string TextRu,
    string ExplanationUz,
    string ExplanationUzLatin,
    string ExplanationRu,
    Difficulty Difficulty,
    int TicketNumber,
    LicenseCategory LicenseCategory,
    bool IsActive,
    bool RemoveQuestionImage,
    Stream? NewQuestionImage,
    string? NewQuestionImageFileName,
    List<UpdateAnswerOptionDto> AnswerOptions) : IRequest<ApiResponse>;

public record UpdateAnswerOptionDto(
    Guid? ExistingId,
    string TextUz,
    string TextUzLatin,
    string TextRu,
    bool IsCorrect,
    bool RemoveImage,
    Stream? NewImage,
    string? NewImageFileName);

public class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.TextUz).NotEmpty();
        RuleFor(x => x.TextRu).NotEmpty();
        RuleFor(x => x.TicketNumber).GreaterThan(0);
        RuleFor(x => x.AnswerOptions).NotEmpty()
            .Must(opts => opts.Count >= 2 && opts.Count <= 6)
            .Must(opts => opts.Count(o => o.IsCorrect) == 1)
            .WithMessage("Exactly one answer must be correct");
    }
}

public class UpdateQuestionCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<UpdateQuestionCommandHandler> logger) : IRequestHandler<UpdateQuestionCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions
            .Include(q => q.Category)
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);

        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        question.Text = new LocalizedText(request.TextUz, request.TextUzLatin, request.TextRu);
        question.Explanation = new LocalizedText(request.ExplanationUz, request.ExplanationUzLatin, request.ExplanationRu);
        question.Difficulty = request.Difficulty;
        question.TicketNumber = request.TicketNumber;
        question.LicenseCategory = request.LicenseCategory;
        question.IsActive = request.IsActive;
        question.UpdatedAt = dateTime.UtcNow;

        // Handle question image diff
        if (request.RemoveQuestionImage && question.ImageUrl is not null)
        {
            var toDelete = new List<string>();
            if (question.ImageUrl is not null) toDelete.Add(question.ImageUrl);
            if (question.ThumbnailUrl is not null) toDelete.Add(question.ThumbnailUrl);
            await storage.DeleteManyAsync(toDelete, ct);
            question.ImageUrl = null;
            question.ThumbnailUrl = null;
        }

        if (request.NewQuestionImage is not null && request.NewQuestionImageFileName is not null)
        {
            // Delete old if exists
            if (question.ImageUrl is not null)
                await storage.DeleteManyAsync([question.ImageUrl, question.ThumbnailUrl ?? ""], ct);

            var processed = await imageProcessor.ProcessImageAsync(request.NewQuestionImage, request.NewQuestionImageFileName, ct);
            var guid = Guid.NewGuid().ToString();
            question.ImageUrl = await storage.UploadQuestionImageAsync(processed.ProcessedImage, $"{guid}.webp", question.Category.Slug, ct);
            question.ThumbnailUrl = await storage.UploadQuestionImageAsync(processed.Thumbnail, $"{guid}_thumb.webp", question.Category.Slug, ct);
        }

        // Rebuild answer options
        var oldOptions = question.AnswerOptions.ToList();
        var existingIds = request.AnswerOptions.Where(o => o.ExistingId.HasValue).Select(o => o.ExistingId!.Value).ToHashSet();
        var toRemove = oldOptions.Where(o => !existingIds.Contains(o.Id)).ToList();

        foreach (var opt in toRemove)
        {
            if (opt.ImageUrl is not null)
                await storage.DeleteAsync(opt.ImageUrl, ct);
            db.AnswerOptions.Remove(opt);
        }

        foreach (var dto in request.AnswerOptions)
        {
            AnswerOption option;
            if (dto.ExistingId.HasValue)
            {
                option = oldOptions.First(o => o.Id == dto.ExistingId.Value);
                option.Text = new LocalizedText(dto.TextUz, dto.TextUzLatin, dto.TextRu);
                option.IsCorrect = dto.IsCorrect;
                option.UpdatedAt = dateTime.UtcNow;

                if (dto.RemoveImage && option.ImageUrl is not null)
                {
                    await storage.DeleteAsync(option.ImageUrl, ct);
                    option.ImageUrl = null;
                }
            }
            else
            {
                option = new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Text = new LocalizedText(dto.TextUz, dto.TextUzLatin, dto.TextRu),
                    IsCorrect = dto.IsCorrect,
                    CreatedAt = dateTime.UtcNow,
                    UpdatedAt = dateTime.UtcNow
                };
                db.AnswerOptions.Add(option);
            }

            if (dto.NewImage is not null && dto.NewImageFileName is not null)
            {
                if (option.ImageUrl is not null)
                    await storage.DeleteAsync(option.ImageUrl, ct);
                var processed = await imageProcessor.ProcessImageAsync(dto.NewImage, dto.NewImageFileName, ct);
                var guid = Guid.NewGuid().ToString();
                option.ImageUrl = await storage.UploadAnswerOptionImageAsync(processed.ProcessedImage, $"{guid}.webp", question.Category.Slug, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated question {Id}", request.QuestionId);
        return ApiResponse.Ok();
    }
}
