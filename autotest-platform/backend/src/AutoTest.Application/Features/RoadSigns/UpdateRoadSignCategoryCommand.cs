using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record UpdateRoadSignCategoryCommand(
    Guid Id,
    string Slug,
    string Code,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateRoadSignCategoryCommandValidator : AbstractValidator<UpdateRoadSignCategoryCommand>
{
    public UpdateRoadSignCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DescriptionUz).NotEmpty();
        RuleFor(x => x.DescriptionUzLatin).NotEmpty();
        RuleFor(x => x.DescriptionRu).NotEmpty();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRoadSignCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateRoadSignCategoryCommandHandler> logger) : IRequestHandler<UpdateRoadSignCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateRoadSignCategoryCommand request, CancellationToken ct)
    {
        var category = await db.RoadSignCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (category is null)
            return ApiResponse.Fail("NOT_FOUND", "Road sign category not found.");

        var slugExists = await db.RoadSignCategories.AnyAsync(c => c.Slug == request.Slug && c.Id != request.Id, ct);
        if (slugExists)
            return ApiResponse.Fail("SLUG_DUPLICATE", $"Road sign category with slug '{request.Slug}' already exists.");

        var codeExists = await db.RoadSignCategories.AnyAsync(c => c.Code == request.Code && c.Id != request.Id, ct);
        if (codeExists)
            return ApiResponse.Fail("CODE_DUPLICATE", $"Road sign category with code '{request.Code}' already exists.");

        category.Slug = request.Slug;
        category.Code = request.Code;
        category.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        category.Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);

        logger.LogInformation("Road sign category updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
