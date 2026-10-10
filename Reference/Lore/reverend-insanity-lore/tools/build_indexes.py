"""Rebuild indexes/*.json and indexes/relations.json from entity files.

Usage: python tools/build_indexes.py
"""
import json
import os
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIRS = ['gu', 'characters', 'recipes', 'factions', 'locations', 'regions', 'resources', 'moves', 'systems',
        'inheritances', 'gu_houses', 'paths', 'creatures', 'events', 'chapters', 'terminology']
PER_TYPE = {'gu': 'gu-index.json', 'characters': 'character-index.json', 'locations': 'location-index.json',
            'regions': 'region-index.json', 'factions': 'faction-index.json'}


def dump(name, data):
    with open(os.path.join(ROOT, 'indexes', name), 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
        fh.write('\n')


def main():
    os.makedirs(os.path.join(ROOT, 'indexes'), exist_ok=True)
    master, edges, by_dir = [], [], {}
    for d in DIRS:
        full = os.path.join(ROOT, d)
        if not os.path.isdir(full):
            continue
        rows = []
        for f in sorted(os.listdir(full)):
            if not f.endswith('.json'):
                continue
            with open(os.path.join(full, f), encoding='utf-8') as fh:
                r = json.load(fh)
            names = r.get('names', {})
            row = {'id': r['id'], 'file': f'{d}/{f}', 'type': r['entity_type'],
                   'en': names.get('en') or r.get('title') or (f'Chapter {r["number"]}' if 'number' in r else None),
                   'zh': names.get('zh'), 'ru': names.get('ru'), 'aliases': names.get('aliases', []),
                   'confidence': r.get('confidence'), 'canon_status': r.get('canon_status'),
                   'first_chapter': (r.get('first_appearance') or {}).get('chapter') or r.get('number')}
            if d == 'gu':
                c = r['classification']
                row.update({'ranks': [x['rank'] for x in c['ranks']], 'rank_type': c['rank_type'], 'paths': c['paths'],
                            'owners': sorted({o['character'] for o in r['ownership_history']}),
                            'food': r['feeding']['food_raw'], 'regions_locations': r['distribution']['locations']})
            if d == 'characters':
                row.update({'ranks': [x['rank'] for x in r['cultivation']['rank_timeline']], 'paths': r['cultivation']['paths'],
                            'factions': sorted({x['target'] for x in r['relations'] if x['type'] == 'member_of'}),
                            'region': r['biography'].get('region'), 'gu_count': len({g['gu'] for g in r['gu_held']})})
            rows.append(row)
            master.append({'id': row['id'], 'file': row['file'], 'type': row['type'], 'en': row['en']})
            for rel in r.get('relations', []):
                edges.append({'from': r['id'], 'type': rel['type'], 'to': rel['target'], 'role': rel.get('role'),
                              'from_chapter': rel.get('from_chapter'), 'to_chapter': rel.get('to_chapter'),
                              'recorded_chapter': rel.get('recorded_chapter'), 'confidence': rel.get('confidence')})
        by_dir[d] = rows
        if d in PER_TYPE:
            dump(PER_TYPE[d], {'schema_version': '1.0', 'count': len(rows), 'entities': rows})
        elif d != 'chapters':
            dump(f'{d.replace("_", "-")}-index.json', {'schema_version': '1.0', 'count': len(rows), 'entities': rows})
    name_index = {}
    for row in master:
        for n in [row['en']] + [x for d in by_dir.values() for x in []]:
            if n:
                name_index.setdefault(n.lower(), []).append(row['id'])
    for d, rows in by_dir.items():
        for row in rows:
            for n in [row.get('zh'), row.get('ru')] + row.get('aliases', []):
                if n:
                    name_index.setdefault(n.lower(), []).append(row['id'])
    dump('master-index.json', {'schema_version': '1.0', 'counts': {d: len(r) for d, r in by_dir.items()},
                               'total': len(master), 'entities': master})
    dump('name-index.json', {k: sorted(set(v)) for k, v in sorted(name_index.items())})
    dump('relations.json', {'schema_version': '1.0', 'count': len(edges),
                            'types': dict(Counter(e['type'] for e in edges).most_common()), 'edges': edges})
    print('indexed', len(master), 'edges', len(edges))


if __name__ == '__main__':
    main()
