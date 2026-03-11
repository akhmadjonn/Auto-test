using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class ExamPoolRule : BaseAuditableEntity
{
    public Guid ExamTemplateId { get; set; }
    public Guid CategoryId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int QuestionCount { get; set; }

    public ExamTemplate ExamTemplate { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
