using AutoTest.Infrastructure.Services;
using ClosedXML.Excel;
using FluentAssertions;

namespace AutoTest.Application.Tests.Infrastructure;

public class ExcelQuestionParserTests
{
    private readonly ExcelQuestionParser _parser = new();

    [Fact]
    public async Task ParseExcel_ValidData_ReturnsQuestions()
    {
        using var stream = CreateTestExcel(new TestRow
        {
            TicketNumber = 1, Order = 1, CategorySlug = "road-signs", Difficulty = 1,
            LicenseCategory = "AB", TextUz = "Savol", TextUzLatin = "Savol", TextRu = "Вопрос",
            ExplanationUz = "Expl", ExplanationUzLatin = "Expl", ExplanationRu = "Объясн",
            CorrectAnswer = "Javob1", IsActive = "true",
            Opt1Uz = "Javob1", Opt1UzLatin = "Javob1", Opt1Ru = "Ответ1",
            Opt2Uz = "Javob2", Opt2UzLatin = "Javob2", Opt2Ru = "Ответ2"
        });

        var result = await _parser.ParseExcelAsync(stream);

        result.Questions.Should().HaveCount(1);
        result.Errors.Should().BeEmpty();
        result.Questions[0].TextUz.Should().Be("Savol");
        result.Questions[0].CategorySlug.Should().Be("road-signs");
        result.Questions[0].Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseExcel_MissingRequiredField_ReturnsError()
    {
        using var stream = CreateTestExcel(new TestRow
        {
            TicketNumber = 1, Order = 1, CategorySlug = "", Difficulty = 1, // empty category
            LicenseCategory = "AB", TextUz = "Savol", TextUzLatin = "Savol", TextRu = "Вопрос",
            ExplanationUz = "Expl", ExplanationUzLatin = "Expl", ExplanationRu = "Объясн",
            CorrectAnswer = "Javob", IsActive = "true",
            Opt1Uz = "A", Opt1UzLatin = "A", Opt1Ru = "A",
            Opt2Uz = "B", Opt2UzLatin = "B", Opt2Ru = "B"
        });

        var result = await _parser.ParseExcelAsync(stream);

        result.Questions.Should().BeEmpty();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Column == "CategorySlug");
    }

    [Fact]
    public async Task ParseExcel_InvalidDifficulty_ReturnsError()
    {
        using var stream = CreateTestExcelRaw(row =>
        {
            row[1] = "1"; row[2] = "1"; row[3] = "road-signs";
            row[4] = "abc"; // invalid difficulty
            row[5] = "AB"; row[6] = "Q"; row[7] = "Q"; row[8] = "Q";
            row[9] = "E"; row[10] = "E"; row[11] = "E";
            row[12] = ""; row[13] = "Answer"; row[14] = "true";
            row[15] = "A"; row[16] = "A"; row[17] = "A"; row[18] = "";
            row[19] = "B"; row[20] = "B"; row[21] = "B"; row[22] = "";
        });

        var result = await _parser.ParseExcelAsync(stream);
        result.Errors.Should().Contain(e => e.Column == "Difficulty");
    }

    [Fact]
    public async Task ParseExcel_LessThan2Options_ReturnsError()
    {
        using var stream = CreateTestExcel(new TestRow
        {
            TicketNumber = 1, Order = 1, CategorySlug = "road-signs", Difficulty = 1,
            LicenseCategory = "AB", TextUz = "Savol", TextUzLatin = "Savol", TextRu = "Вопрос",
            ExplanationUz = "Expl", ExplanationUzLatin = "Expl", ExplanationRu = "Объясн",
            CorrectAnswer = "Javob", IsActive = "true",
            Opt1Uz = "A", Opt1UzLatin = "A", Opt1Ru = "A"
            // Only 1 option
        });

        var result = await _parser.ParseExcelAsync(stream);

        result.Questions.Should().BeEmpty();
        result.Errors.Should().Contain(e => e.Error.Contains("2 answer options"));
    }

    [Fact]
    public async Task ParseExcel_MultipleRows_ParsesAll()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Questions");
        WriteHeaders(ws);

        for (var i = 0; i < 5; i++)
            WriteRow(ws, i + 2, new TestRow
            {
                TicketNumber = i + 1, Order = 1, CategorySlug = "cat", Difficulty = 1,
                LicenseCategory = "AB", TextUz = $"Q{i}", TextUzLatin = $"Q{i}", TextRu = $"Q{i}",
                ExplanationUz = "E", ExplanationUzLatin = "E", ExplanationRu = "E",
                CorrectAnswer = "A", IsActive = "true",
                Opt1Uz = "A", Opt1UzLatin = "A", Opt1Ru = "A",
                Opt2Uz = "B", Opt2UzLatin = "B", Opt2Ru = "B"
            });

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var result = await _parser.ParseExcelAsync(ms);
        result.Questions.Should().HaveCount(5);
        result.Errors.Should().BeEmpty();
    }

    private static void WriteHeaders(IXLWorksheet ws)
    {
        string[] headers =
        [
            "TicketNumber", "Order", "CategorySlug", "Difficulty", "LicenseCategory",
            "TextUz", "TextUzLatin", "TextRu",
            "ExplanationUz", "ExplanationUzLatin", "ExplanationRu",
            "QuestionImageFile", "CorrectAnswer", "IsActive",
            "Opt1TextUz", "Opt1TextUzLatin", "Opt1TextRu", "Opt1Image",
            "Opt2TextUz", "Opt2TextUzLatin", "Opt2TextRu", "Opt2Image",
            "Opt3TextUz", "Opt3TextUzLatin", "Opt3TextRu", "Opt3Image",
            "Opt4TextUz", "Opt4TextUzLatin", "Opt4TextRu", "Opt4Image"
        ];
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
    }

    private static void WriteRow(IXLWorksheet ws, int row, TestRow data)
    {
        ws.Cell(row, 1).Value = data.TicketNumber;
        ws.Cell(row, 2).Value = data.Order;
        ws.Cell(row, 3).Value = data.CategorySlug;
        ws.Cell(row, 4).Value = data.Difficulty;
        ws.Cell(row, 5).Value = data.LicenseCategory;
        ws.Cell(row, 6).Value = data.TextUz;
        ws.Cell(row, 7).Value = data.TextUzLatin;
        ws.Cell(row, 8).Value = data.TextRu;
        ws.Cell(row, 9).Value = data.ExplanationUz;
        ws.Cell(row, 10).Value = data.ExplanationUzLatin;
        ws.Cell(row, 11).Value = data.ExplanationRu;
        ws.Cell(row, 12).Value = "";
        ws.Cell(row, 13).Value = data.CorrectAnswer;
        ws.Cell(row, 14).Value = data.IsActive;
        ws.Cell(row, 15).Value = data.Opt1Uz ?? "";
        ws.Cell(row, 16).Value = data.Opt1UzLatin ?? "";
        ws.Cell(row, 17).Value = data.Opt1Ru ?? "";
        ws.Cell(row, 18).Value = "";
        ws.Cell(row, 19).Value = data.Opt2Uz ?? "";
        ws.Cell(row, 20).Value = data.Opt2UzLatin ?? "";
        ws.Cell(row, 21).Value = data.Opt2Ru ?? "";
        ws.Cell(row, 22).Value = "";
    }

    private Stream CreateTestExcel(TestRow data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Questions");
        WriteHeaders(ws);
        WriteRow(ws, 2, data);

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private Stream CreateTestExcelRaw(Action<Dictionary<int, string>> populateRow)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Questions");
        WriteHeaders(ws);

        var row = new Dictionary<int, string>();
        populateRow(row);
        foreach (var kv in row)
            ws.Cell(2, kv.Key).Value = kv.Value;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private class TestRow
    {
        public int TicketNumber { get; set; }
        public int Order { get; set; }
        public string CategorySlug { get; set; } = "";
        public int Difficulty { get; set; }
        public string LicenseCategory { get; set; } = "";
        public string TextUz { get; set; } = "";
        public string TextUzLatin { get; set; } = "";
        public string TextRu { get; set; } = "";
        public string ExplanationUz { get; set; } = "";
        public string ExplanationUzLatin { get; set; } = "";
        public string ExplanationRu { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public string IsActive { get; set; } = "true";
        public string? Opt1Uz { get; set; }
        public string? Opt1UzLatin { get; set; }
        public string? Opt1Ru { get; set; }
        public string? Opt2Uz { get; set; }
        public string? Opt2UzLatin { get; set; }
        public string? Opt2Ru { get; set; }
    }
}
