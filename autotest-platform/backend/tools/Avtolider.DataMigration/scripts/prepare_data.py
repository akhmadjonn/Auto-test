#!/usr/bin/env python3
"""
prepare_data.py — One-time data preparation for Avtolider.DataMigration tool.

This script:
1. Parses Avtolider PostgreSQL dump → JSON files (themes, questions, options)
2. Downloads 582 question images from external URLs (telegra.ph, postimg.cc)
3. Copies visual assets from extracted Avto Test PRO APK

Usage:
    python3 scripts/prepare_data.py --dump-sql /tmp/avtolider_dump.sql
    python3 scripts/prepare_data.py --from-json   # use pre-extracted JSON from $USERPROFILE
    python3 scripts/prepare_data.py --skip-download  # skip image downloads (use existing)
"""

import argparse
import json
import os
import re
import shutil
import ssl
import sys
import time
import urllib.request
import urllib.error
from collections import defaultdict
from pathlib import Path

# Create SSL context that doesn't verify certificates (needed for postimg.cc/telegra.ph)
SSL_CTX = ssl.create_default_context()
SSL_CTX.check_hostname = False
SSL_CTX.verify_mode = ssl.CERT_NONE

# Resolve paths
SCRIPT_DIR = Path(__file__).resolve().parent
TOOL_DIR = SCRIPT_DIR.parent
DATA_DIR = TOOL_DIR / "data"
AVTOLIDER_DIR = DATA_DIR / "avtolider"
AVTOLIDER_IMG_DIR = AVTOLIDER_DIR / "img"
VISUAL_ASSETS_DIR = DATA_DIR / "visual_assets"

HOME = Path(os.environ.get("USERPROFILE", os.environ.get("HOME", "/tmp")))

# Avto Test PRO extracted path (from APK analysis)
ATP_ASSETS_DEFAULT = Path("/tmp/atp_extract/assets/flutter_assets/assets")


def parse_dump_sql(sql_path: str) -> tuple[list, list, list]:
    """Parse PostgreSQL plain-text dump to extract themes, questions, options."""
    print(f"  Parsing SQL dump: {sql_path}")

    with open(sql_path, "r", encoding="utf-8") as f:
        content = f.read()

    null_marker = "\\N"

    def extract_copy_block(table_name: str) -> list[list[str]]:
        pattern = rf"COPY public\.{table_name}\s+\(.*?\)\s+FROM stdin;\n(.*?)\n\\."
        match = re.search(pattern, content, re.DOTALL)
        if not match:
            print(f"  [WARN] Table {table_name} not found in dump")
            return []
        rows = []
        for line in match.group(1).strip().split("\n"):
            rows.append(line.split("\t"))
        return rows

    # Parse themes: id, name_uz, name_ru, order, is_active, created_at, updated_at
    theme_rows = extract_copy_block("quizzes_theme")
    themes = []
    for row in theme_rows:
        if len(row) >= 3:
            themes.append({
                "id": int(row[0]),
                "name_uz": row[1] if row[1] != null_marker else "",
                "name_ru": row[2] if row[2] != null_marker else "",
            })

    # Parse questions: id, question_uz, question_ru, image_url, is_active, created_at, updated_at, theme_id
    question_rows = extract_copy_block("quizzes_quiz")
    questions = []
    for row in question_rows:
        if len(row) >= 8:
            questions.append({
                "id": int(row[0]),
                "question_uz": row[1] if row[1] != null_marker else "",
                "question_ru": row[2] if row[2] != null_marker else "",
                "image_url": row[3] if row[3] != null_marker else None,
                "is_active": row[4] == "t",
                "theme_id": int(row[7]),
            })

    # Parse options: id, text_uz, text_ru, is_correct, created_at, updated_at, quiz_id
    option_rows = extract_copy_block("quizzes_option")
    options = []
    for row in option_rows:
        if len(row) >= 7:
            options.append({
                "id": int(row[0]),
                "text_uz": row[1] if row[1] != null_marker else "",
                "text_ru": row[2] if row[2] != null_marker else "",
                "is_correct": row[3] == "t",
                "quiz_id": int(row[6]),
            })

    return themes, questions, options


def load_from_json() -> tuple[list, list, list]:
    """Load pre-extracted JSON from $USERPROFILE (from earlier analysis session)."""
    print(f"  Loading pre-extracted JSON from: {HOME}")

    with open(HOME / "db_themes.json", "r", encoding="utf-8") as f:
        raw_themes = json.load(f)
    with open(HOME / "db_questions.json", "r", encoding="utf-8") as f:
        raw_questions = json.load(f)
    with open(HOME / "db_options.json", "r", encoding="utf-8") as f:
        raw_options = json.load(f)

    # Convert to expected format
    themes = [{"id": t["id"], "name_uz": t["name_uz"], "name_ru": t["name_ru"]} for t in raw_themes]
    questions = [{
        "id": q["id"],
        "question_uz": q["question_uz"],
        "question_ru": q["question_ru"],
        "image_url": q["image_url"],
        "is_active": q["is_active"],
        "theme_id": q["theme_id"],
    } for q in raw_questions]
    options = [{
        "id": o["id"],
        "text_uz": o["text_uz"],
        "text_ru": o["text_ru"],
        "is_correct": o["is_correct"],
        "quiz_id": o["quiz_id"],
    } for o in raw_options]

    return themes, questions, options


def save_json(data, path: Path, label: str):
    """Save data as formatted JSON."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(f"  Saved {label}: {path} ({len(data)} records)")


try:
    import requests as _requests
    import urllib3
    urllib3.disable_warnings()
    _SESSION = _requests.Session()
    _SESSION.headers.update({
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Accept": "image/avif,image/webp,image/apng,image/*,*/*;q=0.8",
    })
    HAS_REQUESTS = True
except ImportError:
    HAS_REQUESTS = False
    _SESSION = None


def download_image(url: str, dest: Path, retries: int = 3) -> bool:
    """Download image with retry + exponential backoff. Uses requests lib if available."""
    delays = [2, 4, 8]

    for attempt in range(retries):
        try:
            if HAS_REQUESTS:
                r = _SESSION.get(url, timeout=15, verify=False)
                content_type = r.headers.get("Content-Type", "")
                data = r.content

                # Check for JS challenge / block pages
                if r.status_code == 403 or (r.status_code == 200 and "text/html" in content_type):
                    if attempt < retries - 1:
                        time.sleep(delays[attempt])
                        continue
                    return False

                if r.status_code != 200 or len(data) < 100:
                    if attempt < retries - 1:
                        time.sleep(delays[attempt])
                        continue
                    return False
            else:
                headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"}
                req = urllib.request.Request(url, headers=headers)
                with urllib.request.urlopen(req, timeout=15, context=SSL_CTX) as response:
                    content_type = response.headers.get("Content-Type", "")
                    data = response.read()

                if len(data) < 100:
                    if attempt < retries - 1:
                        time.sleep(delays[attempt])
                        continue
                    return False

            # Determine extension from content type or URL
            ext = ".jpg"
            if "png" in content_type or url.endswith(".png"):
                ext = ".png"
            elif "webp" in content_type or url.endswith(".webp"):
                ext = ".webp"

            final_dest = dest.with_suffix(ext)
            with open(final_dest, "wb") as f:
                f.write(data)
            return True

        except Exception:
            if attempt < retries - 1:
                time.sleep(delays[attempt])
            else:
                return False

    return False


def download_all_images(questions: list, skip: bool = False):
    """Download all external images for questions that have image_url."""
    AVTOLIDER_IMG_DIR.mkdir(parents=True, exist_ok=True)

    urls_to_download = []
    for q in questions:
        if q.get("image_url"):
            qid = q["id"]
            # Check if already downloaded (any extension)
            existing = list(AVTOLIDER_IMG_DIR.glob(f"{qid}.*"))
            if existing:
                continue
            urls_to_download.append((qid, q["image_url"]))

    if not urls_to_download:
        print(f"  All {sum(1 for q in questions if q.get('image_url'))} images already downloaded.")
        return

    if skip:
        print(f"  Skipping download of {len(urls_to_download)} images (--skip-download)")
        return

    total = len(urls_to_download)
    print(f"  Downloading {total} images (retry 3x with backoff)...")

    success = 0
    failed = []

    for i, (qid, url) in enumerate(urls_to_download):
        dest = AVTOLIDER_IMG_DIR / str(qid)  # extension added by download_image
        ok = download_image(url, dest)

        if ok:
            success += 1
        else:
            failed.append((qid, url))

        # Progress every 50
        if (i + 1) % 50 == 0 or i == total - 1:
            print(f"    Progress: {i + 1}/{total} ({success} ok, {len(failed)} failed)")

    print(f"  Download complete: {success} succeeded, {len(failed)} failed")

    if failed:
        failed_path = AVTOLIDER_DIR / "failed_images.txt"
        with open(failed_path, "w") as f:
            for qid, url in failed:
                f.write(f"{qid}\t{url}\n")
        print(f"  Failed URLs written to: {failed_path}")


def copy_visual_assets(atp_path: Path):
    """Copy road signs, markings, hazard, first aid from Avto Test PRO extract."""
    if not atp_path.exists():
        print(f"  [WARN] Avto Test PRO assets not found at: {atp_path}")
        print(f"  Run: unzip -o 'Avto Test PRO.apk' -d /tmp/atp_extract/")
        return

    copies = [
        ("images/belgilar/axborot", "signs/axborot"),
        ("images/belgilar/buyuruvchi", "signs/buyuruvchi"),
        ("images/belgilar/imtiyozli", "signs/imtiyozli"),
        ("images/belgilar/ogohlantiruvchi", "signs/ogohlantiruvchi"),
        ("images/belgilar/qoshimcha", "signs/qoshimcha"),
        ("images/belgilar/servis", "signs/servis"),
        ("images/belgilar/taqiqlovchi", "signs/taqiqlovchi"),
        ("images/chiziq/yotiq", "markings/horizontal"),
        ("images/chiziq/tik", "markings/vertical"),
        ("images/xavli/xavli", "hazard"),
        ("images/yordam", "first_aid"),
    ]

    total_copied = 0
    for src_rel, dst_rel in copies:
        src = atp_path / src_rel
        dst = VISUAL_ASSETS_DIR / dst_rel
        if not src.exists():
            print(f"  [WARN] Source not found: {src}")
            continue

        dst.mkdir(parents=True, exist_ok=True)
        count = 0
        for f in src.iterdir():
            if f.is_file():
                shutil.copy2(f, dst / f.name)
                count += 1
        total_copied += count
        print(f"    {dst_rel}: {count} files")

    # Also copy question images from img/ directory
    img_src = atp_path / "img"
    if img_src.exists():
        img_dst = VISUAL_ASSETS_DIR / "question_images_pro"
        img_dst.mkdir(parents=True, exist_ok=True)
        count = 0
        for f in img_src.iterdir():
            if f.is_file():
                shutil.copy2(f, img_dst / f.name)
                count += 1
        total_copied += count
        print(f"    question_images_pro: {count} files")

    print(f"  Visual assets copied: {total_copied} total")


def verify_data():
    """Verify all required data files are in place."""
    print("\n" + "=" * 60)
    print("VERIFICATION")
    print("=" * 60)

    checks = [
        (AVTOLIDER_DIR / "themes.json", "Avtolider themes"),
        (AVTOLIDER_DIR / "questions.json", "Avtolider questions"),
        (AVTOLIDER_DIR / "options.json", "Avtolider options"),
        (DATA_DIR / "apk" / "uzkiril.json", "APK UZ Kiril"),
        (DATA_DIR / "apk" / "uzlotin.json", "APK UZ Lotin"),
        (DATA_DIR / "apk" / "rus.json", "APK Russian"),
    ]

    all_ok = True
    for path, label in checks:
        if path.exists():
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            print(f"  [OK]   {label}: {len(data)} records")
        else:
            print(f"  [MISS] {label}: {path}")
            all_ok = False

    # Check images
    img_dir = AVTOLIDER_DIR / "img"
    if img_dir.exists():
        img_count = len(list(img_dir.glob("*.*")))
        print(f"  [OK]   Avtolider images: {img_count} files")
    else:
        print(f"  [MISS] Avtolider images directory")
        all_ok = False

    apk_img_dir = DATA_DIR / "apk" / "img"
    if apk_img_dir.exists():
        apk_img_count = len(list(apk_img_dir.glob("*.png")))
        print(f"  [OK]   APK images: {apk_img_count} PNG files")
    else:
        print(f"  [MISS] APK images directory")
        all_ok = False

    # Visual assets
    if VISUAL_ASSETS_DIR.exists():
        va_count = sum(1 for _ in VISUAL_ASSETS_DIR.rglob("*") if _.is_file())
        print(f"  [OK]   Visual assets: {va_count} files")
    else:
        print(f"  [INFO] Visual assets not copied (optional)")

    print()
    if all_ok:
        print("  ALL REQUIRED DATA PRESENT. Ready for: dotnet run -- import-all")
    else:
        print("  MISSING DATA — check above warnings")

    return all_ok


def main():
    parser = argparse.ArgumentParser(description="Prepare data for Avtolider migration")
    parser.add_argument("--dump-sql", help="Path to PostgreSQL plain-text dump (pg_restore -f output)")
    parser.add_argument("--from-json", action="store_true", help="Use pre-extracted JSON from $USERPROFILE")
    parser.add_argument("--skip-download", action="store_true", help="Skip image downloads")
    parser.add_argument("--skip-visual", action="store_true", help="Skip visual assets copy")
    parser.add_argument("--atp-path", default=str(ATP_ASSETS_DEFAULT),
                        help="Path to extracted Avto Test PRO assets")
    parser.add_argument("--verify-only", action="store_true", help="Only verify existing data")
    args = parser.parse_args()

    print("=" * 60)
    print("AVTOLIDER DATA PREPARATION")
    print("=" * 60)
    print(f"  Output: {DATA_DIR}")
    print()

    if args.verify_only:
        verify_data()
        return

    # Step 1: Extract/load question data
    print("STEP 1: Extract question data from DB dump")
    print("-" * 40)

    if args.from_json:
        themes, questions, options = load_from_json()
    elif args.dump_sql:
        themes, questions, options = parse_dump_sql(args.dump_sql)
    else:
        # Try pre-extracted JSON first, fallback to dump
        json_path = HOME / "db_questions.json"
        if json_path.exists():
            print("  Auto-detected pre-extracted JSON files")
            themes, questions, options = load_from_json()
        else:
            print("  [ERROR] No data source specified. Use --dump-sql or --from-json")
            sys.exit(1)

    print(f"  Loaded: {len(themes)} themes, {len(questions)} questions, {len(options)} options")

    # Save to expected location
    save_json(themes, AVTOLIDER_DIR / "themes.json", "themes")
    save_json(questions, AVTOLIDER_DIR / "questions.json", "questions")
    save_json(options, AVTOLIDER_DIR / "options.json", "options")

    # Step 2: Download images
    print()
    print("STEP 2: Download question images")
    print("-" * 40)
    download_all_images(questions, skip=args.skip_download)

    # Step 3: Copy visual assets
    print()
    print("STEP 3: Copy visual assets from Avto Test PRO")
    print("-" * 40)
    if args.skip_visual:
        print("  Skipped (--skip-visual)")
    else:
        copy_visual_assets(Path(args.atp_path))

    # Verify
    verify_data()


if __name__ == "__main__":
    main()
