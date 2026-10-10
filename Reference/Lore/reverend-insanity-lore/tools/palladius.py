"""Toneless pinyin -> Russian (Palladius system) for romanized Chinese names."""
import re

INITIALS = [('zh', 'чж'), ('ch', 'ч'), ('sh', 'ш'), ('b', 'б'), ('p', 'п'), ('m', 'м'), ('f', 'ф'), ('d', 'д'), ('t', 'т'),
            ('n', 'н'), ('l', 'л'), ('g', 'г'), ('k', 'к'), ('h', 'х'), ('j', 'цз'), ('q', 'ц'), ('x', 'с'), ('r', 'ж'),
            ('z', 'цз'), ('c', 'ц'), ('s', 'с'), ('y', ''), ('w', '')]
FINALS = {
    'a': 'а', 'o': 'о', 'e': 'э', 'ai': 'ай', 'ei': 'эй', 'ao': 'ао', 'ou': 'оу', 'an': 'ань', 'en': 'энь', 'ang': 'ан',
    'eng': 'эн', 'ong': 'ун', 'er': 'эр', 'i': 'и', 'ia': 'я', 'ie': 'е', 'iao': 'яо', 'iu': 'ю', 'ian': 'янь', 'in': 'инь',
    'iang': 'ян', 'ing': 'ин', 'iong': 'юн', 'u': 'у', 'ua': 'уа', 'uo': 'о', 'uai': 'уай', 'ui': 'уй', 'uan': 'уань',
    'un': 'унь', 'uang': 'уан', 'ueng': 'ун', 'v': 'юй', 've': 'юэ', 'van': 'юань', 'vn': 'юнь',
}
Y_SYL = {'ya': 'я', 'yo': 'ё', 'ye': 'е', 'yao': 'яо', 'you': 'ю', 'yan': 'янь', 'yin': 'инь', 'yang': 'ян', 'ying': 'ин',
         'yong': 'юн', 'yi': 'и', 'yu': 'юй', 'yue': 'юэ', 'yuan': 'юань', 'yun': 'юнь', 'wa': 'ва', 'wo': 'во', 'wai': 'вай',
         'wei': 'вэй', 'wan': 'вань', 'wen': 'вэнь', 'wang': 'ван', 'weng': 'вэн', 'wu': 'у'}
SPECIAL = {'zi': 'цзы', 'ci': 'цы', 'si': 'сы', 'zhi': 'чжи', 'chi': 'чи', 'shi': 'ши', 'ri': 'жи', 'e': 'э', 'er': 'эр',
           'zhe': 'чжэ', 'che': 'чэ', 'she': 'шэ', 're': 'жэ', 'ze': 'цзэ', 'ce': 'цэ', 'se': 'сэ', 'yo': 'ё', 'lv': 'люй', 'nv': 'нюй',
           'lve': 'люэ', 'nve': 'нюэ', 'ju': 'цзюй', 'qu': 'цюй', 'xu': 'сюй', 'jue': 'цзюэ', 'que': 'цюэ', 'xue': 'сюэ',
           'juan': 'цзюань', 'quan': 'цюань', 'xuan': 'сюань', 'jun': 'цзюнь', 'qun': 'цюнь', 'xun': 'сюнь'}


def syllable(s):
    s = s.lower()
    if s in SPECIAL:
        return SPECIAL[s]
    if s in Y_SYL:
        return Y_SYL[s]
    for ini, ru in INITIALS:
        if s.startswith(ini) and ini not in ('y', 'w'):
            fin = s[len(ini):]
            if fin in FINALS:
                r = FINALS[fin]
                if ini in ('j', 'q', 'x') and fin == 'u':
                    r = 'юй'
                if fin == 'e' and ini in ('g', 'k', 'h', 'd', 't', 'n', 'l', 'm', 'b', 'p', 'f'):
                    r = 'э'
                if fin == 'o' and ini in ('b', 'p', 'm', 'f'):
                    r = 'о'
                return ru + r
            return None
    return FINALS.get(s)


def pinyin_of(name):
    """Return the name if every space-separated token is a single valid pinyin syllable, else None."""
    tokens = name.split()
    if not tokens or any(syllable(t) is None for t in tokens):
        return None
    return ' '.join(t.lower() for t in tokens)


def palladius(pinyin):
    words = [syllable(t) for t in pinyin.split()]
    return ' '.join(w.capitalize() for w in words)
