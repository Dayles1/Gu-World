# Reverend Insanity — исследовательская лор-база

Независимая база канона «Reverend Insanity» (蛊真人, Гу Чжэнь Жэнь) для GU World. Игра её напрямую не загружает: данные в рантайм попадают через экспортёр движка по [DATA_CONTRACT.md](DATA_CONTRACT.md). Игровой код, конфиги и зависимости проекта не изменялись.

## Статус (2026-10-09)

| Раздел | Записей | Состояние |
|---|---|---|
| Гу (`gu/`) | 1035 | собрано |
| Персонажи (`characters/`) | 913 | собрано |
| Фракции (`factions/`) | 182 | собрано |
| Локации (`locations/`) | 366 | собрано |
| Регионы (`regions/`) | 5 | собрано; ещё 11 «земель» и 10 «небес» в очереди |
| Рецепты (`recipes/`) | 11 | собрано (на источнике всего 12 страниц) |
| Ресурсы, приёмы, системы, наследия, Гу-дома, пути, расы, события, главы | — | страницы скачаны или скачиваются, ещё не собраны |

Подробности — в [audits/research-progress.md](audits/research-progress.md) и [audits/quality-report.md](audits/quality-report.md).

## Структура

| Путь | Что внутри |
|---|---|
| `schemas/` | JSON Schema. В `common.schema.json` — общая оболочка записи, утверждения, связи, достоверность |
| `gu/`, `characters/`, `factions/`, `locations/`, `regions/`, `recipes/` … | одна сущность — один JSON, имя файла = стабильный id (`gu_moonlight_gu.json`) |
| `sources/sources.json` | каталог источников со статусом доступа |
| `indexes/` | `master-index.json` (все файлы), индексы по типам, `name-index.json` (имя → id), `relations.json` (граф связей) |
| `audits/` | прогресс, покрытие, противоречия, открытые вопросы, отчёт о качестве |
| `tools/` | краулер, парсер, сборщик, индексатор, валидатор; только стандартная библиотека Python |
| `.cache/` | сырой HTML sagaofgu.com и разобранный JSON; в git не попадает |

## Источники

- **Основной:** [sagaofgu.com](https://sagaofgu.com/). Это неофициальная вики, у которой почти каждое утверждение имеет ссылку на главу. Сайт сам размечает уровень утверждений: canon, inferred, disputed, deviation, unverified.
- **Fandom** закрыт Cloudflare-проверкой, обход не применялся.
- **Официальный перевод на Webnovel** недоступен (403).
- **novelwiki** без ссылок на главы, только уровень C.

Полный список — в `sources/sources.json`.

## Достоверность

- `confidence`:
  - **A** — сверено с текстом романа;
  - **B** — источник ссылается на конкретную главу;
  - **C** — нет нормальной ссылки или вывод сделан из текста;
  - **D** — не проверено у источника, спорно или предположение.
- **Ни одно утверждение пока не сверено с текстом романа, поэтому записей уровня A нет.** B означает «источник указал главу», а не «глава прочитана».
- Поле `claims[].verification` показывает, как проверялась ссылка: `text_checked`, `cross_checked_chapter_summary`, `citation_unchecked`, `no_citation`.
- Противоречия источников хранятся в `conflicts[]` со всеми позициями. Их сводка — в `audits/contradictions.md`.
- Цены не выдумываются. Упоминания цен лежат в `economy.price_claims` с пометкой `needs_review`. Игровые значения хранятся только в `game_proposals[]` с тегом `GAME_PROPOSAL`.

## Имена

- `names.zh` заполняется только из источника. У персонажей китайские имена бывают, у Гу их нет. По памяти поле не заполняется.
- Транслитерация по Палладию, одна на всю базу (`tools/palladius.py`): 古月 = «Гу Юэ», 方源 = «Фан Юань». Альтернативы вроде «Гу Юе» записываются в `translation_variants`.
- `ru_status`: `official`, `transliteration`, `working_translation` (наш перевод, не канон) или `unknown`.

## Тексты

`claims[].text` и `summary.text` — выдержки из sagaofgu (`text_origin: source_excerpt`), они нужны, чтобы каждый факт можно было отследить. Лицензия сайта не указана, поэтому эти тексты только для исследования. В игру идут тексты `own_words`.

## Пересборка и проверка

```sh
cd research/reverend-insanity-lore
python tools/crawl_sagaofgu.py .cache/sagaofgu/pages .cache/sagaofgu/sitemap-0.xml   # докачка, уже скачанное пропускается
python tools/parse_sagaofgu.py .cache/sagaofgu/pages .cache/parsed
python tools/build_from_sagaofgu.py .cache/parsed            # все разделы; --sections gu,characters — выборочно
python tools/build_indexes.py
python tools/validate.py                                      # --all — вывести все ошибки
```

Если нет `.cache/sagaofgu/sitemap-0.xml`: `curl -o .cache/sagaofgu/sitemap-0.xml https://sagaofgu.com/sitemap-0.xml`.

Сборщик не перезаписывает записи со статусом `research.status` = `reviewed` или `verified` и сохраняет вручную заполненные `zh` и `ru`.
