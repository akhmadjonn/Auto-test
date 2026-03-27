using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record DeleteGlossaryCategoryCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteGlossaryCategoryCommandValidator : AbstractValidator<DeleteGlossaryCategoryCommand>
{
    public DeleteGlossaryCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteGlossaryCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    ILogger<DeleteGlossaryCategoryCommandHandler> logger) : IRequestHandler<DeleteGlossaryCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteGlossaryCategoryCommand request, CancellationToken ct)
    {
        var category = await db.GlossaryCategories
            .Include(c => c.Terms)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (category is null)
            return ApiResponse.Fail("CATEGORY_NOT_FOUND", "Glossary category not found.");

        if (category.Terms.Count > 0)
            return ApiResponse.Fail("CATEGORY_HAS_TERMS", $"Cannot delete category with {category.Terms.Count} terms. Remove all terms first.");

        db.GlossaryCategories.Remove(category);
        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary category deleted: {CategoryId}", request.Id);

        return ApiResponse.Ok();
    }
}
