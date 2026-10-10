"""Parse cached sagaofgu.com pages into intermediate JSON (one file per page).

Usage: python -I parse.py <pages_dir> <out_dir>
"""
import html
import json
import os
import re
import sys

PAGES, OUT = sys.argv[1], sys.argv[2]

CJK = re.compile(r'[㐀-鿿]')


def text(s):
    s = re.sub(r'<sup class="cite">.*?</sup>', ' ', s, flags=re.S)
    s = re.sub(r'<svg.*?</svg>', ' ', s, flags=re.S)
    s = re.sub(r'<[^>]+>', ' ', s)
    s = html.unescape(s).replace(' ', ' ')
    return re.sub(r'\s+', ' ', s).strip()


def links(s):
    out = []
    for href in re.findall(r'<a [^>]*href="(/[a-z-]+/[^"#]+/)"', s):
        if href.startswith('/chapters/'):
            continue
        if href not in out:
            out.append(href)
    return out


def chapters(s):
    return sorted({int(c) for c in re.findall(r'href="/chapters/(\d+)/"', s)})


def balanced(s, start, tag):
    """Return end index of the element of `tag` opening at `start`."""
    depth = 0
    for m in re.finditer(r'<(/?)%s\b[^>]*>' % tag, s[start:]):
        depth += -1 if m.group(1) else 1
        if depth == 0:
            return start + m.end()
    return len(s)


def veil_fields(dd):
    items = []
    for m in re.finditer(r'<span class="veil-field"([^>]*)>', dd):
        end = balanced(dd, m.start(), 'span')
        body = dd[m.start():end]
        attrs = dict(re.findall(r'(data-[a-z-]+)="([^"]*)"', m.group(1)))
        val = re.search(r'<span class="veil-field-value">(.*?)</span>\s*(<span class="veil-field-(?:role|ch)">|$)', body, re.S)
        role = re.search(r'<span class="veil-field-role">\((.*?)\)</span>', body)
        rank = re.search(r'data-variant="rank" data-value="(\d+)" data-tone="([a-z]+)"', body)
        href = re.search(r'<a href="(/[^"]+)"', body)
        item = {
            'text': text(re.sub(r'<span class="veil-field-(role|ch)">.*?</span>', '', body)),
            'href': href.group(1) if href else None,
            'role': role.group(1) if role else None,
            'ch': int(attrs['data-ch']) if attrs.get('data-ch', '').isdigit() else None,
            'from': int(attrs['data-from']) if attrs.get('data-from', '').isdigit() else None,
            'to': int(attrs['data-to']) if attrs.get('data-to', '').isdigit() else None,
            'level': attrs.get('data-level'),
        }
        if rank:
            item['rank'] = int(rank.group(1))
            item['rank_tone'] = rank.group(2)
        items.append(item)
    return items


def infobox(t):
    rows = {}
    for m in re.finditer(r'<div class="tablet-row"([^>]*)>', t):
        end = balanced(t, m.start(), 'div')
        row = t[m.start():end]
        dt = re.search(r'<dt[^>]*>(.*?)</dt>', row, re.S)
        dd = re.search(r'<dd[^>]*>(.*)</dd>', row, re.S)
        if not dt or not dd:
            continue
        label = text(dt.group(1))
        ddh = dd.group(1)
        fields = veil_fields(ddh)
        rows[label] = {
            'unverified': 'data-unverified="true"' in m.group(1),
            'text': text(ddh),
            'items': fields,
            'links': links(ddh),
            'ranks': [int(x) for x in re.findall(r'data-variant="rank" data-value="(\d+)"', ddh)],
            'chapters': chapters(ddh),
        }
    return rows


def claims_from(block):
    """Split a prose block into claims, each ending with a citation chip group."""
    out = []
    for para in re.findall(r'<p[^>]*>(.*?)</p>', block, re.S):
        pieces = re.split(r'(<sup class="cite">.*?</sup>)', para, flags=re.S)
        buf = ''
        for piece in pieces:
            if piece.startswith('<sup class="cite">'):
                t = text(buf)
                if t:
                    out.append({'text': t, 'chapters': chapters(piece), 'links': links(buf)})
                buf = ''
            else:
                buf += piece
        t = text(buf)
        if t:
            out.append({'text': t, 'chapters': [], 'links': links(buf)})
    return out


def conflicts(block):
    res = []
    for a in re.findall(r'<aside class="canon-conflict".*?</aside>', block, re.S):
        items = [{'position': text(dt), 'support': text(dd)} for dt, dd in re.findall(r'<dt>(.*?)</dt>\s*<dd>(.*?)</dd>', a, re.S)]
        foot = re.search(r'canon-conflict-footer">(.*?)</p>', a, re.S)
        res.append({'positions': items, 'status': text(foot.group(1)) if foot else None})
    return res


def body(t):
    m = re.search(r'<article class="wiki-body"[^>]*>', t)
    if not m:
        return None, [], []
    end = balanced(t, m.start(), 'article')
    art = t[m.start():end]
    lede = re.search(r'<p class="prose-lede"[^>]*>(.*?)</p>', art, re.S)
    stub = 'stub' in (text(lede.group(1)).lower() if lede else '') or 'class="stub' in art
    # sections: split prose at h2
    prose = art
    parts = re.split(r'<h2[^>]*id="([^"]+)"[^>]*>(.*?)</h2>', prose, flags=re.S)
    sections = []
    intro = parts[0]
    intro = re.sub(r'<p class="prose-lede".*?</p>', '', intro, flags=re.S)
    sections.append({'id': 'intro', 'heading': None, 'claims': claims_from(intro), 'conflicts': conflicts(intro)})
    for i in range(1, len(parts), 3):
        sid, head, content = parts[i], text(parts[i + 1]), parts[i + 2]
        if sid in ("chronicle", "ego-heading", "veil-panel-title") or sid.startswith("relations-"):
            continue
        sections.append({'id': sid, 'heading': head, 'claims': claims_from(content), 'conflicts': conflicts(content)})
    return (text(lede.group(1)) if lede else None), sections, stub


def connections(t):
    m = re.search(r'id="ego-heading"', t)
    if not m:
        return []
    seg = t[m.start():]
    out = []
    for row in re.findall(r'<li class="ego-row.*?</li>', seg, re.S):
        h = re.search(r'<a href="(/[a-z-]+/[^"#]+/)"', row)
        c = re.search(r'×\s*(\d+)', text(row))
        if h:
            out.append({'href': h.group(1), 'count': int(c.group(1)) if c else 1})
    return out


def relation_tiles(t):
    res = {}
    for m in re.finditer(r'<section class="relations" aria-labelledby="([^"]+)"', t):
        end = balanced(t, m.start(), 'section')
        sec = t[m.start():end]
        cap = re.search(r'(\d+) recorded', sec)
        tiles = []
        for li in re.findall(r'<li class="relations-tile.*?</li>', sec, re.S):
            h = re.search(r'<a href="(/[^"]+)"', li)
            ch = re.search(r'data-ch="(\d+)"', li)
            role = re.search(r'veil-field-role">\((.*?)\)', li)
            tiles.append({'href': h.group(1) if h else None, 'ch': int(ch.group(1)) if ch else None,
                          'role': role.group(1) if role else None, 'text': text(li)})
        res[m.group(1)] = {'tiles': tiles, 'capped_total': int(cap.group(1)) if cap else None}
    return res


def parse(path, section, slug):
    t = open(path, encoding='utf-8', errors='replace').read()
    title = re.search(r'<h1 class="wiki-title"[^>]*>(.*?)</h1>', t, re.S)
    cn = re.search(r'<p class="wiki-cn"[^>]*>(.*?)</p>', t, re.S)
    sub = re.search(r'<p class="wiki-subtitle"[^>]*>(.*?)</p>', t, re.S)
    srcs = re.search(r'<footer class="tablet-sources".*?</footer>', t, re.S)
    lede, sections, stub = body(t)
    img = re.search(r'"contentUrl":"(https://sagaofgu.com/art/[^"]+)"', t)
    return {
        'section': section, 'slug': slug, 'url': f'https://sagaofgu.com/{section}/{slug}/',
        'title': text(title.group(1)) if title else None,
        'zh': text(cn.group(1)) if cn else None,
        'subtitle': text(sub.group(1)) if sub else None,
        'infobox': infobox(t),
        'source_chapters': chapters(srcs.group(0)) if srcs else [],
        'lede': lede, 'stub': stub, 'sections': sections,
        'relations': relation_tiles(t),
        'connections': connections(t),
        'image': img.group(1) if img else None,
    }


def main():
    n = 0
    for section in sorted(os.listdir(PAGES)):
        d = os.path.join(PAGES, section)
        if not os.path.isdir(d):
            continue
        od = os.path.join(OUT, section)
        os.makedirs(od, exist_ok=True)
        for f in sorted(os.listdir(d)):
            if not f.endswith('.html') or f == '_index.html':
                continue
            slug = f[:-5]
            rec = parse(os.path.join(d, f), section, slug)
            with open(os.path.join(od, slug + '.json'), 'w', encoding='utf-8') as fh:
                json.dump(rec, fh, ensure_ascii=False, indent=1)
            n += 1
    print('parsed', n)


if __name__ == '__main__':
    main()
