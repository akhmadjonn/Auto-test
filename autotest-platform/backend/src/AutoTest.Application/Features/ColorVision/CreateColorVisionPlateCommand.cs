using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.ColorVision;

public record CreateColorVisionPlateCommand(
    int PlateNumber,
    string ExpectedAnswer,
    string? AlternateAnswer,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse<string>>;

public class CreateColorVisionPlateCommandValidator : AbstractValidator<CreateColorVisionPlateCommand>
{
    public CreateColorVisionPlateCommandValidator()
    {
        RuleFor(x => x.PlateNumber).GreaterThan(0);
        RuleFor(x => x.ExpectedAnswer).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AlternateAnswer).MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateColorVisionPlateCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateColorVisionPlateCommandHandler> logger) : IRequestHandler<CreateColorVisionPlateCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(CreateColorVisionPlateCommand request, CancellationToken ct)
    {
        var plate = new ColorVisionPlate
        {
            Id = Guid.NewGuid(),
            PlateNumber = request.PlateNumber,
            ExpectedAnswer = request.ExpectedAnswer,
            AlternateAnswer = request.AlternateAnswer,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = dateTime.UtcNow
        };

        db.ColorVisionPlates.Add(plate);
        await db.SaveChangesAsync(ct);
        await InvalidateColorVisionCacheAsync(cache, ct);

        logger.LogInformation("Color vision plate created: {Id} number={Number}", plate.Id, request.PlateNumber);
        return ApiResponse<string>.Ok(plate.Id.ToString());
    }

    internal static async Task InvalidateColorVisionCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:color-vision:plates", ct);
    }
}
