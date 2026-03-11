using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Infrastructure.Services;

// Implements IPracticeService — used by external consumers (e.g. admin tools, analytics)
// Practice MediatR handlers use IApplicationDbContext directly for transactional control
public class LeitnerBoxService(
    IApplicationDbContext db,
    IDateTimeProvider dateTime) : IPracticeService
{
    private static readonly int[] LeitnerIntervals = [1, 2, 4, 8, 16];

    public async Task<IReadOnlyList<Question>> GetPracticeBatchAsync(
        Guid userId, Guid categoryId, int batchSize = 10, CancellationToken ct = default)
    {
        var now = dateTime.UtcNow;

        var dueIds = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.NextReviewDate <= now)
            .Select(s => s.QuestionId)
            .ToListAsync(ct);

        return await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.IsActive && q.CategoryId == categoryId && dueIds.Contains(q.Id))
            .OrderBy(_ => EF.Functions.Random())
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Question>> GetDueForReviewAsync(
        Guid userId, Guid? categoryId = null, int limit = 20, CancellationToken ct = default)
    {
        var now = dateTime.UtcNow;

        var dueIds = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.NextReviewDate <= now)
            .Select(s => s.QuestionId)
            .ToListAsync(ct);

        var questionsQuery = db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => q.IsActive && dueIds.Contains(q.Id));

        if (categoryId.HasValue)
            questionsQuery = questionsQuery.Where(q => q.CategoryId == categoryId.Value);

        return await questionsQuery
            .OrderBy(_ => EF.Functions.Random())
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task RecordAnswerAsync(Guid userId, Guid questionId, bool isCorrect, CancellationToken ct = default)
    {
        if (isCorrect)
            await PromoteLeitnerBoxAsync(userId, questionId, ct);
        else
            await DemoteLeitnerBoxAsync(userId, questionId, ct);
    }

    public async Task PromoteLeitnerBoxAsync(Guid userId, Guid questionId, CancellationToken ct = default)
    {
        var now = dateTime.UtcNow;
        var state = await GetOrCreateStateAsync(userId, questionId, now, ct);

        state.CorrectAttempts++;
        state.TotalAttempts++;
        state.LastAttemptAt = now;

        var nextBox = (int)state.LeitnerBox < 5
            ? (LeitnerBox)((int)state.LeitnerBox + 1)
            : LeitnerBox.Box5;
        state.LeitnerBox = nextBox;
        state.NextReviewDate = now.AddDays(LeitnerIntervals[(int)nextBox - 1]);

        await db.SaveChangesAsync(ct);
    }

    public async Task DemoteLeitnerBoxAsync(Guid userId, Guid questionId, CancellationToken ct = default)
    {
        var now = dateTime.UtcNow;
        var state = await GetOrCreateStateAsync(userId, questionId, now, ct);

        state.TotalAttempts++;
        state.LastAttemptAt = now;
        state.LeitnerBox = LeitnerBox.Box1;
        state.NextReviewDate = now.AddDays(LeitnerIntervals[0]);

        await db.SaveChangesAsync(ct);
    }

    private async Task<UserQuestionState> GetOrCreateStateAsync(
        Guid userId, Guid questionId, DateTimeOffset now, CancellationToken ct)
    {
        var state = await db.UserQuestionStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.QuestionId == questionId, ct);

        if (state is not null)
            return state;

        state = new UserQuestionState
        {
            UserId = userId,
            QuestionId = questionId,
            LeitnerBox = LeitnerBox.Box1,
            NextReviewDate = now.AddDays(LeitnerIntervals[0])
        };
        db.UserQuestionStates.Add(state);
        return state;
    }
}
