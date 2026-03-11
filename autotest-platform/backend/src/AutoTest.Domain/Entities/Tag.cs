using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class Tag : BaseAuditableEntity
{
    public LocalizedText Name { get; set; } = null!;
    public string Slug { get; set; } = null!;

    public ICollection<Question> Questions { get; set; } = [];
}
