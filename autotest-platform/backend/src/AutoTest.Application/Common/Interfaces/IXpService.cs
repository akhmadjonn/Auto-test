namespace AutoTest.Application.Common.Interfaces;

public interface IXpService
{
    Task AwardXpAsync(Guid userId, int xpAmount, string reason, CancellationToken ct = default);
    Task UpdateStreakAsync(Guid userId, CancellationToken ct = default);
    Task RecordAnswerAsync(Guid userId, bool isCorrect, int? timeSpentSeconds = null, CancellationToken ct = default);
}
