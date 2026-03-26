import json, os, time, sys

try:
    import requests
    import urllib3
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
except ImportError:
    print("ERROR: 'requests' package required. Run: pip install requests")
    sys.exit(1)

DATA = "autotest-platform/backend/tools/Avtolider.DataMigration/data/avtolider"
MISSING_FILE = os.path.join(DATA, "telegra_missing.json")
IMG_DIR = os.path.join(DATA, "img")

with open(MISSING_FILE, encoding="utf-8") as f:
    missing = json.load(f)

print(f"Downloading {len(missing)} missing telegra.ph images...")
print(f"Output: {IMG_DIR}")
print()

headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"}
downloaded = 0
failed = []

for i, item in enumerate(missing):
    qid = item["id"]
    url = item["url"]

    # Check if already downloaded (from a previous partial run)
    existing = [f for f in os.listdir(IMG_DIR) if os.path.splitext(f)[0] == str(qid)]
    if existing:
        downloaded += 1
        continue

    # Retry 3x with backoff
    success = False
    for attempt in range(3):
        try:
            r = requests.get(url, timeout=15, verify=False, headers=headers)
            if r.status_code == 200 and len(r.content) > 500:
                ct = r.headers.get("content-type", "image/jpeg")
                ext = ".jpg"
                if "png" in ct:
                    ext = ".png"
                elif "webp" in ct:
                    ext = ".webp"

                path = os.path.join(IMG_DIR, f"{qid}{ext}")
                with open(path, "wb") as out:
                    out.write(r.content)
                downloaded += 1
                success = True
                break
            else:
                if attempt < 2:
                    time.sleep(2 ** (attempt + 1))
        except Exception as e:
            if attempt < 2:
                time.sleep(2 ** (attempt + 1))

    if not success:
        failed.append(item)

    # Progress
    if (i + 1) % 10 == 0 or i == len(missing) - 1:
        print(f"  [{i+1}/{len(missing)}] downloaded={downloaded}, failed={len(failed)}")

print()
print(f"DONE: {downloaded} downloaded, {len(failed)} failed")

if failed:
    fail_path = os.path.join(DATA, "still_missing.json")
    with open(fail_path, "w", encoding="utf-8") as f:
        json.dump(failed, f, ensure_ascii=False, indent=2)
    print(f"Failed URLs saved to: {fail_path}")
    for item in failed:
        print(f"  Q#{item['id']}: {item['url']}")

# Final count
total = len(os.listdir(IMG_DIR))
print(f"\nTotal images in {IMG_DIR}: {total}")
