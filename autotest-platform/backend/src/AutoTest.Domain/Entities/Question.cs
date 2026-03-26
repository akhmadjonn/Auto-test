using System.ComponentModel.DataAnnotations.Schema;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class Question : BaseAuditableEntity
{
    public LocalizedText Text { get; set; } = null!;
    public LocalizedText Explanation { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Difficulty Difficulty { get; set; }
    public int TicketNumber { get; set; }
    public LicenseCategory LicenseCategory { get; set; }
    public QuestionStatus Status { get; set; } = QuestionStatus.Active;
    public Guid CategoryId { get; set; }

    // Global question statistics (incremented on every exam/practice answer)
    public int TotalAttempts { get; set; }
    public int CorrectCount { get; set; }
    public double? AvgTimeSec { get; set; }

    [NotMapped]
    public double SuccessRate => TotalAttempts > 0 ? (double)CorrectCount / TotalAttempts : 0;

    public Category Category { get; set; } = null!;
    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
