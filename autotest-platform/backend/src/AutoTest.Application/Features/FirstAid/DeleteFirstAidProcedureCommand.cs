using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record DeleteFirstAidProcedureCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteFirstAidProcedureCommandValidator : AbstractValidator<DeleteFirstAidProcedureCommand>
{
    public DeleteFirstAidProcedureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteFirstAidProcedureCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<DeleteFirstAidProcedureCommandHandler> logger) : IRequestHandler<DeleteFirstAidProcedureCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteFirstAidProcedureCommand request, CancellationToken ct)
    {
        var procedure = await db.FirstAidProcedures
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (procedure is null)
            return ApiResponse.Fail("PROCEDURE_NOT_FOUND", "First aid procedure not found.");

        // Collect step images for deletion from MinIO
        var imageKeys = procedure.Steps
            .Where(s => s.ImageUrl is not null)
            .Select(s => s.ImageUrl!)
            .ToList();

        var slug = procedure.Slug;

        db.FirstAidProcedures.Remove(procedure);
        await db.SaveChangesAsync(ct);
        await CreateFirstAidProcedureCommandHandler.InvalidateFirstAidCacheAsync(cache, slug, ct);

        if (imageKeys.Count > 0)
            await storage.DeleteManyAsync(imageKeys, ct);

        logger.LogInformation("Deleted first aid procedure {ProcedureId} with {StepCount} steps", request.Id, procedure.Steps.Count);
        return ApiResponse.Ok();
    }
}
