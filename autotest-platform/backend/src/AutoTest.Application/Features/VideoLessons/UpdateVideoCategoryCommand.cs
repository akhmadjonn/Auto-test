using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UpdateVideoCategoryCommand(
    Guid Id,
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateVideoCategoryCommandValidator : AbstractValidator<UpdateVideoCategoryCommand>
{
    public UpdateVideoCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateVideoCategoryCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UpdateVideoCategoryCommandHandler> logger) : IRequestHandler<UpdateVideoCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateVideoCategoryCommand request, CancellationToken ct)
    {
        var category = await db.VideoCategories.FindAsync([request.Id], ct);
        if (category is null)
            return ApiResponse.Fail("NOT_FOUND", "Video category not found.");

        category.Name = request.Name;
        category.Description = request.Description;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        logger.LogInformation("Video category updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
