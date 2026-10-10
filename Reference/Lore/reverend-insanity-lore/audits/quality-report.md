# Quality report (2026-10-09, full first pass)

Validator (`python tools/validate.py`): **0 errors** on 6581 files. Checked: schema, unique ids, ids match file names, every reference resolves, indexes complete, sources declared.

| Section | Records |
|---|---|
| gu | 1035 |
| characters | 913 |
| factions | 182 |
| locations | 366 |
| regions (regions + earths + heavens) | 24 |
| recipes | 11 |
| resources (materials) | 445 |
| moves (killer moves) | 634 |
| systems (cultivation concepts) | 297 |
| inheritances | 83 |
| gu_houses | 104 |
| paths | 47 |
| creatures (races) | 13 |
| events (arcs, fights, legends, eras) | 93 |
| chapters (summary index, subjects only) | 2334 |

Claims in Gu + character records, by verification:
- cross_checked_chapter_summary: 8114 (the cited chapter's summary page names the entity)
- citation_unchecked: 4816
- no_citation: 436
- text_checked: 0. **No A-level records**: the novel text was not reachable.

Known gaps
- Gu: 422 without recorded rank; only 183 linked to a path; organism_type not classified; no zh names; no ru names; 154 Gu with unreviewed price mentions, canonical_prices empty.
- Character rank timelines can stop early (source infobox), e.g. Fang Yuan ends at rank 6 (ch. 630).
- Typed schemas exist only for gu and character; the other types keep raw infobox fields in `attributes`.
- Relation types derived from infobox labels are sometimes noisy (e.g. fight sides become types such as `gu_yue_clan`). Normalise before the export relies on them.
- volume: not filled (no volume ↔ chapter table yet).
