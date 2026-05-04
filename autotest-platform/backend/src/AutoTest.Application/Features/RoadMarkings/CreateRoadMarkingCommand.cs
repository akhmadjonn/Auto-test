using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record CreateRoadMarkingCommand(
    string MarkingCode,
    RoadMarkingType MarkingType,
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateRoadMarkingCommandValidator : AbstractValidator<CreateRoadMarkingCommand>
{
    public CreateRoadMarkingCommandValidator()
    {
        RuleFor(x => x.MarkingCode).NotEmpty().MaximumLength(20)
            .Matches(@"^[\d.]+$").WithMessage("Marking code must contain only digits and dots.");
        RuleFor(x => x.MarkingType).IsInEnum();
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateRoadMarkingCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateRoadMarkingCommandHandler> logger) : IRequestHandler<CreateRoadMarkingCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateRoadMarkingCommand request, CancellationToken ct)
    {
        var codeExists = await db.RoadMarkings.AnyAsync(m => m.MarkingCode == request.MarkingCode, ct);
        if (codeExists)
            return ApiResponse<Guid>.Fail("CODE_DUPLICATE", $"Road marking with code '{request.MarkingCode}' already exists.");

        var marking = new RoadMarking
        {
            Id = Guid.NewGuid(),
            MarkingCode = request.MarkingCode,
            MarkingType = request.MarkingType,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = dateTime.UtcNow
        };

        db.RoadMarkings.Add(marking);
        await db.SaveChangesAsync(ct);
        await InvalidateRoadMarkingCacheAsync(cache, ct);

        logger.LogInformation("Road marking created: {Id} code={Code}", marking.Id, request.MarkingCode);
        return ApiResponse<Guid>.Ok(marking.Id);
    }

    internal static async Task InvalidateRoadMarkingCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:road-markings:all", ct);
        await cache.RemoveAsync("avtolider:road-markings:horizontal", ct);
        await cache.RemoveAsync("avtolider:road-markings:vertical", ct);
    }
}
