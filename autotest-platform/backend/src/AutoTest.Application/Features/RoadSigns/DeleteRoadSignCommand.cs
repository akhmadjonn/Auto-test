using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record DeleteRoadSignCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteRoadSignCommandValidator : AbstractValidator<DeleteRoadSignCommand>
{
    public DeleteRoadSignCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteRoadSignCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteRoadSignCommandHandler> logger) : IRequestHandler<DeleteRoadSignCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteRoadSignCommand request, CancellationToken ct)
    {
        var sign = await db.RoadSigns.FindAsync([request.Id], ct);
        if (sign is null)
            return ApiResponse.Fail("NOT_FOUND", "Road sign not found.");

        var imageKeys = new List<string>();
        if (!string.IsNullOrEmpty(sign.ImageUrl))
            imageKeys.Add(sign.ImageUrl);
        if (!string.IsNullOrEmpty(sign.ThumbnailUrl))
            imageKeys.Add(sign.ThumbnailUrl);

        db.RoadSigns.Remove(sign);
        await db.SaveChangesAsync(ct);
        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-signs:category:{sign.CategoryId}", ct);
        await cache.RemoveAsync($"avtolider:road-signs:{request.Id}", ct);

        if (imageKeys.Count > 0)
            await storage.DeleteManyAsync(imageKeys, ct);

        logger.LogInformation("Road sign deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
