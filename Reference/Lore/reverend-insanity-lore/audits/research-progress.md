# Research progress

## 2026-10-09 — session 1

Update: the crawl finished (6615/6615, 0 failures) and all sections are built: 6581 records, validator 0 errors. Steps 1–2 below are done. DATA_CONTRACT.md 0.2 has been agreed with the game agent and awaits user confirmation. README.md and glossary.md are in Russian.


Done
- Source survey: sagaofgu.com chosen as the primary source (chapter-cited, crawl allowed). Fandom blocked (Cloudflare), Webnovel blocked (403), ChatGPT share empty, novelwiki C-level only. See `sources/sources.json`.
- Schemas: common envelope + gu (detailed) + generic schemas for the other types (`attributes` + `claims`; typed fields still to add).
- Tooling: crawler, parser, builder, index builder, validator (stdlib Python).
- Built: 1035 Gu, 913 characters, 182 factions, 366 locations, 5 regions, 11 recipes (~3100 of 6615 pages cached when the session ended).
- Validator: everything passes except 515 dangling `res_*` references (resources section not built yet).

Next, in order
1. Finish the crawl (resumable): `python tools/crawl_sagaofgu.py .cache/sagaofgu/pages .cache/sagaofgu/sitemap-0.xml`. Remaining: materials, moves, cultivation, inheritances, gu-houses, paths, races, legends, earths, heavens, eras, arcs, fights, chapters.
2. Re-run parse → build (all sections) → indexes → validate. Chapter pages enable the `cross_checked_chapter_summary` check and arc links; `paths` fills Gu/character paths.
3. Add `systems/volumes.json` (volume → chapter ranges) from a citable source; the builder then fills `first_appearance.volume`.
4. Write typed schemas for location (parent, region, adjacency), faction (leaders, HQ), recipe (inputs/outputs), resource, move.
5. Recipes: only 12 recipe pages exist at the source. Mine Gu `refinement.claims` for fusion inputs/outputs and record them as `rcp_*` with basis "claim".
6. Gu Russian names (`working_translation`), organism_type classification, review of 154 Gu price mentions into `canonical_prices`.
7. Chinese names: try Moegirl (`src_moegirl`) for key Gu/characters; record `zh_source`.
8. Coverage matrix per arc from `chapters/` subjects.
9. Spot-check a sample of B claims against licensed text when one is reachable (would give the first A records and a measured error rate for the source).
