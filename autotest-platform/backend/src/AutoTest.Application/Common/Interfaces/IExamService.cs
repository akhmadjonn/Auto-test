using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;

namespace AutoTest.Application.Common.Interfaces;

public interface IExamService
{
    Task<ExamSession> StartExamAsync(Guid userId, Guid examTemplateId, CancellationToken ct = default);
    Task<ExamSession> StartTicketExamAsync(Guid userId, int ticketNumber, LicenseCategory category, CancellationToken ct = default);
    Task<ExamSession> StartMarathonAsync(Guid userId, LicenseCategory category, CancellationToken ct = default);
    Task<ExamSession?> GetActiveMarathonAsync(Guid userId, CancellationToken ct = default);
    Task<ExamSession> CompleteExamAsync(Guid examSessionId, CancellationToken ct = default);
    Task SubmitAnswerAsync(Guid examSessionId, Guid questionId, Guid answerId, CancellationToken ct = default);
}
