"""Build lore records from parsed sagaofgu.com pages.

Usage: python tools/build_from_sagaofgu.py <parsed_dir> [--sections gu,characters,...]

<parsed_dir> is the output of tools/parse_sagaofgu.py (one JSON per cached page).
The raw HTML cache is not committed; re-crawl https://sagaofgu.com/sitemap-0.xml to rebuild it.
Existing records with research.status "reviewed" or "verified" are never overwritten.
"""
import json
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = 'src_sagaofgu'
SCHEMA_VERSION = '1.0'
TODAY = '2026-10-09'

# sagaofgu section -> (id prefix, output dir, entity_type)
SECTIONS = {
    'gu': ('gu_', 'gu', 'gu'),
    'characters': ('char_', 'characters', 'character'),
    'recipes': ('rcp_', 'recipes', 'recipe'),
    'factions': ('fac_', 'factions', 'faction'),
    'locations': ('loc_', 'locations', 'location'),
    'regions': ('reg_', 'regions', 'region'),
    'earths': ('reg_earth_', 'regions', 'region'),
    'heavens': ('reg_heaven_', 'regions', 'region'),
    'materials': ('res_', 'resources', 'resource'),
    'moves': ('move_', 'moves', 'move'),
    'cultivation': ('sys_', 'systems', 'system'),
    'inheritances': ('inh_', 'inheritances', 'inheritance'),
    'gu-houses': ('house_', 'gu_houses', 'gu_house'),
    'paths': ('path_', 'paths', 'path'),
    'races': ('race_', 'creatures', 'race'),
    'legends': ('evt_legend_', 'events', 'event'),
    'eras': ('evt_era_', 'events', 'event'),
    'arcs': ('evt_arc_', 'events', 'event'),
    'fights': ('evt_fight_', 'events', 'event'),
    'chapters': ('ch_', 'chapters', 'chapter'),
}

INFER_WORDS = re.compile(r'\b(suggests?|implies|implied|likely|probably|presumably|appears to|seems to|inferred|may have|might have|unclear|not stated)\b', re.I)
PRICE_RE = re.compile(r'(\d[\d,.]*\s*(thousand|million|hundred)?\s*(primeval stones?|immortal essence stones?|stones?)|price[sd]?\b|auction|sold for|bought for|costs? \d|worth \d|bid)', re.I)

GU_TOPICS = [
    ('appearance', r'appearance|^form$|form-and-(rank|origin|effect)|^look|shape|name-form'),
    ('feeding', r'feed|food|diet|eat'),
    ('economy', r'price|auction|sale|shop|market|buy|sold|trade|cost-of|listing'),
    ('refinement', r'refine|recipe|fusion|fuse|material|evolv|upgrade|incomplete-recipe|reverse'),
    ('distribution', r'habitat|origin|where|wild|found|signature|harvest|extraction|clan-'),
    ('classification', r'^rank|^path|rank-and|series|place-among|-set$|types|line$|classics|naming'),
    ('abilities', r'effect|use|ability|activation|combat|killer|attack|defen|limit|cost|move|power|function|role|battle|drill|duel'),
]

PALLADIUS = None  # filled lazily from tools/palladius.py


def to_id(prefix, slug):
    s = re.sub(r'[^a-z0-9]+', '_', slug.lower()).strip('_')
    if prefix == 'ch_':
        return 'ch_%04d' % int(s)
    return prefix + s


def href_to_id(href):
    m = re.match(r'^/([a-z-]+)/([^/]+)/$', href or '')
    if not m or m.group(1) not in SECTIONS:
        return None
    return to_id(SECTIONS[m.group(1)][0], m.group(2))


def clean(t):
    t = re.sub(r'\s+([.,;:!?’”)])', r'\1', t)
    t = re.sub(r'([(“])\s+', r'\1', t)
    return re.sub(r'\s+', ' ', t).strip()


class Builder:
    def __init__(self, parsed_dir):
        self.parsed_dir = parsed_dir
        self.pages = {}
        for section in os.listdir(parsed_dir):
            d = os.path.join(parsed_dir, section)
            if section not in SECTIONS or not os.path.isdir(d):
                continue
            for f in os.listdir(d):
                with open(os.path.join(d, f), encoding='utf-8') as fh:
                    p = json.load(fh)
                self.pages[f'/{section}/{p["slug"]}/'] = p
        self.known_ids = {href_to_id(h) for h in self.pages}
        self.chapter_subjects = {}
        self.chapter_arc = {}
        for h, p in self.pages.items():
            if p['section'] == 'chapters':
                n = int(p['slug'])
                subj = set()
                for s in p['sections']:
                    for c in s['claims']:
                        subj.update(c['links'])
                for row in p['infobox'].values():
                    subj.update(row['links'])
                self.chapter_subjects[n] = subj
                arc = p['infobox'].get('Arc')
                if arc and arc['links']:
                    self.chapter_arc[n] = href_to_id(arc['links'][0])
        self.volumes = load_volumes()

    # ---- helpers -------------------------------------------------------
    def ref(self, href):
        i = href_to_id(href)
        return i if i in self.known_ids else None

    def verification(self, href, chapters):
        if not chapters:
            return 'no_citation'
        if not self.chapter_subjects:
            return 'citation_unchecked'
        for c in chapters:
            if href in self.chapter_subjects.get(c, ()):
                return 'cross_checked_chapter_summary'
        return 'citation_unchecked'

    def claim(self, page, c, section_id):
        text = clean(c['text'])
        chapters = sorted({x for x in c['chapters'] if 1 <= x <= 2334})
        inferred = bool(INFER_WORDS.search(text))
        if chapters and not inferred:
            certainty, conf = 'canon', 'B'
        elif chapters:
            certainty, conf = 'inferred', 'C'
        else:
            certainty, conf = 'inferred', 'C'
        out = {
            'text': text,
            'chapters': chapters,
            'certainty': certainty,
            'confidence': conf,
            'verification': self.verification(page_href(page), chapters),
            'source_id': SRC,
            'section': section_id,
            'text_origin': 'source_excerpt',
        }
        mentions = []
        for h in c['links']:
            i = self.ref(h)
            if i and i not in mentions:
                mentions.append(i)
        if mentions:
            out['mentions'] = mentions
        return out

    def first_chapter(self, page):
        row = page['infobox'].get('First chapter')
        if row:
            m = re.search(r'(\d+)', row['text'])
            if m and 1 <= int(m.group(1)) <= 2334:
                return int(m.group(1))
        return min(page['source_chapters']) if page['source_chapters'] else None

    def envelope(self, page, prefix, etype):
        rid = to_id(prefix, page['slug'])
        fc = self.first_chapter(page)
        all_claims = [c for s in page['sections'] for c in s['claims']]
        cited = sorted({x for c in all_claims for x in c['chapters'] if 1 <= x <= 2334} | {x for x in page['source_chapters'] if 1 <= x <= 2334})
        aliases = []
        aka = page['infobox'].get('Also known as')
        if aka:
            aliases = [clean(a) for a in re.split(r'\s*[;,]\s*|\s{2,}', aka['text']) if a.strip()] if not aka['items'] else [clean(i['text']) for i in aka['items']]
            aliases = [a for a in aliases if a and a != page['title']]
        conflicts = []
        for s in page['sections']:
            for cf in s['conflicts']:
                if len(cf['positions']) >= 2:
                    conflicts.append({
                        'topic': s['heading'],
                        'positions': [{'position': clean(p['position']), 'support': clean(p['support']),
                                       'chapters': sorted({int(x) for x in re.findall(r'\b(\d{1,4})\b', p['support']) if 1 <= int(x) <= 2334})}
                                      for p in cf['positions']],
                        'status': cf['status'], 'preferred': None, 'source_id': SRC,
                    })
        has_canon = any(c['chapters'] for c in all_claims)
        unverified_rows = [k for k, v in page['infobox'].items() if v['unverified']]
        if page['stub'] or not has_canon:
            canon_status, conf, status = 'unverified', 'C' if has_canon else 'D', 'stub'
        elif conflicts:
            canon_status, conf, status = 'partially_confirmed', 'B', 'extracted'
        else:
            canon_status, conf, status = 'confirmed', 'B', 'extracted'
        notes = []
        if unverified_rows:
            notes.append('Infobox fields marked unverified at source: ' + ', '.join(unverified_rows))
        rec = {
            'id': rid,
            'entity_type': etype,
            'schema_version': SCHEMA_VERSION,
            'names': {
                'en': page['title'] or page['slug'],
                'zh': page['zh'] or None,
                'zh_source': SRC if page['zh'] else None,
                'pinyin': None,
                'pinyin_status': None,
                'ru': None,
                'ru_status': 'unknown',
                'aliases': aliases,
                'translation_variants': [],
            },
            'summary': {'text': clean(page['lede']), 'text_origin': 'source_excerpt', 'source_id': SRC} if page['lede'] else None,
            'canon_status': canon_status,
            'confidence': conf,
            'first_appearance': {'chapter': fc, 'volume': self.volume_of(fc), 'arc': self.chapter_arc.get(fc) if fc else None},
            'chapters_cited': cited,
            'relations': [],
            'co_mentions': [],
            'claims': [],
            'conflicts': conflicts,
            'image': {'url': page['image'], 'kind': 'reference_site_art', 'canonical': False, 'source_id': SRC} if page['image'] else None,
            'sources': [{'source_id': SRC, 'url': page['url'], 'chapters': cited, 'accessed': TODAY, 'note': None}],
            'research': {'status': status, 'is_stub_at_source': bool(page['stub']), 'unresolved': [], 'notes': notes, 'last_updated': TODAY},
            'game_proposals': [],
        }
        seen = set()
        for c in page['connections']:
            i = self.ref(c['href'])
            if i and i != rid and i not in seen:
                seen.add(i)
                rec['co_mentions'].append({'target': i, 'count': max(1, c['count'])})
        return rec

    def volume_of(self, ch):
        if not ch:
            return None
        for v in self.volumes:
            if v['from'] <= ch <= v['to']:
                return v['volume']
        return None

    def infobox_relations(self, page, rid, skip=()):
        rels, attrs = [], {}
        for label, row in page['infobox'].items():
            if label in skip or label in ('First chapter', 'Also known as'):
                continue
            rtype = RELATION_TYPES.get(label, re.sub(r'[^a-z]+', '_', label.lower()).strip('_'))
            items = row['items'] or [{'text': row['text'], 'href': h, 'role': None, 'ch': None, 'from': None, 'to': None} for h in row['links']]
            linked = False
            for it in items:
                target = self.ref(it.get('href')) if it.get('href') else None
                if target and target != rid:
                    linked = True
                    rels.append({
                        'type': rtype, 'target': target, 'role': it.get('role'),
                        'from_chapter': valid_ch(it.get('from')), 'to_chapter': valid_ch(it.get('to')),
                        'recorded_chapter': valid_ch(it.get('ch')),
                        'count': None, 'confidence': 'D' if row['unverified'] else 'B', 'source_id': SRC, 'note': None,
                    })
            attrs[label] = {
                'text': clean(row['text']) if row['text'] and row['text'] != 'Unknown' else None,
                'unverified_at_source': row['unverified'],
                'linked': linked,
            }
        return rels, attrs

    # ---- type builders --------------------------------------------------
    def build_gu(self, page):
        rec = self.envelope(page, 'gu_', 'gu')
        rid = rec['id']
        ib = page['infobox']
        topics = defaultdict(list)
        for s in page['sections']:
            topic = gu_topic(s['id'])
            for c in s['claims']:
                cl = self.claim(page, c, s['id'])
                t = topic
                if t in ('history', 'abilities', 'refinement') and PRICE_RE.search(cl['text']):
                    cl['needs_review'] = True
                    topics['economy'].append(cl)
                    continue
                topics[t].append(cl)
        ranks = []
        rank_row = ib.get('Rank')
        rank_type = 'unknown'
        if rank_row:
            for it in rank_row['items']:
                if 'rank' in it and not any(r['rank'] == it['rank'] for r in ranks):
                    ranks.append({'rank': it['rank'], 'recorded_chapter': valid_ch(it.get('ch')), 'confidence': 'D' if rank_row['unverified'] else 'B'})
                    if it.get('rank_tone') in ('mortal', 'immortal'):
                        rank_type = 'immortal' if it['rank_tone'] == 'immortal' or rank_type == 'immortal' else 'mortal'
        imm = ib.get('Immortal Gu')
        is_imm = None
        if imm:
            is_imm = imm['text'].strip().lower().startswith('yes')
            if is_imm:
                rank_type = 'immortal'
        if rank_type == 'unknown' and ranks:
            rank_type = 'immortal' if min(r['rank'] for r in ranks) >= 6 else 'mortal'
        path_row = ib.get('Path')
        paths = [self.ref(h) for h in (path_row['links'] if path_row else [])]
        paths = [p for p in paths if p]
        eff = ib.get('Effect type')
        food = ib.get('Food')
        act = ib.get('Activation')
        mats = ib.get('Refinement materials')
        foods = [self.ref(h) for h in (food['links'] if food else [])]
        foods = [f for f in foods if f]
        rels, attrs = self.infobox_relations(page, rid, skip=('Rank', 'Known owners'))
        owners = []
        ko = ib.get('Known owners')
        if ko:
            for it in ko['items']:
                t = self.ref(it.get('href')) if it.get('href') else None
                if t:
                    owners.append({'character': t, 'role': it.get('role') or 'unspecified',
                                   'recorded_chapter': valid_ch(it.get('ch')),
                                   'from_chapter': valid_ch(it.get('from')), 'to_chapter': valid_ch(it.get('to')),
                                   'confidence': 'D' if ko['unverified'] else 'B', 'source_id': SRC})
                    rels.append({'type': 'held_by', 'target': t, 'role': it.get('role'),
                                 'from_chapter': valid_ch(it.get('from')), 'to_chapter': valid_ch(it.get('to')),
                                 'recorded_chapter': valid_ch(it.get('ch')), 'count': None,
                                 'confidence': 'D' if ko['unverified'] else 'B', 'source_id': SRC, 'note': None})
        dist_locs, dist_facs = [], []
        for c in topics['distribution']:
            for m in c.get('mentions', []):
                if m.startswith(('loc_', 'reg_')) and m not in dist_locs:
                    dist_locs.append(m)
                if m.startswith('fac_') and m not in dist_facs:
                    dist_facs.append(m)
        functional = []
        if eff:
            for word in re.split(r'[,/ ]+', eff['text'].lower()):
                f = EFFECT_MAP.get(word)
                if f and f not in functional:
                    functional.append(f)
        rec['names']['aliases'] = rec['names']['aliases']
        rec.update({
            'classification': {
                'ranks': ranks, 'rank_type': rank_type, 'is_immortal_gu': is_imm, 'paths': paths,
                'path_unverified': bool(path_row and path_row['unverified']),
                'functional_types': functional, 'effect_type_raw': clean(eff['text']) if eff else None,
                'organism_type': 'unknown', 'organism_type_basis': None,
                'claims': topics['classification'],
            },
            'appearance': {'claims': topics['appearance'], 'visual_confidence': 'text_described' if topics['appearance'] else 'unknown'},
            'abilities': {'activation_raw': known(act), 'claims': topics['abilities']},
            'feeding': {'known': bool(known(food) or topics['feeding']), 'food_raw': known(food), 'foods': foods, 'claims': topics['feeding']},
            'refinement': {
                'recipes': [], 'used_in_recipes': [],
                'refinement_materials': [clean(x) for x in re.split(r',\s*', mats['text'])] if mats else [],
                'confirmed_evolutions': [], 'related_variants': [], 'claims': topics['refinement'],
            },
            'economy': {'canonical_prices': [], 'price_claims': topics['economy'], 'game_proposals': []},
            'distribution': {'locations': dist_locs, 'factions': dist_facs, 'origin': 'unknown', 'claims': topics['distribution']},
            'ownership_history': owners,
            'history': {'claims': topics['history']},
        })
        rec['relations'] = dedupe_relations(rels)
        rec['attributes'] = attrs
        if rank_row and rank_row['unverified']:
            rec['research']['unresolved'].append('Rank is marked unverified at source.')
        if not ranks:
            rec['research']['unresolved'].append('Rank not recorded at source.')
        return rec

    def build_generic(self, page, prefix, etype):
        rec = self.envelope(page, prefix, etype)
        rels, attrs = self.infobox_relations(page, rec['id'])
        rec['relations'] = dedupe_relations(rels)
        rec['attributes'] = attrs
        claims = []
        for s in page['sections']:
            for c in s['claims']:
                claims.append(self.claim(page, c, s['id']))
        rec['claims'] = claims
        for key, rel in page['relations'].items():
            rtype = {'relations-moves': 'uses_move', 'relations-fights': 'took_part_in', 'relations-inheritances': 'linked_inheritance',
                     'relations-gu-houses': 'uses_gu_house', 'relations-gu-held': 'holds_gu'}.get(key, key.replace('relations-', '').replace('-', '_'))
            for tile in rel['tiles']:
                t = self.ref(tile['href']) if tile['href'] else None
                if t and t != rec['id']:
                    rec['relations'].append({'type': rtype, 'target': t, 'role': tile['role'], 'from_chapter': None, 'to_chapter': None,
                                             'recorded_chapter': valid_ch(tile['ch']), 'count': None, 'confidence': 'B', 'source_id': SRC, 'note': None})
        rec['relations'] = dedupe_relations(rec['relations'])
        return rec

    def build_character(self, page):
        rec = self.build_generic(page, 'char_', 'character')
        ib = page['infobox']
        ranks = []
        rr = ib.get('Ranks')
        if rr:
            for it in rr['items']:
                if 'rank' in it:
                    ranks.append({'rank': it['rank'], 'as_of_chapter': valid_ch(it.get('ch')), 'confidence': 'D' if rr['unverified'] else 'B'})
        status = ib.get('Status')
        rec['cultivation'] = {
            'rank_timeline': ranks,
            'rank_timeline_note': 'As recorded by the source infobox; may stop before the character\'s final rank. Gu Master rank, not Gu rank.',
            'paths': [r['target'] for r in rec['relations'] if r['type'] == 'path'],
            'aperture': None, 'talent_grade': None,
        }
        rec['biography'] = {
            'race': next((r['target'] for r in rec['relations'] if r['type'] == 'race'), None),
            'region': next((r['target'] for r in rec['relations'] if r['type'] == 'in_region'), None),
            'status': clean(status['text']) if status else None,
            'alignment': clean(ib['Alignment']['text']) if 'Alignment' in ib else None,
            'age': None, 'birthplace': None,
        }
        rec['gu_held'] = []
        romanized = rec['names']['en']
        py = pinyin_of(romanized)
        if py:
            rec['names']['pinyin'] = py
            rec['names']['pinyin_status'] = 'derived_from_en_romanization'
            rec['names']['ru'] = palladius(py)
            rec['names']['ru_status'] = 'transliteration'
        return rec

    def build(self, sections):
        out = defaultdict(dict)
        for href, page in sorted(self.pages.items()):
            sec = page['section']
            if sec not in sections:
                continue
            prefix, d, etype = SECTIONS[sec]
            if sec == 'gu':
                rec = self.build_gu(page)
            elif sec == 'characters':
                rec = self.build_character(page)
            elif sec == 'chapters':
                rec = self.build_chapter(page)
            else:
                rec = self.build_generic(page, prefix, etype)
                rec[SUBKIND_FIELD.get(etype, 'kind')] = sec if etype in ('event', 'region') else None
                if etype in ('event', 'region'):
                    pass
                else:
                    rec.pop(SUBKIND_FIELD.get(etype, 'kind'), None)
            out[d][rec['id']] = rec
        self.link_inverse(out)
        return out

    def build_chapter(self, page):
        n = int(page['slug'])
        subjects = sorted({i for i in (self.ref(h) for h in self.chapter_subjects.get(n, ())) if i})
        return {
            'id': 'ch_%04d' % n, 'entity_type': 'chapter', 'schema_version': SCHEMA_VERSION,
            'number': n, 'title': None, 'volume': self.volume_of(n), 'arc': self.chapter_arc.get(n),
            'subjects': subjects,
            'summary_source': {'source_id': SRC, 'url': page['url']},
            'summary_word_count': sum(len(c['text'].split()) for s in page['sections'] for c in s['claims']),
        }

    def link_inverse(self, out):
        index = {}
        for d, recs in out.items():
            for rid, rec in recs.items():
                index[rid] = rec
        for d, recs in out.items():
            for rid, rec in recs.items():
                if rec.get('entity_type') != 'gu':
                    continue
                for o in rec['ownership_history']:
                    tgt = index.get(o['character'])
                    if tgt is not None and tgt.get('entity_type') == 'character':
                        tgt['gu_held'].append({'gu': rid, 'role': o['role'], 'recorded_chapter': o['recorded_chapter'],
                                               'from_chapter': o['from_chapter'], 'to_chapter': o['to_chapter'],
                                               'confidence': o['confidence'], 'source_id': SRC})
        for d, recs in out.items():
            for rid, rec in recs.items():
                if rec.get('entity_type') == 'recipe':
                    for r in rec['relations']:
                        if r['type'] == 'produces' and r['target'] in index and index[r['target']]['entity_type'] == 'gu':
                            g = index[r['target']]
                            if rid not in g['refinement']['recipes']:
                                g['refinement']['recipes'].append(rid)
                        if r['type'] in ('ingredients', 'inputs', 'requires', 'consumes') and r['target'] in index and index[r['target']]['entity_type'] == 'gu':
                            g = index[r['target']]
                            if rid not in g['refinement']['used_in_recipes']:
                                g['refinement']['used_in_recipes'].append(rid)


RELATION_TYPES = {
    'Known owners': 'held_by', 'Affiliations': 'member_of', 'Region': 'in_region', 'Race': 'race',
    'Seat': 'seat', 'Paths': 'path', 'Path': 'path', 'Food': 'feeds_on', 'Produces': 'produces',
    'Holders': 'held_by', 'Arc': 'in_arc', 'Previous': 'previous', 'Next': 'next',
}
EFFECT_MAP = {'movement': 'movement', 'support': 'support', 'defensive': 'defense', 'investigative': 'investigation',
              'cultivation': 'cultivation', 'healing': 'healing', 'transformation': 'transformation',
              'expenditure': 'expenditure', 'offensive': 'attack', 'assistance': 'support', 'attack': 'attack',
              'storage': 'storage', 'communication': 'communication', 'concealment': 'concealment', 'control': 'control'}
SUBKIND_FIELD = {'event': 'event_kind', 'region': 'region_kind'}


def page_href(page):
    return f'/{page["section"]}/{page["slug"]}/'


def valid_ch(x):
    return x if isinstance(x, int) and 1 <= x <= 2334 else None


def known(row):
    if not row:
        return None
    t = clean(row['text'])
    return None if not t or t.lower() in ('unknown', 'none recorded', '—') else t


def gu_topic(section_id):
    if section_id == 'intro':
        return 'history'
    for topic, pat in GU_TOPICS:
        if re.search(pat, section_id):
            return topic
    return 'history'


def dedupe_relations(rels):
    seen, out = set(), []
    for r in rels:
        k = (r['type'], r['target'], r.get('role'), r.get('from_chapter'), r.get('to_chapter'), r.get('recorded_chapter'))
        if k not in seen:
            seen.add(k)
            out.append(r)
    return out


def load_volumes():
    p = os.path.join(ROOT, 'systems', 'volumes.json')
    if os.path.exists(p):
        with open(p, encoding='utf-8') as fh:
            return json.load(fh).get('volumes', [])
    return []


sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palladius import palladius, pinyin_of  # noqa: E402


def write(out):
    n = 0
    for d, recs in out.items():
        full = os.path.join(ROOT, d)
        os.makedirs(full, exist_ok=True)
        for rid, rec in recs.items():
            p = os.path.join(full, rid + '.json')
            if os.path.exists(p):
                with open(p, encoding='utf-8') as fh:
                    old = json.load(fh)
                if old.get('research', {}).get('status') in ('reviewed', 'verified'):
                    continue
                for key in ('names',):
                    # keep hand-filled translations
                    for f in ('ru', 'ru_status', 'zh', 'zh_source', 'pinyin', 'pinyin_status', 'translation_variants'):
                        if old.get(key, {}).get(f) and not rec.get(key, {}).get(f) or (f == 'ru' and old.get(key, {}).get('ru_status') == 'working_translation'):
                            if key in rec and f in old.get(key, {}):
                                rec[key][f] = old[key][f]
                if old.get('game_proposals'):
                    rec['game_proposals'] = old['game_proposals']
            with open(p, 'w', encoding='utf-8', newline='\n') as fh:
                json.dump(rec, fh, ensure_ascii=False, indent=2)
                fh.write('\n')
            n += 1
    return n


def main():
    parsed = sys.argv[1]
    sections = set(SECTIONS)
    if '--sections' in sys.argv:
        sections = set(sys.argv[sys.argv.index('--sections') + 1].split(','))
    b = Builder(parsed)
    out = b.build(sections)
    print('written', write(out), {d: len(r) for d, r in out.items()})


if __name__ == '__main__':
    main()
