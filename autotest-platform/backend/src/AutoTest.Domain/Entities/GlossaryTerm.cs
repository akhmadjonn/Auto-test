using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class GlossaryTerm : BaseAuditableEntity
{
    public Guid GlossaryCategoryId { get; set; }
    public LocalizedText Term { get; set; } = null!;
    public LocalizedText Definition { get; set; } = null!;
    public int SortOrder { get; set; }
    public Guid[] RelatedQuestionIds { get; set; } = [];

    public GlossaryCategory Category { get; set; } = null!;
}
