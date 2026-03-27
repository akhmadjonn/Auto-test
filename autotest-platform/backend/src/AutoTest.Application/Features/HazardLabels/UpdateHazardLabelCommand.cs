using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record UpdateHazardLabelCommand(
    Guid Id,
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu,
    string HazardClass,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateHazardLabelCommandValidator : AbstractValidator<UpdateHazardLabelCommand>
{
    public UpdateHazardLabelCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DescriptionUz).NotEmpty();
        RuleFor(x => x.DescriptionUzLatin).NotEmpty();
        RuleFor(x => x.DescriptionRu).NotEmpty();
        RuleFor(x => x.HazardClass).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateHazardLabelCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateHazardLabelCommandHandler> logger) : IRequestHandler<UpdateHazardLabelCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateHazardLabelCommand request, CancellationToken ct)
    {
        var label = await db.HazardLabels.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (label is null)
            return ApiResponse.Fail("NOT_FOUND", "Hazard label not found.");

        var slugExists = await db.HazardLabels.AnyAsync(l => l.Slug == request.Slug && l.Id != request.Id, ct);
        if (slugExists)
            return ApiResponse.Fail("SLUG_DUPLICATE", $"Hazard label with slug '{request.Slug}' already exists.");

        label.Slug = request.Slug;
        label.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        label.Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);
        label.HazardClass = request.HazardClass;
        label.SortOrder = request.SortOrder;
        label.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateHazardLabelCommandHandler.InvalidateHazardLabelCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:hazard-labels:{request.Id}", ct);

        logger.LogInformation("Hazard label updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
