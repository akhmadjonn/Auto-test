using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record DeleteGlossaryTermCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteGlossaryTermCommandValidator : AbstractValidator<DeleteGlossaryTermCommand>
{
    public DeleteGlossaryTermCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteGlossaryTermCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<DeleteGlossaryTermCommandHandler> logger) : IRequestHandler<DeleteGlossaryTermCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteGlossaryTermCommand request, CancellationToken ct)
    {
        var term = await db.GlossaryTerms.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (term is null)
            return ApiResponse.Fail("TERM_NOT_FOUND", "Glossary term not found.");

        db.GlossaryTerms.Remove(term);
        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary term deleted: {TermId}", request.Id);

        return ApiResponse.Ok();
    }
}
