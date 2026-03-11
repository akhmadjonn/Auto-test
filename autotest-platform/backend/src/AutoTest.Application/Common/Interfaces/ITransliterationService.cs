namespace AutoTest.Application.Common.Interfaces;

public interface ITransliterationService
{
    string CyrillicToLatin(string text);
    string LatinToCyrillic(string text);
}
