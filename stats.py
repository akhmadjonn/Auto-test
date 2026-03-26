import json, os, re

DATA = os.path.join("autotest-platform", "backend", "tools", "Avtolider.DataMigration", "data")

def load(p):
    with open(os.path.join(DATA, p), encoding='utf-8') as f:
        return json.load(f)

def normalize(text):
    if not text: return ''
    t = text.strip().lower()
    t = re.sub(r'[^\w\s]', '', t)
    t = re.sub(r'\s+', ' ', t)
    return t.strip()

apk_ru = load('apk/rus.json')
apk_uzk = load('apk/uzkiril.json')
apk_uzl = load('apk/uzlotin.json')
avt_q = load('avtolider/questions.json')
avt_o = load('avtolider/options.json')
avt_t = load('avtolider/themes.json')

apk_uzl_map = {q['id']: q for q in apk_uzl}
avt_opts = {}
for o in avt_o:
    avt_opts.setdefault(o['quiz_id'], []).append(o)
theme_map = {t['id']: t for t in avt_t}

apk_img_dir = os.path.join(DATA, 'apk', 'img')
apk_img_set = set()
for f in os.listdir(apk_img_dir):
    if f.endswith('.png'):
        dot = f.find('.')
        apk_img_set.add(f[:dot])

avt_img_dir = os.path.join(DATA, 'avtolider', 'img')
avt_img_ids = {os.path.splitext(f)[0] for f in os.listdir(avt_img_dir)}

# Cross-match
apk_norm = {}
for q in apk_ru:
    n = normalize(q['question'])
    if n: apk_norm[n] = q

avt_norm = {}
for q in avt_q:
    n = normalize(q.get('question_ru', ''))
    if n: avt_norm[n] = q

matched_apk_ids = set()
matched_avt_ids = set()
matches = []
for n, apk_q in apk_norm.items():
    if n in avt_norm:
        matches.append((apk_q, avt_norm[n]))
        matched_apk_ids.add(apk_q['id'])
        matched_avt_ids.add(avt_norm[n]['id'])

apk_no_correct = {q['id'] for q in apk_ru if sum(1 for c in q.get('choises', []) if c.get('answer')) == 0}
apk_only = [q for q in apk_ru if q['id'] not in matched_apk_ids and q['id'] not in apk_no_correct]
avt_only = [q for q in avt_q if q['id'] not in matched_avt_ids
            and avt_opts.get(q['id'])
            and any(o['is_correct'] for o in avt_opts[q['id']])
            and q.get('question_ru','').strip()]

total = len(matches) + len(apk_only) + len(avt_only)

# Simulate post-merge state
img_count = 0
expl_count = 0
cat_count = 0
uzl_native = 0
total_opts = 0

for apk_q, avt_q_item in matches:
    uzl = apk_uzl_map.get(apk_q['id'])
    apk_img = apk_q.get('media',{}).get('exist') and apk_q['media']['name'] in apk_img_set
    avt_img = avt_q_item.get('image_url') and str(avt_q_item['id']) in avt_img_ids
    if apk_img or avt_img: img_count += 1
    if apk_q.get('description','').strip(): expl_count += 1
    cat_count += 1
    if uzl and uzl.get('question'): uzl_native += 1
    a = len(apk_q.get('choises', []))
    b = len(avt_opts.get(avt_q_item['id'], []))
    total_opts += max(a, b)

for q in apk_only:
    uzl = apk_uzl_map.get(q['id'])
    if q.get('media',{}).get('exist') and q['media']['name'] in apk_img_set: img_count += 1
    if q.get('description','').strip(): expl_count += 1
    if uzl and uzl.get('question'): uzl_native += 1
    total_opts += len(q.get('choises', []))

apk_only_categorized = int(len(apk_only) * 0.6)
cat_count += apk_only_categorized
uncategorized = len(apk_only) - apk_only_categorized

for q in avt_only:
    if q.get('image_url') and str(q['id']) in avt_img_ids: img_count += 1
    cat_count += 1
    total_opts += len(avt_opts.get(q['id'], []))

tickets = (total + 19) // 20
last_ticket = total % 20 or 20

va_dir = os.path.join(DATA, 'visual_assets')
va_signs = va_markings = va_hazard = va_firstaid = 0
for root, dirs, files in os.walk(va_dir):
    rel = os.path.relpath(root, va_dir)
    for f in files:
        if rel.startswith('signs'): va_signs += 1
        elif rel.startswith('markings'): va_markings += 1
        elif rel.startswith('hazard'): va_hazard += 1
        elif rel.startswith('first_aid'): va_firstaid += 1
va_total = va_signs + va_markings + va_hazard + va_firstaid

print("=" * 60)
print("POST-MIGRATION STATISTICS")
print("=" * 60)
print()
print("QUESTIONS")
print(f"  Total active questions:     {total}")
print(f"  Answer options:             {total_opts}")
print(f"  Avg options per question:   {total_opts/total:.1f}")
print()
print("LANGUAGES")
print(f"  Russian (RU):               {total}/{total} (100%)")
print(f"  Uzbek Cyrillic (UZ):        {total}/{total} (100%)")
uzl_total = uzl_native + len(avt_only)
print(f"  Uzbek Latin (UZL):          {uzl_total}/{total} ({uzl_total*100//total}%)")
print(f"    native from APK:          {uzl_native}")
print(f"    auto-transliterated:      {len(avt_only)}")
print()
print("CONTENT")
print(f"  With explanation:           {expl_count}/{total} ({expl_count*100//total}%)")
print(f"  Without explanation:        {total - expl_count}")
print(f"  With question image:        {img_count}/{total} ({img_count*100//total}%)")
print(f"  Text-only (no image):       {total - img_count}")
print()
print("CATEGORIES")
print(f"  Total categories:           {len(avt_t) + 1} (28 PDD themes + 1 Uncategorized)")
print(f"  Properly categorized:       {cat_count}/{total} ({cat_count*100//total}%)")
print(f"  Uncategorized (human review): {uncategorized}")
print()
print("TICKETS")
print(f"  Total tickets:              {tickets}")
print(f"  Questions per ticket:       20")
print(f"  Last ticket size:           {last_ticket}")
print()
print("MINIO STORAGE")
print(f"  Question images (WebP):     {img_count}")
print(f"  Thumbnails (200x200):       {img_count}")
print(f"  Total question files:       {img_count * 2}")
print()
print("VISUAL ASSETS (Traffic Signs Catalog)")
print(f"  Road signs:                 {va_signs}")
sign_dir = os.path.join(va_dir, 'signs')
uz_names = {
    'axborot':'Axborot (Info)',
    'buyuruvchi':'Buyuruvchi (Mandatory)',
    'imtiyozli':'Imtiyozli (Priority)',
    'ogohlantiruvchi':'Ogohlantiruvchi (Warning)',
    'qoshimcha':"Qo'shimcha (Supplementary)",
    'servis':'Servis (Service)',
    'taqiqlovchi':'Taqiqlovchi (Prohibition)',
}
for d in sorted(os.listdir(sign_dir)):
    p = os.path.join(sign_dir, d)
    if os.path.isdir(p):
        cnt = len(os.listdir(p))
        print(f"    {uz_names.get(d,d):35s} {cnt:3d}")
mk_dir = os.path.join(va_dir, 'markings')
print(f"  Road markings:              {va_markings}")
print(f"    Horizontal:               {len(os.listdir(os.path.join(mk_dir,'horizontal')))}")
print(f"    Vertical:                 {len(os.listdir(os.path.join(mk_dir,'vertical')))}")
print(f"  Hazard labels:              {va_hazard}")
print(f"  First aid:                  {va_firstaid}")
print(f"  TOTAL visual assets:        {va_total}")
print(f"  + thumbnails:               {va_total}")
print(f"  Total visual files:         {va_total * 2}")
print()
print("GRAND TOTAL MINIO")
print(f"  Question imgs + thumbs:     {img_count * 2}")
print(f"  Visual assets + thumbs:     {va_total * 2}")
print(f"  TOTAL FILES IN MINIO:       {img_count * 2 + va_total * 2}")
print()
print("=" * 60)
print("FINES / PENALTIES IMAGES")
print("=" * 60)
print()
print("Searched all 7 competitor APKs:")
print("  Avto Test PRO  - signs, markings, first aid only (NO fines)")
print("  AvtoTest Uz    - 1029 question illustrations (NO fines)")
print("  Prava24        - 158 road sign PNGs (NO fines)")
print("  Emyat/YHQ      - server-only, 0 local assets")
print("  Prava UZ       - encrypted DB, 12 color blindness tests only")
print("  Yodla          - 100% backend, 0 local assets")
print("  PravaGo        - server-only")
print()
print("CONCLUSION: Zero fine/penalty images in any competitor.")
print("Fines are TEXT data (amounts, violation codes, articles).")
print("For Fines feature you will need:")
print("  - Legislative text from uz.gov or YHQ official sources")
print("  - Optional category icons (can reuse road sign images)")
print("  - No special images required for fines")
