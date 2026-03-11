using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class ExamTemplate : BaseAuditableEntity
{
    public LocalizedText Title { get; set; } = null!;
    public int TotalQuestions { get; set; }
    public int PassingScore { get; set; }
    public int TimeLimitMinutes { get; set; }
    public bool IsActive { get; set; }

    public ICollection<ExamPoolRule> PoolRules { get; set; } = [];
}
