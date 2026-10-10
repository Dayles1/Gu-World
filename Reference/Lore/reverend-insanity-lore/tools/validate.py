"""Validate the lore database. Standard library only (no project dependencies).

Usage: python tools/validate.py [--quiet]

Checks:
  1. every *.json parses as UTF-8 JSON
  2. every entity file matches its JSON Schema (draft 2020-12 subset)
  3. ids are unique and match file names
  4. every referenced entity id exists
  5. indexes list exactly the entity files on disk
  6. every source_id used is declared in sources/sources.json
  7. canon claims carry at least one chapter; GAME_PROPOSAL values only in game_proposals
"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCHEMAS = os.path.join(ROOT, 'schemas')

DIR_SCHEMA = {
    'gu': 'gu', 'characters': 'character', 'recipes': 'recipe', 'factions': 'faction',
    'locations': 'location', 'regions': 'region', 'resources': 'resource', 'moves': 'move',
    'systems': 'system', 'inheritances': 'inheritance', 'gu_houses': 'gu_house', 'paths': 'path',
    'creatures': 'creature', 'events': 'event', 'chapters': 'chapter', 'terminology': 'term',
}
ID_RE = re.compile(r'^(gu|char|rcp|fac|loc|reg|res|move|sys|inh|house|path|race|evt|ch|term|src)_[a-z0-9_]+$')
TYPES = {'object': dict, 'array': list, 'string': str, 'boolean': bool, 'null': type(None)}

_schema_cache = {}


def load_schema(name):
    if name not in _schema_cache:
        with open(os.path.join(SCHEMAS, name), encoding='utf-8') as fh:
            _schema_cache[name] = json.load(fh)
    return _schema_cache[name]


def resolve(ref, base):
    file, _, pointer = ref.partition('#')
    doc_name = file or base
    node = load_schema(doc_name)
    for part in [p for p in pointer.split('/') if p]:
        node = node[part]
    return node, doc_name


def is_type(value, t):
    if t == 'integer':
        return isinstance(value, int) and not isinstance(value, bool)
    if t == 'number':
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    return isinstance(value, TYPES[t])


def validate(value, schema, base, path, errors):
    if schema is True or schema == {}:
        return
    if '$ref' in schema:
        sub, doc = resolve(schema['$ref'], base)
        validate(value, sub, doc, path, errors)
    for sub in schema.get('allOf', []):
        validate(value, sub, base, path, errors)
    if 'anyOf' in schema:
        if not any(not _errs(value, s, base) for s in schema['anyOf']):
            errors.append(f'{path}: matches none of anyOf')
    if 'type' in schema:
        types = schema['type'] if isinstance(schema['type'], list) else [schema['type']]
        if not any(is_type(value, t) for t in types):
            errors.append(f'{path}: expected {types}, got {type(value).__name__}')
            return
    if 'const' in schema and value != schema['const']:
        errors.append(f'{path}: expected const {schema["const"]!r}')
    if 'enum' in schema and value not in schema['enum']:
        errors.append(f'{path}: {value!r} not in enum')
    if isinstance(value, str):
        if 'pattern' in schema and not re.search(schema['pattern'], value):
            errors.append(f'{path}: {value!r} does not match {schema["pattern"]}')
        if len(value) < schema.get('minLength', 0):
            errors.append(f'{path}: string too short')
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if 'minimum' in schema and value < schema['minimum']:
            errors.append(f'{path}: {value} < {schema["minimum"]}')
        if 'maximum' in schema and value > schema['maximum']:
            errors.append(f'{path}: {value} > {schema["maximum"]}')
    if isinstance(value, dict):
        for key in schema.get('required', []):
            if key not in value:
                errors.append(f'{path}: missing required {key!r}')
        props = schema.get('properties', {})
        for key, sub in props.items():
            if key in value:
                validate(value[key], sub, base, f'{path}.{key}', errors)
        extra = schema.get('additionalProperties', True)
        if extra is not True:
            for key in value:
                if key not in props:
                    if extra is False:
                        errors.append(f'{path}: unexpected property {key!r}')
                    else:
                        validate(value[key], extra, base, f'{path}.{key}', errors)
    if isinstance(value, list):
        if len(value) < schema.get('minItems', 0):
            errors.append(f'{path}: fewer than {schema["minItems"]} items')
        if schema.get('uniqueItems'):
            seen = [json.dumps(v, sort_keys=True) for v in value]
            if len(seen) != len(set(seen)):
                errors.append(f'{path}: items not unique')
        if 'items' in schema:
            for i, item in enumerate(value):
                validate(item, schema['items'], base, f'{path}[{i}]', errors)


def _errs(value, schema, base):
    e = []
    validate(value, schema, base, '', e)
    return e


def collect_refs(node, out):
    if isinstance(node, dict):
        for k, v in node.items():
            if k in ('id', 'type', 'entity_type', 'source_id', 'url', 'text', 'en', 'ru', 'zh', 'pinyin', 'role', 'aliases'):
                continue
            collect_refs(v, out)
    elif isinstance(node, list):
        for v in node:
            collect_refs(v, out)
    elif isinstance(node, str) and ID_RE.match(node) and not node.startswith('src_'):
        out.add(node)


def collect_sources(node, out):
    if isinstance(node, dict):
        for k, v in node.items():
            if k == 'source_id' and isinstance(v, str):
                out.add(v)
            else:
                collect_sources(v, out)
    elif isinstance(node, list):
        for v in node:
            collect_sources(v, out)


def check_claims(node, path, errors):
    if isinstance(node, dict):
        if 'certainty' in node and 'text' in node and node.get('certainty') == 'canon' and not node.get('chapters'):
            errors.append(f'{path}: canon claim without chapter')
        for k, v in node.items():
            if k == 'game_proposals':
                continue
            if k == 'tag' and v == 'GAME_PROPOSAL':
                errors.append(f'{path}: GAME_PROPOSAL outside a game_proposals list')
            check_claims(v, f'{path}.{k}', errors)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            check_claims(v, f'{path}[{i}]', errors)


def main():
    quiet = '--quiet' in sys.argv
    errors = []
    ids = {}
    refs = defaultdict(set)
    used_sources = set()
    counts = Counter()

    for d, schema_name in DIR_SCHEMA.items():
        full = os.path.join(ROOT, d)
        if not os.path.isdir(full):
            continue
        schema_file = f'{schema_name}.schema.json'
        if not os.path.exists(os.path.join(SCHEMAS, schema_file)):
            errors.append(f'missing schema {schema_file}')
            continue
        schema = load_schema(schema_file)
        for f in sorted(os.listdir(full)):
            if not f.endswith('.json'):
                continue
            p = os.path.join(full, f)
            rel = f'{d}/{f}'
            try:
                with open(p, encoding='utf-8') as fh:
                    rec = json.load(fh)
            except Exception as e:
                errors.append(f'{rel}: invalid JSON: {e}')
                continue
            counts[d] += 1
            e = []
            validate(rec, schema, schema_file, rel, e)
            errors.extend(e)
            rid = rec.get('id')
            if rid != f[:-5]:
                errors.append(f'{rel}: id {rid!r} does not match file name')
            if rid in ids:
                errors.append(f'{rel}: duplicate id {rid} (also {ids[rid]})')
            ids[rid] = rel
            r = set()
            collect_refs(rec, r)
            r.discard(rid)
            refs[rel] = r
            collect_sources(rec, used_sources)
            check_claims(rec, rel, errors)

    for rel, r in refs.items():
        for target in sorted(r):
            if target not in ids:
                errors.append(f'{rel}: dangling reference {target}')

    src_path = os.path.join(ROOT, 'sources', 'sources.json')
    declared = set()
    if os.path.exists(src_path):
        with open(src_path, encoding='utf-8') as fh:
            src = json.load(fh)
        e = []
        validate(src, load_schema('source.schema.json'), 'source.schema.json', 'sources/sources.json', e)
        errors.extend(e)
        declared = {s['id'] for s in src.get('sources', [])}
    for s in sorted(used_sources - declared):
        errors.append(f'source {s} used but not declared in sources/sources.json')

    master = os.path.join(ROOT, 'indexes', 'master-index.json')
    if os.path.exists(master):
        with open(master, encoding='utf-8') as fh:
            m = json.load(fh)
        listed = {e['id']: e['file'] for e in m.get('entities', [])}
        for rid, rel in ids.items():
            if listed.get(rid) != rel:
                errors.append(f'master-index: {rid} missing or wrong file')
        for rid, rel in listed.items():
            if not os.path.exists(os.path.join(ROOT, rel)):
                errors.append(f'master-index: {rel} does not exist')
    else:
        errors.append('indexes/master-index.json missing')

    for f in os.listdir(os.path.join(ROOT, 'indexes')) if os.path.isdir(os.path.join(ROOT, 'indexes')) else []:
        try:
            with open(os.path.join(ROOT, 'indexes', f), encoding='utf-8') as fh:
                json.load(fh)
        except Exception as e:
            errors.append(f'indexes/{f}: invalid JSON: {e}')

    print('files:', dict(counts), 'total', sum(counts.values()))
    print('sources used:', len(used_sources), 'declared:', len(declared))
    print('errors:', len(errors))
    by_kind = Counter(re.sub(r"[\[\]0-9'.]+", '', e.split(': ', 1)[-1])[:60] for e in errors)
    for k, n in by_kind.most_common(15):
        print(f'  {n:6d}  {k}')
    if not quiet:
        for e in (errors if '--all' in sys.argv else errors[:60]):
            print('  -', e)
    sys.exit(1 if errors else 0)


if __name__ == '__main__':
    main()
