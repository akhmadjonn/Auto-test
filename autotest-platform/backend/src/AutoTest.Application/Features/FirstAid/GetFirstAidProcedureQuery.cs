using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.FirstAid;

public record GetFirstAidProcedureQuery(string Slug) : IRequest<ApiResponse<FirstAidProcedureDto>>;

public record FirstAidProcedureDto(
    Guid Id,
    string Slug,
    LocalizedText Name,
    LocalizedText? Summary,
    string IconUrl,
    int SortOrder,
    List<FirstAidStepDto> Steps);

public record FirstAidStepDto(
    Guid Id,
    int StepOrder,
    LocalizedText Title,
    LocalizedText Description,
    string? ImageUrl);

public class GetFirstAidProcedureQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IFileStorageService storage,
    ILogger<GetFirstAidProcedureQueryHandler> logger) : IRequestHandler<GetFirstAidProcedureQuery, ApiResponse<FirstAidProcedureDto>>
{
    public async Task<ApiResponse<FirstAidProcedureDto>> Handle(GetFirstAidProcedureQuery request, CancellationToken ct)
    {
        var cacheKey = $"avtolider:first-aid:{request.Slug}";
        var cached = await cache.GetAsync<FirstAidProcedureDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<FirstAidProcedureDto>.Ok(cached);

        var procedure = await db.FirstAidProcedures
            .AsNoTracking()
            .Include(p => p.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct);

        if (procedure is null)
            return ApiResponse<FirstAidProcedureDto>.Fail("PROCEDURE_NOT_FOUND", "First aid procedure not found.");

        var steps = new List<FirstAidStepDto>();
        foreach (var step in procedure.Steps)
        {
            string? presignedUrl = null;
            if (step.ImageUrl is not null)
                presignedUrl = await storage.GetPresignedUrlAsync(step.ImageUrl, ct);

            steps.Add(new FirstAidStepDto(
                step.Id,
                step.StepOrder,
                step.Title,
                step.Description,
                presignedUrl));
        }

        var iconUrl = string.IsNullOrEmpty(procedure.IconUrl)
            ? string.Empty
            : await storage.GetPresignedUrlAsync(procedure.IconUrl, ct);

        var dto = new FirstAidProcedureDto(
            procedure.Id,
            procedure.Slug,
            procedure.Name,
            procedure.Summary,
            iconUrl,
            procedure.SortOrder,
            steps);

        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(50), ct);
        logger.LogDebug("First aid procedure {Slug} loaded from DB, cached for 50min", request.Slug);

        return ApiResponse<FirstAidProcedureDto>.Ok(dto);
    }
}
