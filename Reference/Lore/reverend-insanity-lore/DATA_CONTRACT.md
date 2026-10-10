# DATA_CONTRACT — лор-база ↔ движок GU World

Статус: **0.2** (2026-10-09). §1–8 согласованы с игровым агентом; §9–11 внесены по его отзыву. Ждёт подтверждения пользователя.

## 1. Роли

- **Лор-база** (`research/reverend-insanity-lore/`) хранит канон, источники, достоверность и связи. Она не знает о координатах, балансе и runtime-состоянии.
- **Движок** (`app/Games/GuWorld`) хранит игровые сущности, координаты, баланс и сохранения. Лор-базу целиком никогда не загружает.
- **Экспортёр** живёт на стороне движка. Он читает `indexes/` и записи по id и выдаёт компактный проверенный JSON для рантайма.

## 2. Идентификаторы

- Лор-id: `^(gu|char|rcp|fac|loc|reg|res|move|sys|inh|house|path|race|evt|ch|term)_[a-z0-9_]+$`. Имя файла совпадает с id (`gu/gu_moonlight_gu.json`).
- Лор-id — это ссылки на шаблоны контента, а не runtime-id. Runtime-id движка имеют вид `kind[:part…]`, например `npc:gu_yue:…`.
- NPC — отдельная игровая сущность с полем `template: "char_…"`. Игровая локация — с необязательным полем `lore_ref: "loc_…" | "reg_…"`.
- **Стабильность.** Опубликованный id не меняется и не удаляется. При переименовании или слиянии старый id остаётся записью с `canon_status: "duplicate"` и связью `duplicate_of`. Экспортёр следует по ней.
- Где найти записи: `indexes/master-index.json` (id → файл, тип), индексы по типам (`gu-index.json`, `character-index.json`, `location-index.json`, `faction-index.json`, `region-index.json`), `name-index.json` (имя/алиас → id), `relations.json` (рёбра графа).

## 3. Версии

- `schema_version: "major.minor"`, сейчас `1.0`.
- Ломающее изменение: удаление или переименование обязательного поля, смена типа или смысла значения. Такое изменение повышает major. Экспортёр проверяет major и отказывается работать с неизвестным.
- Добавление необязательных полей и значений перечислений повышает minor. Неизвестные поля экспортёр игнорирует.

## 4. Поля, на которые может опираться движок (v1)

Для всех типов:

| Поле | Тип | Примечание |
|---|---|---|
| `id`, `entity_type`, `schema_version` | string | обязательны |
| `names.ru` | string \| null | см. `names.ru_status`: `official` / `transliteration` / `working_translation` / `unknown` |
| `names.zh`, `names.en`, `names.aliases` | | `zh` только из источника |
| `canon_status` | enum | `confirmed`, `partially_confirmed`, `unverified`, `speculative`, `duplicate` |
| `confidence` | A–D | см. §6 |
| `first_appearance.chapter` | int \| null | 1..2334 |
| `relations[]` | `{type, target, role, from_chapter, to_chapter}` | типы связей — §5 |
| `game_proposals[]` | `{tag: "GAME_PROPOSAL", field, value}` | единственное место для игровых значений |

Для Гу:

| Поле | Примечание |
|---|---|
| `classification.ranks[].rank` | список зафиксированных рангов. Пустой список = ранг неизвестен; диапазон 1–9 не достраивается |
| `classification.rank_type` | `mortal` / `immortal` / `unknown` |
| `classification.paths[]` | ссылки на `path_*` |
| `feeding.food_raw`, `feeding.foods[]` | `foods` ссылаются на `res_*` |
| `refinement.recipes[]`, `refinement.used_in_recipes[]` | ссылки на `rcp_*` |
| `ownership_history[]` | `{character, role, from_chapter, to_chapter}` |
| `economy.canonical_prices[]` | только проверенные цены; `price_claims` — сырьё, не для игры |

Для персонажей: `cultivation.rank_timeline[]` (`{rank, as_of_chapter}`; это ранг мастера, а не ранг Гу), `cultivation.paths[]`, `biography.region|race|status`, `gu_held[]`, связи `member_of` (фракции).

Поле `attributes` содержит сырые поля инфобокса источника. Это нестабильная часть, движку на неё не опираться.

## 5. Типы связей (v1)

`held_by` (Гу → владелец), `member_of`, `in_region`, `race`, `seat`, `path`, `feeds_on`, `produces`, `uses_move`, `took_part_in`, `linked_inheritance`, `uses_gu_house`, `duplicate_of`. Новые типы добавляются как minor-изменение.

## 6. Тексты и достоверность

- В игру попадают **только** тексты с `text_origin: "own_words"`. Тексты `source_excerpt` (выдержки sagaofgu) остаются только для исследования.
- Порог по умолчанию для экспорта: `confidence ∈ {A, B, C}` и `canon_status ∉ {speculative, duplicate}`. Всё остальное — только с явным флагом в контенте.
- Оговорка: на сегодня записей уровня A нет. B означает «источник указал конкретную главу», но глава не перечитывалась. Более строгий фильтр строится по полю `claims[].verification`.

## 7. Что в лор-базе не хранится

Координаты, границы, точки появления, рельеф, раскладка зданий, распорядки NPC, баланс. Всё это игровые допущения, они живут в игровом контенте и `config/gu_world.php`. Исключение — `game_proposals` с тегом `GAME_PROPOSAL`: это предложения, а не канон.

## 8. Проверка

- Лор-сторона: `python tools/validate.py` проверяет схемы, уникальность и совпадение id с именем файла, ссылки, индексы и источники.
- Сторона движка: экспортёр проверяет major-версию, обязательные поля и существование всех id, на которые ссылается игровой контент.

## 9. Экспорт (сторона движка)

- **Замыкание ссылок, а не вся база.** Экспортёр начинает с id, на которые ссылается игровой контент (`lore_ref` локаций, `template` NPC, позже Гу и предметы), и добирает связи нужных типов на глубину 1. Тысячи записей в рантайм никогда не попадают.
- **Расположение:** `app/Games/GuWorld/client/content/lore/`. Несколько файлов по типам: `characters.json`, `gu.json`, `factions.json`, `locations.json`.
- **Заголовок каждого файла:** `export_version`, `lore_schema_major`, `lore_commit` (sha коммита лор-базы, с которого собран экспорт) и список записей.
- **У каждой записи** остаются `confidence` и `verification` — для отладки.
- **Имена:** движок показывает `names.ru` (`official` / `transliteration` / `working_translation`), запасной вариант — `names.en`. В отладочном режиме `working_translation` помечается.
- Экспортёр и его тесты принадлежат движку. Инструменты лор-базы движок не трогает.

## 10. Игровые локации (сторона движка, для справки)

Это игровые данные, в лор-базе их нет. Описание здесь, чтобы обе стороны видели границу.

- `id`: `^[a-z][a-z0-9_]{0,63}$` (сейчас `test_grounds`, дальше, например, `qing_mao`).
- `lore_ref?`: `loc_*` | `reg_*`. `parent?`: id игровой локации-родителя (региона) с её собственным `lore_ref`.
- `bounds {min_x…max_z}` в метрах, ось Y вверх.
- `spawn {x, y, z, yaw}` хранится в `config/gu_world.php`: сервер проверяет по нему сохранения.
- `chunks {size, simulation_radius, visual_radius}` — параметры загрузки.
- `terrain` — id генератора и его параметры (игровые допущения).
- `objects` — объекты со стабильными runtime-id вида `kind:location:slug` (здания, точки появления NPC, интерактивные предметы). Объект может иметь `template` (лор-id, например `char_fang_yuan` или `loc_…`). Он загружается вместе с чанком, в который попадает его точка привязки.

Канон берётся только через `lore_ref` и `template`. Всё остальное в этом разделе — допущения движка.

## 11. Runtime-id NPC

`npc:<игровая локация или клан>:<slug>`, где slug — лор-id без префикса. Например, `npc:gu_yue:fang_yuan` → `template: "char_fang_yuan"`. Лор-id стабильны (§2), поэтому связь не рвётся. Если персонаж появляется в нескольких местах, это разные NPC с одним и тем же `template`.

## 12. Открыто

- Транслитерация: по умолчанию Палладий (`古月` = «Гу Юэ»), альтернатива «Гу Юе». Решение пользователя будет зафиксировано в `glossary.md`.
- Индекс по арке или главе движку пока не нужен: для постепенного открытия контента хватает `first_appearance.chapter`. Вернёмся к этому, когда в игре появится прогресс сюжета.
