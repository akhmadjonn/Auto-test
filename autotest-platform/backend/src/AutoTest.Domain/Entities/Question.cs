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
    public bool IsActive { get; set; }
    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
