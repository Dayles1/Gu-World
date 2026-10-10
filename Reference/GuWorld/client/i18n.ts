/** GU World's words, in the page's language (ru, en or uz). */

import type { DayPhase } from './engine/clock';

interface Texts {
    title: string;
    back: string;
    loading: string;
    load_failed: string;
    retry: string;
    loading_world: string;
    settings_broken: string;
    play: string;
    resume: string;
    paused: string;
    click_to_play: string;
    controls: [string, string][];
    day: string;
    phases: Record<DayPhase, string>;
    /** Location names by id; a location without one shows its id. */
    locations: Record<string, string>;
    saving: string;
    saved: string;
    save_failed: string;
    save_refused: string;
    repaired: string;
    system_disabled: string;
}

const TEXTS: Record<'ru' | 'en' | 'uz', Texts> = {
    ru: {
        title: 'GU World',
        back: 'Игры',
        loading: 'Загрузка…',
        load_failed:
            'Не удалось загрузить сохранение. Игра не начата, чтобы не затереть его.',
        retry: 'Повторить',
        loading_world: 'Загрузка мира…',
        settings_broken: 'Настройки мира повреждены',
        play: 'Играть',
        resume: 'Продолжить',
        paused: 'Пауза',
        click_to_play: 'Нажмите, чтобы играть. Esc — пауза.',
        controls: [
            ['W A S D', 'ходьба · Shift — бег'],
            ['Пробел', 'прыжок · залезть на уступ'],
            ['C · Z', 'присесть · ползти'],
            ['Мышь', 'камера · колесо — ближе/дальше'],
            ['F3 · F6', 'отладка · пометить ближайший предмет'],
        ],
        day: 'День',
        phases: {
            night: 'ночь',
            dawn: 'рассвет',
            morning: 'утро',
            day: 'день',
            evening: 'вечер',
        },
        locations: { test_grounds: 'Полигон (проверка движка)' },
        saving: 'Сохранение…',
        saved: 'Сохранено',
        save_failed: 'Не удалось сохранить — попробуем снова',
        save_refused: 'Сохранение отклонено: данные повреждены',
        repaired: 'Сохранение восстановлено',
        system_disabled: 'Система отключена из-за ошибок',
    },
    en: {
        title: 'GU World',
        back: 'Games',
        loading: 'Loading…',
        load_failed:
            'Could not load the saved game. The game has not started, so as not to overwrite it.',
        retry: 'Try again',
        loading_world: 'Loading the world…',
        settings_broken: 'The world’s settings are broken',
        play: 'Play',
        resume: 'Resume',
        paused: 'Paused',
        click_to_play: 'Click to play. Esc pauses.',
        controls: [
            ['W A S D', 'walk · Shift to run'],
            ['Space', 'jump · climb a ledge'],
            ['C · Z', 'crouch · crawl'],
            ['Mouse', 'camera · wheel to zoom'],
            ['F3 · F6', 'debug · mark the nearest thing'],
        ],
        day: 'Day',
        phases: {
            night: 'night',
            dawn: 'dawn',
            morning: 'morning',
            day: 'day',
            evening: 'evening',
        },
        locations: { test_grounds: 'Proving ground (engine test)' },
        saving: 'Saving…',
        saved: 'Saved',
        save_failed: 'Could not save — will try again',
        save_refused: 'Save refused: the data is broken',
        repaired: 'The save was repaired',
        system_disabled: 'A system was switched off after errors',
    },
    uz: {
        title: 'GU World',
        back: 'O‘yinlar',
        loading: 'Yuklanmoqda…',
        load_failed:
            'Saqlangan o‘yinni yuklab bo‘lmadi. Uni o‘chirib yubormaslik uchun o‘yin boshlanmadi.',
        retry: 'Qayta urinish',
        loading_world: 'Dunyo yuklanmoqda…',
        settings_broken: 'Dunyo sozlamalari buzilgan',
        play: 'O‘ynash',
        resume: 'Davom etish',
        paused: 'Pauza',
        click_to_play: 'O‘ynash uchun bosing. Esc — pauza.',
        controls: [
            ['W A S D', 'yurish · Shift — yugurish'],
            ['Probel', 'sakrash · zinaga chiqish'],
            ['C · Z', 'cho‘kkalash · emaklash'],
            ['Sichqoncha', 'kamera · g‘ildirak — yaqin/uzoq'],
            ['F3 · F6', 'nosozliklar · eng yaqin narsani belgilash'],
        ],
        day: 'Kun',
        phases: {
            night: 'tun',
            dawn: 'tong',
            morning: 'ertalab',
            day: 'kunduz',
            evening: 'kechqurun',
        },
        locations: { test_grounds: 'Sinov maydoni (dvigatel sinovi)' },
        saving: 'Saqlanmoqda…',
        saved: 'Saqlandi',
        save_failed: 'Saqlab bo‘lmadi — yana urinamiz',
        save_refused: 'Saqlash rad etildi: ma’lumotlar buzilgan',
        repaired: 'Saqlangan o‘yin tiklandi',
        system_disabled: 'Xatolar tufayli tizim o‘chirildi',
    },
};

function language(): keyof typeof TEXTS {
    const lang = document.documentElement.lang.slice(0, 2).toLowerCase();

    return lang === 'en' || lang === 'uz' ? lang : 'ru';
}

export const t: Texts = TEXTS[language()];
