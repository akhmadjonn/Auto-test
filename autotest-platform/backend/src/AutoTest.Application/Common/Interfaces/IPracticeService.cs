using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;

namespace AutoTest.Application.Common.Interfaces;

public interface IPracticeService
{
    Task<IReadOnlyList<Question>> GetPracticeBatchAsync(Guid userId, Guid categoryId, int batchSize = 10, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> GetDueForReviewAsync(Guid userId, Guid? categoryId = null, int limit = 20, CancellationToken ct = default);
    Task RecordAnswerAsync(Guid userId, Guid questionId, bool isCorrect, CancellationToken ct = default);
    Task PromoteLeitnerBoxAsync(Guid userId, Guid questionId, CancellationToken ct = default);
    Task DemoteLeitnerBoxAsync(Guid userId, Guid questionId, CancellationToken ct = default);
}
