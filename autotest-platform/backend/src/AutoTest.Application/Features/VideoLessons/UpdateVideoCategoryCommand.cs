using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record UpdateVideoCategoryCommand(
    Guid Id,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateVideoCategoryCommandValidator : AbstractValidator<UpdateVideoCategoryCommand>
{
    public UpdateVideoCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(500);
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

        var description = request.DescriptionUz is not null && request.DescriptionUzLatin is not null && request.DescriptionRu is not null
            ? new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu)
            : null;

        category.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        category.Description = description;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateVideoCategoryCommandHandler.InvalidateVideoCategoryCacheAsync(cache, ct);

        logger.LogInformation("Video category updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
