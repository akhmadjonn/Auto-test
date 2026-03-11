namespace AutoTest.Domain.Entities;

public class UserCategoryStat
{
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
