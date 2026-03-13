namespace Avtolider.DataMigration.Services;

public sealed class MigrationStats
{
    private int _imported;
    private int _skipped;
    private int _imagesUploaded;
    private int _errors;
    private int _duplicatesRemoved;

    public int Imported => _imported;
    public int Skipped => _skipped;
    public int ImagesUploaded => _imagesUploaded;
    public int Errors => _errors;
    public int DuplicatesRemoved => _duplicatesRemoved;

    public void RecordImported(int count = 1) =>
        Interlocked.Add(ref _imported, count);

    public void RecordSkipped(int count = 1) =>
        Interlocked.Add(ref _skipped, count);

    public void RecordImageUploaded(int count = 1) =>
        Interlocked.Add(ref _imagesUploaded, count);

    public void RecordError(int count = 1) =>
        Interlocked.Add(ref _errors, count);

    public void RecordDuplicateRemoved(int count = 1) =>
        Interlocked.Add(ref _duplicatesRemoved, count);

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("  MIGRATION SUMMARY");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"  Imported:           {_imported}");
        Console.WriteLine($"  Skipped:            {_skipped}");
        Console.WriteLine($"  Images uploaded:    {_imagesUploaded}");
        Console.WriteLine($"  Duplicates removed: {_duplicatesRemoved}");
        Console.WriteLine($"  Errors:             {_errors}");
        Console.WriteLine("═══════════════════════════════════════");
    }

    public void Reset()
    {
        _imported = 0;
        _skipped = 0;
        _imagesUploaded = 0;
        _errors = 0;
        _duplicatesRemoved = 0;
    }
}
