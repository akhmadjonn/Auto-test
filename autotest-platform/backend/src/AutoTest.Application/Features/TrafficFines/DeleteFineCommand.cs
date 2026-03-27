using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.TrafficFines;

public record DeleteFineCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteFineCommandValidator : AbstractValidator<DeleteFineCommand>
{
    public DeleteFineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteFineCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<DeleteFineCommandHandler> logger) : IRequestHandler<DeleteFineCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteFineCommand request, CancellationToken ct)
    {
        var fine = await db.TrafficFines.FindAsync([request.Id], ct);
        if (fine is null)
            return ApiResponse.Fail("NOT_FOUND", "Traffic fine not found.");

        fine.IsActive = false;
        fine.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateFineCommandHandler.InvalidateFineCachesAsync(cache, request.Id, ct);

        logger.LogInformation("Soft-deleted traffic fine {FineId}", request.Id);
        return ApiResponse.Ok();
    }
}
