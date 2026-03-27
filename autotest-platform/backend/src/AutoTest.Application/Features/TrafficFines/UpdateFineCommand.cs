using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record UpdateFineCommand(
    Guid Id,
    string ArticleNumber,
    string ViolationDescriptionUz,
    string ViolationDescriptionUzLatin,
    string ViolationDescriptionRu,
    string? AdditionalNotesUz,
    string? AdditionalNotesUzLatin,
    string? AdditionalNotesRu,
    long PenaltyAmountTiyins,
    long? PenaltyMaxTiyins,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateFineCommandValidator : AbstractValidator<UpdateFineCommand>
{
    public UpdateFineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ArticleNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ViolationDescriptionUz).NotEmpty();
        RuleFor(x => x.ViolationDescriptionUzLatin).NotEmpty();
        RuleFor(x => x.ViolationDescriptionRu).NotEmpty();
        RuleFor(x => x.PenaltyAmountTiyins).GreaterThan(0);
        RuleFor(x => x.PenaltyMaxTiyins)
            .GreaterThanOrEqualTo(x => x.PenaltyAmountTiyins)
            .When(x => x.PenaltyMaxTiyins.HasValue)
            .WithMessage("Max penalty must be >= min penalty");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateFineCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<UpdateFineCommandHandler> logger) : IRequestHandler<UpdateFineCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateFineCommand request, CancellationToken ct)
    {
        var fine = await db.TrafficFines.FindAsync([request.Id], ct);
        if (fine is null)
            return ApiResponse.Fail("NOT_FOUND", "Traffic fine not found.");

        fine.ArticleNumber = request.ArticleNumber;
        fine.ViolationDescription = new LocalizedText(
            request.ViolationDescriptionUz,
            request.ViolationDescriptionUzLatin,
            request.ViolationDescriptionRu);
        fine.AdditionalNotes = request.AdditionalNotesUz is not null
            ? new LocalizedText(
                request.AdditionalNotesUz,
                request.AdditionalNotesUzLatin ?? string.Empty,
                request.AdditionalNotesRu ?? string.Empty)
            : null;
        fine.PenaltyAmountTiyins = request.PenaltyAmountTiyins;
        fine.PenaltyMaxTiyins = request.PenaltyMaxTiyins;
        fine.SortOrder = request.SortOrder;
        fine.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFineCommandHandler.InvalidateFineCachesAsync(cache, request.Id, ct);

        logger.LogInformation("Updated traffic fine {FineId}", request.Id);
        return ApiResponse.Ok();
    }
}
