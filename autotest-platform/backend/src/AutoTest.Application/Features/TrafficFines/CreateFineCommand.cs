using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record CreateFineCommand(
    string ArticleNumber,
    LocalizedText ViolationDescription,
    LocalizedText? AdditionalNotes,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateFineCommandValidator : AbstractValidator<CreateFineCommand>
{
    public CreateFineCommandValidator()
    {
        RuleFor(x => x.ArticleNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ViolationDescription).NotNull();
        RuleFor(x => x.ViolationDescription.Uz).NotEmpty().When(x => x.ViolationDescription is not null);
        RuleFor(x => x.ViolationDescription.UzLatin).NotEmpty().When(x => x.ViolationDescription is not null);
        RuleFor(x => x.ViolationDescription.Ru).NotEmpty().When(x => x.ViolationDescription is not null);
        RuleFor(x => x.PenaltyAmountTiyins).GreaterThan(0);
        RuleFor(x => x.PenaltyMaxTiyins)
            .GreaterThanOrEqualTo(x => x.PenaltyAmountTiyins)
            .When(x => x.PenaltyMaxTiyins.HasValue)
            .WithMessage("Max penalty must be >= min penalty");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateFineCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<CreateFineCommandHandler> logger) : IRequestHandler<CreateFineCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateFineCommand request, CancellationToken ct)
    {
        var fine = new TrafficFine
        {
            Id = Guid.NewGuid(),
            ArticleNumber = request.ArticleNumber,
            ViolationDescription = request.ViolationDescription,
            AdditionalNotes = request.AdditionalNotes,
            PenaltyAmountTiyins = request.PenaltyAmountTiyins,
            PenaltyMaxTiyins = request.PenaltyMaxTiyins,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = dateTime.UtcNow,
            UpdatedAt = dateTime.UtcNow
        };

        db.TrafficFines.Add(fine);
        await db.SaveChangesAsync(ct);
        await InvalidateFineCachesAsync(cache, ct);

        logger.LogInformation("Created traffic fine {FineId} article {ArticleNumber}", fine.Id, request.ArticleNumber);
        return ApiResponse<Guid>.Ok(fine.Id);
    }

    internal static async Task InvalidateFineCachesAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:fines:list:1:20::::", ct);
    }

    internal static async Task InvalidateFineCachesAsync(ICacheService cache, Guid fineId, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:fines:list:1:20::::", ct);
        await cache.RemoveAsync($"avtolider:fines:{fineId}", ct);
    }
}
