using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.ColorVision;

public record UpdateColorVisionPlateCommand(
    Guid Id,
    int PlateNumber,
    string ExpectedAnswer,
    string? AlternateAnswer,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateColorVisionPlateCommandValidator : AbstractValidator<UpdateColorVisionPlateCommand>
{
    public UpdateColorVisionPlateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PlateNumber).GreaterThan(0);
        RuleFor(x => x.ExpectedAnswer).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AlternateAnswer).MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateColorVisionPlateCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateColorVisionPlateCommandHandler> logger) : IRequestHandler<UpdateColorVisionPlateCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateColorVisionPlateCommand request, CancellationToken ct)
    {
        var plate = await db.ColorVisionPlates.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (plate is null)
            return ApiResponse.Fail("NOT_FOUND", "Color vision plate not found.");

        plate.PlateNumber = request.PlateNumber;
        plate.ExpectedAnswer = request.ExpectedAnswer;
        plate.AlternateAnswer = request.AlternateAnswer;
        plate.SortOrder = request.SortOrder;
        plate.IsActive = request.IsActive;
        plate.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateColorVisionPlateCommandHandler.InvalidateColorVisionCacheAsync(cache, ct);

        logger.LogInformation("Color vision plate updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
