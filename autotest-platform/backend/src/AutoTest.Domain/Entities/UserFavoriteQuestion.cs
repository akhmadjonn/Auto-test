namespace AutoTest.Domain.Entities;

public class UserFavoriteQuestion
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
