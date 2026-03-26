"""Final comprehensive verification of ALL migration data."""
import json, os, re, sys

DATA = os.path.join(
    r"C:\Users\ASirozhiddinov\Documents\Claude projects\Auto test",
    "autotest-platform", "backend", "tools", "Avtolider.DataMigration", "data"
)

errors = []
warnings = []

def check(condition, msg):
    if not condition:
        errors.append(f"FAIL: {msg}")
        return False
    return True

def warn(condition, msg):
    if not condition:
        warnings.append(f"WARN: {msg}")

def load_json(path):
    with open(os.path.join(DATA, path), encoding='utf-8') as f:
        return json.load(f)

print("=" * 65)
print("COMPREHENSIVE DATA VERIFICATION")
print("=" * 65)

# 1. Check all required files exist
print("\n1. FILE EXISTENCE:")
required_files = [
    "apk/uzkiril.json", "apk/uzlotin.json", "apk/rus.json",
    "avtolider/themes.json", "avtolider/questions.json", "avtolider/options.json",
]
for f in required_files:
    exists = os.path.exists(os.path.join(DATA, f))
    status = "OK" if exists else "MISSING"
    print(f"  [{status}] {f}")
    check(exists, f"Required file missing: {f}")

# 2. Load and validate APK data
print("\n2. APK DATA VALIDATION:")
apk_ru = load_json("apk/rus.json")
apk_uzk = load_json("apk/uzkiril.json")
apk_uzl = load_json("apk/uzlotin.json")

print(f"  APK Russian:  {len(apk_ru)} questions")
print(f"  APK UZ Kiril: {len(apk_uzk)} questions")
print(f"  APK UZ Latin: {len(apk_uzl)} questions")
check(len(apk_ru) == 700, f"APK Russian should be 700, got {len(apk_ru)}")
check(len(apk_uzk) == 700, f"APK UZ Kiril should be 700, got {len(apk_uzk)}")
check(len(apk_uzl) == 700, f"APK UZ Latin should be 700, got {len(apk_uzl)}")

# Validate each APK question has required fields
apk_invalid = 0
apk_no_correct = 0
for q in apk_ru:
    if not q.get('question', '').strip():
        apk_invalid += 1
    correct = sum(1 for c in q.get('choises', []) if c.get('answer'))
    if correct == 0:
        apk_no_correct += 1
print(f"  Empty questions: {apk_invalid}")
print(f"  No correct answer: {apk_no_correct}")
warn(apk_no_correct <= 1, f"More than 1 question without correct answer: {apk_no_correct}")

# APK images
apk_img_dir = os.path.join(DATA, "apk", "img")
apk_imgs = [f for f in os.listdir(apk_img_dir) if f.endswith('.png')]
apk_with_media = sum(1 for q in apk_uzk if q.get('media', {}).get('exist'))
print(f"  APK images: {len(apk_imgs)} files, {apk_with_media} questions reference images")

# 3. Validate Avtolider data
print("\n3. AVTOLIDER DATA VALIDATION:")
avt_themes = load_json("avtolider/themes.json")
avt_questions = load_json("avtolider/questions.json")
avt_options = load_json("avtolider/options.json")

print(f"  Themes:    {len(avt_themes)}")
print(f"  Questions: {len(avt_questions)}")
print(f"  Options:   {len(avt_options)}")
check(len(avt_themes) == 28, f"Themes should be 28, got {len(avt_themes)}")
check(len(avt_questions) == 1044, f"Questions should be 1044, got {len(avt_questions)}")

# Validate theme IDs referenced by questions
theme_ids = {t['id'] for t in avt_themes}
orphan_themes = set()
for q in avt_questions:
    if q['theme_id'] not in theme_ids:
        orphan_themes.add(q['theme_id'])
print(f"  Orphan theme references: {len(orphan_themes)}")
check(len(orphan_themes) == 0, f"Questions reference unknown themes: {orphan_themes}")

# Validate options reference valid questions
question_ids = {q['id'] for q in avt_questions}
orphan_options = sum(1 for o in avt_options if o['quiz_id'] not in question_ids)
print(f"  Orphan option references: {orphan_options}")
warn(orphan_options == 0, f"{orphan_options} options reference non-existent questions")

# Check each question has at least one correct option
opts_by_q = {}
for o in avt_options:
    opts_by_q.setdefault(o['quiz_id'], []).append(o)

no_opts = sum(1 for q in avt_questions if q['id'] not in opts_by_q)
no_correct = sum(1 for qid, opts in opts_by_q.items() if not any(o['is_correct'] for o in opts))
print(f"  Questions without options: {no_opts}")
print(f"  Questions without correct answer: {no_correct}")

# Avtolider images
avt_img_dir = os.path.join(DATA, "avtolider", "img")
avt_imgs = os.listdir(avt_img_dir)
avt_with_url = sum(1 for q in avt_questions if q.get('image_url'))
avt_img_ids = {os.path.splitext(f)[0] for f in avt_imgs}
avt_missing_imgs = sum(1 for q in avt_questions if q.get('image_url') and str(q['id']) not in avt_img_ids)
print(f"  Image URLs: {avt_with_url}")
print(f"  Downloaded: {len(avt_imgs)}")
print(f"  Missing:    {avt_missing_imgs}")
check(avt_missing_imgs == 0, f"{avt_missing_imgs} images missing from disk")

# Check image file sizes (corrupted downloads = 0 bytes)
zero_size = 0
small_files = 0
for f in avt_imgs:
    size = os.path.getsize(os.path.join(avt_img_dir, f))
    if size == 0:
        zero_size += 1
    elif size < 500:
        small_files += 1
print(f"  Zero-byte images: {zero_size}")
print(f"  Suspiciously small (<500B): {small_files}")
check(zero_size == 0, f"{zero_size} images are 0 bytes (corrupted)")
warn(small_files == 0, f"{small_files} images are suspiciously small")

# 4. Visual assets
print("\n4. VISUAL ASSETS:")
va_dir = os.path.join(DATA, "visual_assets")
if os.path.exists(va_dir):
    va_count = sum(len(files) for _, _, files in os.walk(va_dir))
    print(f"  Total visual asset files: {va_count}")
    for subdir in sorted(os.listdir(va_dir)):
        sub_path = os.path.join(va_dir, subdir)
        if os.path.isdir(sub_path):
            sub_count = sum(len(files) for _, _, files in os.walk(sub_path))
            print(f"    {subdir}/: {sub_count} files")
else:
    print("  [SKIP] visual_assets/ not found")

# 5. Cross-source matching preview
print("\n5. CROSS-SOURCE MATCHING (what dedup will see):")

def normalize(text):
    if not text: return ''
    t = text.strip().lower()
    t = re.sub(r'[^\w\s]', '', t)
    t = re.sub(r'\s+', ' ', t)
    return t.strip()

apk_norms = {}
for q in apk_ru:
    n = normalize(q['question'])
    if n: apk_norms[n] = q

avt_norms = {}
for q in avt_questions:
    n = normalize(q.get('question_ru', ''))
    if n: avt_norms[n] = q

exact_matches = sum(1 for n in apk_norms if n in avt_norms)
apk_unique = len(apk_norms) - exact_matches
avt_unique = len(avt_norms) - exact_matches
total_unique = exact_matches + apk_unique + avt_unique

print(f"  Exact text matches:    {exact_matches}")
print(f"  APK-only questions:    {apk_unique}")
print(f"  Avtolider-only:        {avt_unique}")
print(f"  EXPECTED TOTAL UNIQUE: {total_unique}")

# What dedup merge should produce
matched_with_image = 0
matched_with_expl = 0
matched_with_category = 0
for n in apk_norms:
    if n in avt_norms:
        apk_q = apk_norms[n]
        avt_q = avt_norms[n]
        # Image from either
        has_img = (apk_q.get('media', {}).get('exist') or bool(avt_q.get('image_url')))
        if has_img: matched_with_image += 1
        # Explanation from APK
        if apk_q.get('description', '').strip(): matched_with_expl += 1
        # Category from Avtolider (always)
        matched_with_category += 1

print(f"\n  After smart merge of {exact_matches} matched pairs:")
print(f"    With image:       {matched_with_image}")
print(f"    With explanation: {matched_with_expl}")
print(f"    With category:    {matched_with_category} (from Avtolider themes)")

# 6. Final summary
print("\n" + "=" * 65)
print("FINAL READINESS SUMMARY")
print("=" * 65)

# Count completeness after merge
total_with_image = matched_with_image
total_with_expl = matched_with_expl
total_with_category = matched_with_category

# APK-only
for n, q in apk_norms.items():
    if n not in avt_norms:
        if q.get('media', {}).get('exist'): total_with_image += 1
        if q.get('description', '').strip(): total_with_expl += 1
        # APK-only: goes through keyword assignment, some get category
        # Conservative: 30% keyword match

# Avtolider-only
for n, q in avt_norms.items():
    if n not in apk_norms:
        if q.get('image_url') and str(q['id']) in avt_img_ids: total_with_image += 1
        total_with_category += 1  # all have themes

print(f"""
  Total unique questions:  {total_unique}
  With image:              {total_with_image}/{total_unique} ({total_with_image*100//total_unique}%)
  With explanation:        {total_with_expl}/{total_unique} ({total_with_expl*100//total_unique}%)
  With themed category:    {total_with_category}/{total_unique} ({total_with_category*100//total_unique}%)
  Uncategorized (needs human): {total_unique - total_with_category}
""")

if errors:
    print("ERRORS:")
    for e in errors:
        print(f"  {e}")
    print(f"\n  STATUS: BLOCKED — {len(errors)} errors must be fixed")
    sys.exit(1)
elif warnings:
    print("WARNINGS:")
    for w in warnings:
        print(f"  {w}")
    print(f"\n  STATUS: READY (with {len(warnings)} minor warnings)")
else:
    print("  STATUS: ALL CHECKS PASSED — READY FOR import-all")
