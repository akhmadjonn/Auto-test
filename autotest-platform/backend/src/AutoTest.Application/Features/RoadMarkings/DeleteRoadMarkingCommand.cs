using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record DeleteRoadMarkingCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteRoadMarkingCommandValidator : AbstractValidator<DeleteRoadMarkingCommand>
{
    public DeleteRoadMarkingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteRoadMarkingCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteRoadMarkingCommandHandler> logger) : IRequestHandler<DeleteRoadMarkingCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteRoadMarkingCommand request, CancellationToken ct)
    {
        var marking = await db.RoadMarkings.FindAsync([request.Id], ct);
        if (marking is null)
            return ApiResponse.Fail("NOT_FOUND", "Road marking not found.");

        var imageKeys = new List<string>();
        if (!string.IsNullOrEmpty(marking.ImageUrl))
            imageKeys.Add(marking.ImageUrl);
        if (!string.IsNullOrEmpty(marking.ThumbnailUrl))
            imageKeys.Add(marking.ThumbnailUrl);

        db.RoadMarkings.Remove(marking);
        await db.SaveChangesAsync(ct);
        await CreateRoadMarkingCommandHandler.InvalidateRoadMarkingCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-markings:{request.Id}", ct);

        if (imageKeys.Count > 0)
            await storage.DeleteManyAsync(imageKeys, ct);

        logger.LogInformation("Road marking deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
