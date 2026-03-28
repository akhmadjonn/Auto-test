using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.ColorVision;

public record DeleteColorVisionPlateCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteColorVisionPlateCommandValidator : AbstractValidator<DeleteColorVisionPlateCommand>
{
    public DeleteColorVisionPlateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteColorVisionPlateCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteColorVisionPlateCommandHandler> logger) : IRequestHandler<DeleteColorVisionPlateCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteColorVisionPlateCommand request, CancellationToken ct)
    {
        var plate = await db.ColorVisionPlates.FindAsync([request.Id], ct);
        if (plate is null)
            return ApiResponse.Fail("NOT_FOUND", "Color vision plate not found.");

        var imageKey = plate.ImageUrl;

        db.ColorVisionPlates.Remove(plate);
        await db.SaveChangesAsync(ct);
        await CreateColorVisionPlateCommandHandler.InvalidateColorVisionCacheAsync(cache, ct);

        if (!string.IsNullOrEmpty(imageKey))
            await storage.DeleteAsync(imageKey, ct);

        logger.LogInformation("Color vision plate deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
