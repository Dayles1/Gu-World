# Глоссарий

Черновик. Одна транслитерация на всю базу: **система Палладия** (`tools/palladius.py`). Китайское написание приводится только при наличии в источнике; там, где источник не даёт иероглифов, стоит «—».

## Принятые правила транслитерации

| Пиньинь | Принято | Альтернатива (не используется) | Пример |
|---|---|---|---|
| yue | юэ | юе | 古月 Gu Yue — **Гу Юэ** (альт. «Гу Юе», ждёт решения пользователя) |
| -an | ань | ан | 方源 Fang Yuan — **Фан Юань** |
| -ang | ан | — | Fang — Фан |
| shi / zhi / chi | ши / чжи / чи | ши / жи / чи | — |
| xi / qi / ji | си / ци / цзи | — | Bai Ning Bing — Бай Нин Бин |
| e | э | е | Shen — Шэнь |

## Термины (рабочие русские варианты)

| EN | ZH | RU (принято) | Варианты | Статус |
|---|---|---|---|---|
| Gu | 蛊 | Гу | гу-червь | термин романа |
| Gu Master | 蛊师 | мастер Гу | — | working_translation |
| Gu Immortal | 蛊仙 | Бессмертный Гу | бессмертный мастер Гу | working_translation |
| Venerable | 尊者 | Почтенный | Достопочтенный | working_translation |
| aperture | 空窍 | апертура | отверстие, полость | working_translation |
| primeval essence | 真元 | первобытная эссенция | первичная эссенция | working_translation |
| immortal essence | 仙元 | бессмертная эссенция | — | working_translation |
| primeval stone | 元石 | первобытный камень | камень первоэссенции | working_translation |
| killer move | 杀招 | убийственный приём | смертельный приём | working_translation |
| Gu house | 蛊屋 | Гу-дом | дом Гу | working_translation |
| dao marks | 道痕 | Дао-метки | метки Дао | working_translation |
| blessed land | 福地 | благословенная земля | — | working_translation |
| grotto-heaven | 洞天 | грот-небо | пещерное небо | working_translation |
| inheritance | 传承 | наследие | наследство | working_translation |
| Spring Autumn Cicada | 春秋蝉 | Весенне-осенняя цикада | цикада Весны и Осени | working_translation |
| Southern Border | 南疆 | Южная граница | Южные рубежи | working_translation |
| Central Continent | 中洲 | Центральный континент | — | working_translation |
| Northern Plains | 北原 | Северные равнины | — | working_translation |
| Eastern Sea | 东海 | Восточное море | — | working_translation |
| Western Desert | 西漠 | Западная пустыня | — | working_translation |

Иероглифы в таблице терминов — **непроверенные (уровень D)**. Они указаны как ориентир для поиска и должны быть подтверждены источником, прежде чем попасть в `names.zh`. В JSON-записи они не переносились.

## Имена собственные

Имена персонажей с китайским написанием находятся в `characters/*.json` (`names.zh`, `names.pinyin`, `names.ru`). Полный поиск по имени — `indexes/name-index.json`.
