using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record CreateRoadSignCommand(
    Guid CategoryId,
    string SignCode,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateRoadSignCommandValidator : AbstractValidator<CreateRoadSignCommand>
{
    public CreateRoadSignCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.SignCode).NotEmpty().MaximumLength(20)
            .Matches(@"^[\d.]+$").WithMessage("Sign code must contain only digits and dots (e.g., 1.1, 2.3.1).");
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateRoadSignCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateRoadSignCommandHandler> logger) : IRequestHandler<CreateRoadSignCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateRoadSignCommand request, CancellationToken ct)
    {
        var categoryExists = await db.RoadSignCategories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
            return ApiResponse<Guid>.Fail("CATEGORY_NOT_FOUND", "Road sign category not found.");

        var codeExists = await db.RoadSigns.AnyAsync(s => s.SignCode == request.SignCode, ct);
        if (codeExists)
            return ApiResponse<Guid>.Fail("CODE_DUPLICATE", $"Road sign with code '{request.SignCode}' already exists.");

        var sign = new RoadSign
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            SignCode = request.SignCode,
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Description = request.DescriptionUzLatin is not null
                ? new LocalizedText(request.DescriptionUz ?? "", request.DescriptionUzLatin, request.DescriptionRu ?? "")
                : null,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = dateTime.UtcNow
        };

        db.RoadSigns.Add(sign);
        await db.SaveChangesAsync(ct);
        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-signs:category:{request.CategoryId}", ct);

        logger.LogInformation("Road sign created: {Id} code={Code}", sign.Id, request.SignCode);
        return ApiResponse<Guid>.Ok(sign.Id);
    }
}
