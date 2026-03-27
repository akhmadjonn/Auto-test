using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace AutoTest.Application.Features.ColorVision;

public record SaveColorVisionResultCommand(List<PlateAnswerDto> Answers) : IRequest<ApiResponse<ColorVisionResultDto>>;

public record PlateAnswerDto(string PlateId, string Answer);

public record ColorVisionResultDto(bool Passed, int Score, int Total);

public class SaveColorVisionResultCommandValidator : AbstractValidator<SaveColorVisionResultCommand>
{
    public SaveColorVisionResultCommandValidator()
    {
        RuleFor(x => x.Answers)
            .NotEmpty().WithMessage("Answers are required.");

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.PlateId).NotEmpty().WithMessage("PlateId is required.");
            answer.RuleFor(a => a.Answer).NotEmpty().WithMessage("Answer is required.");
        });
    }
}

public class SaveColorVisionResultCommandHandler(
    ICurrentUser currentUser,
    ICacheService cache,
    IDateTimeProvider dateTime) : IRequestHandler<SaveColorVisionResultCommand, ApiResponse<ColorVisionResultDto>>
{
    private const int PassingThreshold = 10;
    private const int TotalPlates = 12;

    public async Task<ApiResponse<ColorVisionResultDto>> Handle(SaveColorVisionResultCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ColorVisionResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var plateMap = ColorVisionPlates.All.ToDictionary(p => p.PlateId);
        var score = 0;

        foreach (var answer in request.Answers)
        {
            if (!plateMap.TryGetValue(answer.PlateId, out var plate))
                continue;

            var trimmed = answer.Answer.Trim();
            if (string.Equals(trimmed, plate.ExpectedAnswer, StringComparison.OrdinalIgnoreCase)
                || (plate.AlternateAnswer is not null && string.Equals(trimmed, plate.AlternateAnswer, StringComparison.OrdinalIgnoreCase)))
                score++;
        }

        var passed = score >= PassingThreshold;
        var result = new ColorVisionResultDto(passed, score, TotalPlates);

        var cacheKey = $"avtolider:color-vision:result:{currentUser.UserId}";
        var cacheValue = new ColorVisionCacheEntry(passed, score, TotalPlates, dateTime.UtcNow);
        await cache.SetAsync(cacheKey, cacheValue, TimeSpan.FromDays(30), ct);

        return ApiResponse<ColorVisionResultDto>.Ok(result);
    }
}

internal record ColorVisionCacheEntry(bool Passed, int Score, int Total, DateTimeOffset TestDate);
