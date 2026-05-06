using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

// Paginated batch fetch for marathon (and other long sessions). Returns a slice ordered by
// Order ascending; verdict fields populated for already-answered questions only (anti-cheat).
public record GetExamQuestionsBatchQuery(
    Guid SessionId,
    int From,
    int Take,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<ExamQuestionsBatchDto>>;

public record ExamQuestionsBatchDto(
    int From,
    int Take,
    int TotalQuestions,
    List<ExamQuestionDto> Questions);

public class GetExamQuestionsBatchQueryValidator : AbstractValidator<GetExamQuestionsBatchQuery>
{
    public GetExamQuestionsBatchQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.From).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Take).InclusiveBetween(1, 50);
    }
}

public class GetExamQuestionsBatchQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage)
    : IRequestHandler<GetExamQuestionsBatchQuery, ApiResponse<ExamQuestionsBatchDto>>
{
    public async Task<ApiResponse<ExamQuestionsBatchDto>> Handle(
        GetExamQuestionsBatchQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamQuestionsBatchDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<ExamQuestionsBatchDto>.Fail("SESSION_NOT_FOUND", "Session not found.");

        if (session.Status != ExamStatus.InProgress)
            return ApiResponse<ExamQuestionsBatchDto>.Fail("SESSION_NOT_ACTIVE", "Session is not active.");

        var totalQuestions = await db.SessionQuestions
            .CountAsync(sq => sq.ExamSessionId == request.SessionId, ct);

        var endOrder = request.From + request.Take - 1;

        var batch = await db.SessionQuestions
            .AsNoTracking()
            .Include(sq => sq.Question)
            .ThenInclude(q => q.AnswerOptions)
            .Where(sq => sq.ExamSessionId == request.SessionId
                && sq.Order >= request.From
                && sq.Order <= endOrder)
            .OrderBy(sq => sq.Order)
            .ToListAsync(ct);

        var allImageKeys = new List<string>();
        foreach (var sq in batch)
        {
            if (sq.Question.ImageUrl is not null) allImageKeys.Add(sq.Question.ImageUrl);
            foreach (var a in sq.Question.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var includeExplanation = session.Mode == ExamMode.Marathon;

        var questions = batch.Select(sq =>
        {
            var q = sq.Question;
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;

            // Shuffle option ORDER for unanswered questions to mirror StartMarathon. Once
            // answered, preserve original order so the user sees the same layout they did
            // when they committed (avoids confusing "wait, where did my answer go" UX).
            var optionsList = sq.SelectedAnswerId is null
                ? q.AnswerOptions.OrderBy(_ => Random.Shared.Next()).ToList()
                : q.AnswerOptions.OrderBy(a => a.SortOrder).ToList();

            var optDtos = optionsList.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new ExamAnswerOptionDto(a.Id, a.Text, optImg);
            }).ToList();

            Guid? correctAnswerId = null;
            bool? isCorrect = null;
            LocalizedText? explanation = null;
            if (sq.SelectedAnswerId is not null)
            {
                correctAnswerId = q.AnswerOptions.FirstOrDefault(a => a.IsCorrect)?.Id;
                isCorrect = sq.IsCorrect;
                if (includeExplanation)
                    explanation = q.Explanation;
            }

            return new ExamQuestionDto(
                sq.Id, q.Id, sq.Order, q.Text, imgUrl, optDtos,
                sq.SelectedAnswerId, correctAnswerId, isCorrect, explanation);
        }).ToList();

        return ApiResponse<ExamQuestionsBatchDto>.Ok(new ExamQuestionsBatchDto(
            request.From, request.Take, totalQuestions, questions));
    }
}
