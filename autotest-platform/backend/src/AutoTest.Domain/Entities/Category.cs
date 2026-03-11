using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class Category : BaseAuditableEntity
{
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText Description { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? IconUrl { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Question> Questions { get; set; } = [];
}
