# Avtolider.DataMigration

CLI tool that imports Avtolider questions, road-sign images, and other content into
the AutoTest database. Runs against any environment by switching connection
strings — no data is committed, everything is regenerated per-environment from
the latest production PostgreSQL dump.

## Quick start (local)

```bash
# 1. Configure connection (or set MIGRATION_ConnectionStrings__PostgreSQL env var)
cp appsettings.json appsettings.local.json   # edit as needed; gitignored

# 2. Generate JSON + download question images from the production dump
python scripts/extract_dump.py /path/to/autoleader_backup.dump.zst

# 3. Run the full import pipeline
dotnet run -- import-all
```

## Server workflow

JSON files (`themes.json`, `questions.json`, `options.json`) **are tracked in
git** so the team can review changes via PR diffs. Images are too large to
commit and must be regenerated per environment.

```bash
# 1. Pull latest code (brings the JSON with it)
git pull

# 2. Download images. The script reads URLs from the tracked questions.json,
#    no dump file required. Idempotent: skips already-downloaded images.
cd /repo/backend/tools/Avtolider.DataMigration
python3 scripts/extract_dump.py --images-only

# 3. Configure DB + MinIO (env vars are simplest; no secrets in files)
export MIGRATION_ConnectionStrings__PostgreSQL="Host=...;Database=avtolider_prod;Username=...;Password=...;Search Path=autotest"
export MIGRATION_MinioSettings__Endpoint="minio.internal:9000"
export MIGRATION_MinioSettings__AccessKey="..."
export MIGRATION_MinioSettings__SecretKey="..."
export MIGRATION_MinioSettings__BucketName="autotest-images"

# 4. Wipe questions for a clean re-import (only safe with no real users yet)
psql "$DATABASE_URL" -c 'TRUNCATE autotest."AnswerOptions", autotest."Questions" CASCADE;'

# 5. Restart the API once so DbSeeder creates the new vehicle-classification
#    category and rewrites the exam pool rules.

# 6. Run the importer (dry-run first to preview)
dotnet run -- import-all --dry-run
dotnet run -- import-all
```

### Refreshing JSON from a newer dump (do this from your local machine)

When a fresher production dump arrives, refresh the JSON so the team and the
servers pick it up via `git pull`:

```bash
# Local machine — needs pg_restore + the dump file
cd backend/tools/Avtolider.DataMigration
python3 scripts/extract_dump.py /path/to/newer_dump.zst

# Review the diff in git, commit + push
git diff data/avtolider/*.json
git add data/avtolider/*.json
git commit -m "data(avtolider): refresh from <dump-date>"
git push
```

The migration tool's `import-all` runs 7 stages in sequence. See `Program.cs` for
the full list. The first two stages do the actual question inserts; stages 3-7
deduplicate, enrich, and upload media.

## Commands

| Command | What it does |
|---|---|
| `import-apk` | 700 questions extracted from a competitor APK (trilingual + explanations). Inserts as `Status=Inactive` in the `uncategorized` category. |
| `import-avtolider` | 1073 questions (from the latest dump). Mapped directly to PDD categories via `theme_id → slug`. Inserts as `Status=Active`. |
| `deduplicate` | Fuzzy Levenshtein merge across both sources. **Image-bearing questions are no longer collapsed** when only the text matches — only same-image-or-both-text-only pairs merge. |
| `merge-explanations` | Backfills APK descriptions into Avtolider questions matched by normalized Russian text. |
| `assign-categories` | Reassigns APK questions out of `uncategorized` using keyword matching. Successfully reassigned questions flip `Inactive → Active`. Anything still uncategorized stays `Inactive` (invisible to users until admin reviews). |
| `import-visual-assets` | Uploads road signs, markings, hazard labels, first aid images to MinIO. |
| `import-road-signs` | Creates/updates `RoadSign` and `RoadMarking` records from extracted competitor APK assets. |
| `import-all` | Runs all seven stages in sequence. |

Flags:
- `--dry-run` — preview without writing to DB or uploading to MinIO.
- `--hard` (`deduplicate` only) — hard-delete duplicates instead of soft-archiving them.

## Deduplication rule (PO-confirmed)

A question is unique on **(normalized Russian text + image source)**. Same Russian
wording + different image = two distinct picture-questions, both kept. Same Russian
wording + same image (or both text-only) = collapse.

This recovers ~136 picture-question variants that prior runs lost to text-only
deduplication. See `DUPLICATE_QUESTIONS_REPORT.md` in the original analysis for
the full list of previously-collapsed groups.

## Question status lifecycle

| Status | Meaning | Visible in exam? |
|---|---|---|
| `Active` | Categorized, ready for users | ✅ yes |
| `Inactive` | Awaiting admin review, soft-deleted by admin, or collapsed by dedup | ❌ no |

Admin "delete" sets `Status = Inactive` (row stays in DB, all foreign-key history intact). Hard-delete is a separate explicit `/permanent` admin endpoint that removes the row and the MinIO images.

After `import-all`, expect:
- ~1500 Active questions across all 29 PDD categories
- ~300 Inactive questions in `uncategorized` for admin review
- 0 questions in the `cd-specific` slot (license-class content TBD)

## Why nothing under `data/avtolider/` is in git

The dump is the authoritative source. JSON is derived; images are downloaded.
Re-running `extract_dump.py` regenerates everything deterministically. Committing
either would just stale the repo. The script's image download is idempotent —
existing files are skipped, so re-runs are cheap.

## Sample-question seeder is local-only

`DbSeeder.SeedQuestionsAsync` (the small hard-coded fallback set) is gated behind
the `Seeding:SeedSampleQuestions` config flag. To enable for local development,
add to `backend/src/AutoTest.Api/appsettings.Development.json`:

```json
{ "Seeding": { "SeedSampleQuestions": true } }
```

Real environments leave it disabled and populate questions exclusively via this
migration tool.
