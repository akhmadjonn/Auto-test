using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record DeleteHazardLabelCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteHazardLabelCommandValidator : AbstractValidator<DeleteHazardLabelCommand>
{
    public DeleteHazardLabelCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteHazardLabelCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteHazardLabelCommandHandler> logger) : IRequestHandler<DeleteHazardLabelCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteHazardLabelCommand request, CancellationToken ct)
    {
        var label = await db.HazardLabels.FindAsync([request.Id], ct);
        if (label is null)
            return ApiResponse.Fail("NOT_FOUND", "Hazard label not found.");

        var imageKey = label.ImageUrl;

        db.HazardLabels.Remove(label);
        await db.SaveChangesAsync(ct);
        await CreateHazardLabelCommandHandler.InvalidateHazardLabelCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:hazard-labels:{request.Id}", ct);

        if (!string.IsNullOrEmpty(imageKey))
            await storage.DeleteAsync(imageKey, ct);

        logger.LogInformation("Hazard label deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
