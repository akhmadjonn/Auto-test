using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class GlossaryCategory : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = null!;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }

    public ICollection<GlossaryTerm> Terms { get; set; } = [];
}
