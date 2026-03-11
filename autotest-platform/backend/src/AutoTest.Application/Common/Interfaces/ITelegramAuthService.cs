namespace AutoTest.Application.Common.Interfaces;

public interface ITelegramAuthService
{
    bool VerifyHash(long id, string firstName, string? lastName, string? username, string? photoUrl, long authDate, string hash);
}
