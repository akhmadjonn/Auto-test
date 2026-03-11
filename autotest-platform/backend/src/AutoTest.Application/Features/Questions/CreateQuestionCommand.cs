using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

public record CreateQuestionCommand(
    Guid CategoryId,
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
    Stream? QuestionImage,
    string? QuestionImageFileName,
    List<CreateAnswerOptionDto> AnswerOptions) : IRequest<ApiResponse<Guid>>;

public record CreateAnswerOptionDto(
    string TextUz,
    string TextUzLatin,
    string TextRu,
    bool IsCorrect,
    Stream? Image,
    string? ImageFileName);

public class CreateQuestionCommandValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.TextUz).NotEmpty();
        RuleFor(x => x.TextRu).NotEmpty();
        RuleFor(x => x.TicketNumber).GreaterThan(0);
        RuleFor(x => x.AnswerOptions).NotEmpty()
            .Must(opts => opts.Count >= 2 && opts.Count <= 6)
            .WithMessage("Must have 2-6 answer options")
            .Must(opts => opts.Count(o => o.IsCorrect) == 1)
            .WithMessage("Exactly one answer must be correct");
    }
}

public class CreateQuestionCommandHandler(
    IApplicationDbContext db,
    IImageProcessingService imageProcessor,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<CreateQuestionCommandHandler> logger) : IRequestHandler<CreateQuestionCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateQuestionCommand request, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([request.CategoryId], ct);
        if (category is null)
            return ApiResponse<Guid>.Fail("CATEGORY_NOT_FOUND", "Category not found.");

        var question = new Question
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Text = new LocalizedText(request.TextUz, request.TextUzLatin, request.TextRu),
            Explanation = new LocalizedText(request.ExplanationUz, request.ExplanationUzLatin, request.ExplanationRu),
            Difficulty = request.Difficulty,
            TicketNumber = request.TicketNumber,
            LicenseCategory = request.LicenseCategory,
            IsActive = request.IsActive,
            CreatedAt = dateTime.UtcNow,
            UpdatedAt = dateTime.UtcNow
        };

        if (request.QuestionImage is not null && request.QuestionImageFileName is not null)
        {
            var processed = await imageProcessor.ProcessImageAsync(request.QuestionImage, request.QuestionImageFileName, ct);
            var guid = Guid.NewGuid().ToString();
            question.ImageUrl = await storage.UploadQuestionImageAsync(processed.ProcessedImage, $"{guid}.webp", category.Slug, ct);
            question.ThumbnailUrl = await storage.UploadQuestionImageAsync(processed.Thumbnail, $"{guid}_thumb.webp", category.Slug, ct);
        }

        var options = new List<AnswerOption>();
        foreach (var opt in request.AnswerOptions)
        {
            var option = new AnswerOption
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Text = new LocalizedText(opt.TextUz, opt.TextUzLatin, opt.TextRu),
                IsCorrect = opt.IsCorrect,
                CreatedAt = dateTime.UtcNow,
                UpdatedAt = dateTime.UtcNow
            };

            if (opt.Image is not null && opt.ImageFileName is not null)
            {
                var processed = await imageProcessor.ProcessImageAsync(opt.Image, opt.ImageFileName, ct);
                var guid = Guid.NewGuid().ToString();
                option.ImageUrl = await storage.UploadAnswerOptionImageAsync(processed.ProcessedImage, $"{guid}.webp", category.Slug, ct);
            }

            options.Add(option);
        }

        question.AnswerOptions = options;
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created question {QuestionId} in category {CategoryId}", question.Id, request.CategoryId);
        return ApiResponse<Guid>.Ok(question.Id);
    }
}
