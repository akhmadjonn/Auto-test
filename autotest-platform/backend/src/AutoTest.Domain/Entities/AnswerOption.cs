using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class AnswerOption : BaseAuditableEntity
{
    public LocalizedText Text { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
    public Guid QuestionId { get; set; }

    public Question Question { get; set; } = null!;
}
