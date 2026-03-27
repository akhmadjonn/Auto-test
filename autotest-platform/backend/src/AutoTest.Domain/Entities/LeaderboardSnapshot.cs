namespace AutoTest.Domain.Entities;

public class LeaderboardSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Period { get; set; } = string.Empty;
    public DateOnly SnapshotDate { get; set; }
    public int Rank { get; set; }
    public long XpValue { get; set; }

    public User User { get; set; } = null!;
}
