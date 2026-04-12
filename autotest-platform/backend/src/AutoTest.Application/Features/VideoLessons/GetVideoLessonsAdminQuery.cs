using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.VideoLessons;

public record GetVideoLessonsAdminQuery(Guid? VideoCategoryId = null) : IRequest<ApiResponse<List<VideoLessonAdminDto>>>;

public class GetVideoLessonsAdminQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetVideoLessonsAdminQuery, ApiResponse<List<VideoLessonAdminDto>>>
{
    public async Task<ApiResponse<List<VideoLessonAdminDto>>> Handle(GetVideoLessonsAdminQuery request, CancellationToken ct)
    {
        var query = db.VideoLessons.AsNoTracking();

        if (request.VideoCategoryId.HasValue)
            query = query.Where(l => l.VideoCategoryId == request.VideoCategoryId.Value);

        var lessons = await query
            .OrderBy(l => l.SortOrder)
            .Select(l => new VideoLessonAdminDto(
                l.Id, l.VideoCategoryId, l.Title, l.Description,
                l.SourceType, l.VideoUrl, l.ThumbnailUrl,
                l.DurationSeconds, l.SortOrder,
                l.IsFree, l.IsDownloadable, l.LinkedCategoryId, l.IsActive, l.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<List<VideoLessonAdminDto>>.Ok(lessons);
    }
}
