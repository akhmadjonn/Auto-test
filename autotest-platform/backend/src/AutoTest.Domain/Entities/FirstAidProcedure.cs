using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class FirstAidProcedure : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText? Summary { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<FirstAidStep> Steps { get; set; } = [];
}
