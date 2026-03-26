import json, re, os

def load(p):
    with open(p, encoding='utf-8') as f: return json.load(f)

def normalize(text):
    if not text: return ''
    t = text.strip().lower()
    t = re.sub(r'[^\w\s]', '', t)
    t = re.sub(r'\s+', ' ', t)
    return t.strip()

DATA = "autotest-platform/backend/tools/Avtolider.DataMigration/data"

apk_ru = load(f'{DATA}/apk/rus.json')
apk_uzk = load(f'{DATA}/apk/uzkiril.json')
apk_uzl = load(f'{DATA}/apk/uzlotin.json')
avt_q = load(f'{DATA}/avtolider/questions.json')
avt_o = load(f'{DATA}/avtolider/options.json')
avt_t = load(f'{DATA}/avtolider/themes.json')

apk_ru_map = {q['id']: q for q in apk_ru}
apk_uzk_map = {q['id']: q for q in apk_uzk}
apk_uzl_map = {q['id']: q for q in apk_uzl}
avt_opts = {}
for o in avt_o:
    avt_opts.setdefault(o['quiz_id'], []).append(o)
theme_map = {t['id']: t for t in avt_t}

img_dir = f'{DATA}/apk/img'
apk_img_map = {}
for f in os.listdir(img_dir):
    if f.endswith('.png'):
        name = os.path.splitext(f)[0]
        dot = name.find('.')
        key = name[:dot] if dot >= 0 else name
        if key not in apk_img_map:
            apk_img_map[key] = f

avt_img_dir = f'{DATA}/avtolider/img'
avt_img_files = {os.path.splitext(f)[0] for f in os.listdir(avt_img_dir)}

# Cross-match
apk_norm = {}
for q in apk_ru:
    n = normalize(q['question'])
    if n: apk_norm[n] = q

avt_norm = {}
for q in avt_q:
    n = normalize(q.get('question_ru', ''))
    if n: avt_norm[n] = q

matched_apk = set()
matched_avt = set()
matches = []

for norm, apk_q in apk_norm.items():
    if norm in avt_norm:
        matches.append((apk_q, avt_norm[norm]))
        matched_apk.add(apk_q['id'])
        matched_avt.add(avt_norm[norm]['id'])

apk_only = [q for q in apk_ru if q['id'] not in matched_apk and q['id'] != 156]
avt_only = [q for q in avt_q if q['id'] not in matched_avt]

total_unique = len(matches) + len(apk_only) + len(avt_only)

print('=' * 70)
print('COMPLETE DATA GAP ANALYSIS REPORT')
print('=' * 70)
print()
print(f'Total unique questions after merge: {total_unique}')
print(f'  Matched (both sources):  {len(matches)}')
print(f'  APK-only:                {len(apk_only)}')
print(f'  Avtolider-only:          {len(avt_only)}')
print()

# Build results
results = []

for apk_q, avt_q_item in matches:
    uzk = apk_uzk_map.get(apk_q['id'])
    uzl = apk_uzl_map.get(apk_q['id'])
    apk_has_img = (apk_q.get('media', {}).get('exist') and apk_q['media']['name'] in apk_img_map)
    avt_has_img = (avt_q_item.get('image_url') and str(avt_q_item['id']) in avt_img_files)
    results.append({
        'source': 'BOTH',
        'ru': True, 'uz': True, 'uzl': bool(uzl and uzl.get('question')),
        'expl_ru': bool(apk_q.get('description', '').strip()),
        'expl_uz': bool(uzk and uzk.get('description', '').strip()),
        'expl_uzl': bool(uzl and uzl.get('description', '').strip()),
        'image': apk_has_img or avt_has_img,
        'category': True,
        'apk_score': (2 if apk_has_img else 0) + (1 if apk_q.get('description', '').strip() else 0),
        'avt_score': (2 if avt_has_img else 0),
    })

for q in apk_only:
    uzk = apk_uzk_map.get(q['id'])
    uzl = apk_uzl_map.get(q['id'])
    has_img = (q.get('media', {}).get('exist') and q['media']['name'] in apk_img_map)
    results.append({
        'source': 'APK',
        'ru': True, 'uz': True, 'uzl': bool(uzl and uzl.get('question')),
        'expl_ru': bool(q.get('description', '').strip()),
        'expl_uz': bool(uzk and uzk.get('description', '').strip()),
        'expl_uzl': bool(uzl and uzl.get('description', '').strip()),
        'image': has_img, 'category': False,
    })

for q in avt_only:
    has_img = (q.get('image_url') and str(q['id']) in avt_img_files)
    results.append({
        'source': 'AVT',
        'ru': bool(q.get('question_ru', '').strip()),
        'uz': bool(q.get('question_uz', '').strip()),
        'uzl': False,  # auto-transliterated, not native
        'expl_ru': False, 'expl_uz': False, 'expl_uzl': False,
        'image': has_img, 'category': True,
    })

# Completeness matrix
print('COMPLETENESS MATRIX:')
print(f'{"Field":<25s} {"OK":>6s} {"MISSING":>8s} {"Pct":>5s}')
print('-' * 48)
for label, key in [
    ('Russian text', 'ru'), ('Uzbek Cyrillic text', 'uz'), ('Uzbek Latin text', 'uzl'),
    ('Explanation (RU)', 'expl_ru'), ('Explanation (UZ Cyr)', 'expl_uz'),
    ('Explanation (UZ Lat)', 'expl_uzl'), ('Image', 'image'), ('Category (themed)', 'category'),
]:
    ok = sum(1 for r in results if r[key])
    miss = len(results) - ok
    pct = ok * 100 // len(results)
    marker = ' <<<' if miss > 0 and key not in ('image',) else ''
    print(f'{label:<25s} {ok:>6d} {miss:>8d} {pct:>4d}%{marker}')

# Dedup analysis
print()
print('=' * 70)
print('DEDUP WINNER ANALYSIS (for matched questions)')
print('=' * 70)
both = [r for r in results if r['source'] == 'BOTH']
apk_wins = sum(1 for r in both if r['apk_score'] >= r['avt_score'])
avt_wins = sum(1 for r in both if r['apk_score'] < r['avt_score'])
print(f'''
  {len(both)} matched pairs:
    APK wins dedup: {apk_wins}  (has explanation, often no image)
    AVT wins dedup: {avt_wins}  (has image, no explanation)

  After dedup + merge-explanations:
    AVT winners get explanation backfilled from APK: ~{avt_wins} fixed
    APK winners LOSE Avtolider category: {apk_wins} need recategorization

  CURRENT keyword matching recovers ~30% of uncategorized.
  REMAINING UNCATEGORIZED: ~{int(apk_wins * 0.7) + len(apk_only)} questions
''')

# The real gaps
no_expl = [r for r in results if not r['expl_ru']]
no_uzl_native = [r for r in results if not r['uzl']]
no_cat = [r for r in results if not r['category']]

print('=' * 70)
print('ACTION ITEMS')
print('=' * 70)
print(f'''
GAP 1: {len(no_expl)} questions have NO explanation at all
  - {len(avt_only)} Avtolider-only (never had explanations in source DB)
  - {sum(1 for r in results if r["source"]=="APK" and not r["expl_ru"])} APK questions without description
  - {sum(1 for r in results if r["source"]=="BOTH" and not r["expl_ru"])} matched questions without description

  FIX: Generate explanations using AI/translation model for Russian,
       then transliterate to UZ Cyrillic + UZ Latin.

GAP 2: {len(no_uzl_native)} questions have auto-transliterated UZ Latin (not human-written)
  - All Avtolider-only questions use Cyrillic-to-Latin transliteration
  - Quality is ~95% (simple character mapping)
  - This is ACCEPTABLE for MVP — Uzbek Latin/Cyrillic is a direct mapping

GAP 3: {len(no_cat)} questions have NO proper category
  - {len(apk_only)} APK-only questions (never in Avtolider themes)
  - After dedup, ~{apk_wins} matched questions where APK wins also lose category

  FIX OPTIONS:
  a) Improve dedup to MERGE best fields from both versions (not just keep one)
  b) During dedup, when APK wins, copy CategoryId from AVT version
  c) Use AI/LLM categorization instead of keyword matching

GAP 4: Explanation translations (UZ Cyrillic + UZ Latin)
  Even questions WITH Russian explanations may be missing UZ translations.
  - Missing UZ Cyrillic explanation: {sum(1 for r in results if r["expl_ru"] and not r["expl_uz"])}
  - Missing UZ Latin explanation: {sum(1 for r in results if r["expl_ru"] and not r["expl_uzl"])}

  FIX: Translate Russian explanations to Uzbek.
''')

# Sample AVT-only questions without explanations
print('SAMPLE: Avtolider-only questions needing explanations:')
for q in avt_only[:5]:
    ru = q.get('question_ru', '')[:80]
    print(f'  Q#{q["id"]}: {ru}')

print()
print('SAMPLE: APK-only questions needing categories:')
for q in apk_only[:5]:
    print(f'  Q#{q["id"]}: {q["question"][:80]}')
