using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record DeleteRoadSignCategoryCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteRoadSignCategoryCommandValidator : AbstractValidator<DeleteRoadSignCategoryCommand>
{
    public DeleteRoadSignCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteRoadSignCategoryCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteRoadSignCategoryCommandHandler> logger) : IRequestHandler<DeleteRoadSignCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteRoadSignCategoryCommand request, CancellationToken ct)
    {
        var category = await db.RoadSignCategories
            .Include(c => c.Signs)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (category is null)
            return ApiResponse.Fail("NOT_FOUND", "Road sign category not found.");

        if (category.Signs.Count > 0)
            return ApiResponse.Fail("HAS_SIGNS", $"Cannot delete category with {category.Signs.Count} signs. Remove all signs first.");

        if (!string.IsNullOrEmpty(category.IconUrl))
            await storage.DeleteAsync(category.IconUrl, ct);

        db.RoadSignCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);

        logger.LogInformation("Road sign category deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
