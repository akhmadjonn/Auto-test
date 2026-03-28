using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.ColorVision;

public record AdminColorVisionPlateDto(
    Guid Id,
    int PlateNumber,
    string? ImageUrl,
    string ExpectedAnswer,
    string? AlternateAnswer,
    bool IsActive,
    int SortOrder);

public record GetAdminColorVisionPlatesQuery : IRequest<ApiResponse<List<AdminColorVisionPlateDto>>>;

public class GetAdminColorVisionPlatesQueryHandler(
    IApplicationDbContext db,
    IFileStorageService storage) : IRequestHandler<GetAdminColorVisionPlatesQuery, ApiResponse<List<AdminColorVisionPlateDto>>>
{
    public async Task<ApiResponse<List<AdminColorVisionPlateDto>>> Handle(GetAdminColorVisionPlatesQuery request, CancellationToken ct)
    {
        var plates = await db.ColorVisionPlates
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        var dtos = new List<AdminColorVisionPlateDto>(plates.Count);
        foreach (var plate in plates)
        {
            string? imageUrl = null;
            if (!string.IsNullOrEmpty(plate.ImageUrl))
                imageUrl = await storage.GetPresignedUrlAsync(plate.ImageUrl, ct);

            dtos.Add(new AdminColorVisionPlateDto(
                plate.Id,
                plate.PlateNumber,
                imageUrl,
                plate.ExpectedAnswer,
                plate.AlternateAnswer,
                plate.IsActive,
                plate.SortOrder));
        }

        return ApiResponse<List<AdminColorVisionPlateDto>>.Ok(dtos);
    }
}
