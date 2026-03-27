using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Practice;

public record GetHardQuestionsQuery(
    int Limit = 20,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<List<HardQuestionDto>>>;

public record HardQuestionDto(
    Guid Id,
    LocalizedText Text,
    string? ImageUrl,
    Difficulty Difficulty,
    double SuccessRate,
    List<PracticeAnswerOptionDto> AnswerOptions);

public class GetHardQuestionsQueryValidator : AbstractValidator<GetHardQuestionsQuery>
{
    public GetHardQuestionsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class GetHardQuestionsQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cacheService)
    : IRequestHandler<GetHardQuestionsQuery, ApiResponse<List<HardQuestionDto>>>
{
    public async Task<ApiResponse<List<HardQuestionDto>>> Handle(
        GetHardQuestionsQuery request, CancellationToken ct)
    {
        var cacheKey = "avtolider:practice:hard";
        var cached = await cacheService.GetAsync<List<HardQuestionDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<HardQuestionDto>>.Ok(cached.Take(request.Limit).ToList());

        var questions = await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.Status == QuestionStatus.Active
                && (q.Difficulty == Difficulty.Hard
                    || (q.TotalAttempts > 10
                        && (double)q.CorrectCount / q.TotalAttempts < 0.4)))
            .OrderBy(q => q.TotalAttempts > 0
                ? (double)q.CorrectCount / q.TotalAttempts
                : 0.5)
            .ThenBy(_ => EF.Functions.Random())
            .Take(50) // cache more than needed
            .ToListAsync(ct);

        // Batch presigned URLs
        var imageKeys = new List<string>();
        foreach (var q in questions)
        {
            if (q.ImageUrl is not null) imageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) imageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(imageKeys, ct);

        var dtos = questions.Select(q =>
        {
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;
            var options = q.AnswerOptions
                .OrderBy(_ => Random.Shared.Next())
                .Select(a => new PracticeAnswerOptionDto(
                    a.Id, a.Text,
                    a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null))
                .ToList();

            return new HardQuestionDto(
                q.Id, q.Text, imgUrl, q.Difficulty, q.SuccessRate, options);
        }).ToList();

        await cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(15), ct);

        return ApiResponse<List<HardQuestionDto>>.Ok(dtos.Take(request.Limit).ToList());
    }
}
