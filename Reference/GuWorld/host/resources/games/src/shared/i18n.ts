/**
 * Strings of the games shell (the menu). The locale is whatever the server
 * rendered into <html lang>, i.e. the user's app language.
 */

type Dictionary = typeof ru;

/** A phrase in each plural form a language has; `other` is the fallback. */
type PluralForms = Partial<Record<Intl.LDMLPluralRule, string>> & {
    other: string;
};

const ru = {
    games: 'Игры',
    subtitle: 'Мини-игры, чтобы отдохнуть пять минут или пару вечеров.',
    back_to_app: 'В приложение',
    all: 'Все',
    play: 'Играть',
    continue: 'Продолжить',
    soon: 'Скоро',
    new: 'Новинка',
    empty: 'В этом жанре пока нет игр.',
    progress_city: ':year год · :epoch · :population жителей',
    progress_epochs: ':year год · :epoch · :population жителей',
    progress_sandbox: ':score очков · побед: :kills · артефактов: :artifacts',
    reset: 'Сбросить прогресс',
    reset_confirm:
        'Сбросить прогресс в игре «:game»? Город, достижения и открытия будут удалены навсегда.',
    reset_confirm_sandbox:
        'Сбросить прогресс в игре «:game»? Герой, вещи, постройки, изученные рецепты и статистика будут удалены навсегда.',
    reset_done: 'Прогресс сброшен',
    rating_none: 'Ещё нет оценок',
    ratings_count: {
        one: ':n оценка',
        few: ':n оценки',
        many: ':n оценок',
        other: ':n оценки',
    } as PluralForms,
    rate: 'Оценить',
    rate_edit: 'Ваша оценка: :stars',
    leaderboard: 'Рейтинг',
    close: 'Закрыть',
    loading: 'Загрузка…',
    load_error: 'Не удалось загрузить. Попробуйте ещё раз.',
    rate_title: 'Оцените «:game»',
    stars_label: {
        one: ':n звезда',
        few: ':n звезды',
        many: ':n звёзд',
        other: ':n звезды',
    } as PluralForms,
    rate_comment: 'Комментарий (необязательно)',
    rate_comment_placeholder: 'Что понравилось, а что стоит улучшить?',
    rate_save: 'Сохранить',
    rate_delete: 'Удалить оценку',
    rate_saved: 'Спасибо за оценку!',
    rate_pick: 'Выберите от 1 до 5 звёзд.',
    rate_locked: 'Сыграйте ещё :n мин, чтобы оценить',
    rate_locked_hint:
        'Оценки ставят те, кто провёл в игре хотя бы 5 минут — так отзывы честнее.',
    board_title: 'Рейтинг: :game',
    tab_leaders: 'Лучшие игроки',
    tab_reviews: 'Отзывы',
    col_rank: 'Место',
    col_player: 'Игрок',
    col_progress: 'Прогресс',
    col_score: 'Очки',
    board_empty: 'Пока никто не попал в рейтинг — станьте первым!',
    board_me: 'Ваше место',
    board_you: 'вы',
    board_not_played: 'Сыграйте, чтобы попасть в рейтинг.',
    board_details: ':epoch · :population жит.',
    board_details_sandbox:
        'побед: :kills · деревьев: :trees · артефактов: :artifacts',
    board_anonymous: 'Игрок',
    reviews_empty: 'Отзывов с комментариями пока нет.',
    reviews_distribution: 'Распределение оценок',
    genres: {
        strategy: 'Стратегии',
        party: 'Для компании',
        quiz: 'Викторины',
        duel: 'Дуэли',
        puzzle: 'Головоломки',
        adventure: 'Приключения',
    },
    epochs: [
        'Раннее поселение',
        'Деревня',
        'Средневековый город',
        'Королевство',
        'Раннее новое время',
        'Торговый город',
        'Индустриальная эпоха',
        'Современный город',
        'Мегаполис',
    ],
    titles: {
        'gu-world': [
            'GU World',
            'Мир Мастеров Гу по мотивам Reverend Insanity. В разработке: сейчас строится основа мира.',
        ],
        sandbox: [
            'Песочница',
            'Открытый мир без кубиков: холмы, горы, лес и камни. Пока можно бегать, прыгать и исследовать — дальше будет больше.',
        ],
        epochs: [
            'Летопись города 2',
            'Десять эпох от античности до 3000 года: стройте в 3D, улучшайте дома от шалаша до нанобашни, изучайте технологии, открывайте чертежи и создавайте свои округа.',
        ],
        city: [
            'Летопись города',
            'Начните с пары хижин в 1000 году и проведите поселение через девять эпох — до сияющего мегаполиса.',
        ],
        dice: ['Кости', 'Бросьте кубик в чате и узнайте, кому сегодня везёт.'],
        quiz: ['Викторина', 'Вопросы на скорость для всей команды.'],
        tictactoe: ['Крестики-нолики', 'Классика один на один с коллегой.'],
        battleship: [
            'Морской бой',
            'Расставьте флот и потопите корабли соперника.',
        ],
        words: ['Слова', 'Угадайте слово из пяти букв за шесть попыток.'],
    } as Record<string, [string, string]>,
};

const en: Dictionary = {
    games: 'Games',
    subtitle: 'Mini games for a five-minute break or a couple of evenings.',
    back_to_app: 'Back to app',
    all: 'All',
    play: 'Play',
    continue: 'Continue',
    soon: 'Soon',
    new: 'New',
    empty: 'No games in this genre yet.',
    progress_city: 'Year :year · :epoch · :population residents',
    progress_epochs: 'Year :year · :epoch · :population residents',
    progress_sandbox: ':score points · :kills beaten · artifacts: :artifacts',
    reset: 'Reset progress',
    reset_confirm:
        'Reset your progress in “:game”? The city, achievements and discoveries will be deleted for good.',
    reset_confirm_sandbox:
        'Reset your progress in “:game”? The hero, things, buildings, learnt recipes and stats will be deleted for good.',
    reset_done: 'Progress reset',
    rating_none: 'No ratings yet',
    ratings_count: { one: ':n rating', other: ':n ratings' },
    rate: 'Rate',
    rate_edit: 'Your rating: :stars',
    leaderboard: 'Leaderboard',
    close: 'Close',
    loading: 'Loading…',
    load_error: 'Could not load. Please try again.',
    rate_title: 'Rate “:game”',
    stars_label: { one: ':n star', other: ':n stars' },
    rate_comment: 'Comment (optional)',
    rate_comment_placeholder: 'What did you like, and what could be better?',
    rate_save: 'Save',
    rate_delete: 'Delete rating',
    rate_saved: 'Thanks for rating!',
    rate_pick: 'Pick 1 to 5 stars.',
    rate_locked: 'Play :n more min to rate',
    rate_locked_hint:
        'Ratings come from players who spent at least 5 minutes in the game — it keeps reviews honest.',
    board_title: 'Leaderboard: :game',
    tab_leaders: 'Top players',
    tab_reviews: 'Reviews',
    col_rank: 'Rank',
    col_player: 'Player',
    col_progress: 'Progress',
    col_score: 'Score',
    board_empty: 'Nobody is on the leaderboard yet — be the first!',
    board_me: 'Your place',
    board_you: 'you',
    board_not_played: 'Play to get on the leaderboard.',
    board_details: ':epoch · :population res.',
    board_details_sandbox:
        ':kills beaten · :trees trees · :artifacts artifacts',
    board_anonymous: 'Player',
    reviews_empty: 'No reviews with comments yet.',
    reviews_distribution: 'Rating distribution',
    genres: {
        strategy: 'Strategy',
        party: 'Party',
        quiz: 'Quizzes',
        duel: 'Duels',
        puzzle: 'Puzzles',
        adventure: 'Adventure',
    },
    epochs: [
        'Early settlement',
        'Village',
        'Medieval town',
        'Kingdom',
        'Early modern age',
        'Trading city',
        'Industrial age',
        'Modern city',
        'Megalopolis',
    ],
    titles: {
        'gu-world': [
            'GU World',
            'A world of Gu Masters inspired by Reverend Insanity. In development: the world’s foundation is being built.',
        ],
        sandbox: [
            'Sandbox',
            'An open world without cubes: hills, mountains, woods and rocks. For now you can run, jump and explore — more is coming.',
        ],
        epochs: [
            'City Chronicle 2',
            'Ten eras from antiquity to the year 3000: build in 3D, grow homes from a hut to a nano tower, research technologies, unlock blueprints and found your own districts.',
        ],
        city: [
            'City Chronicle',
            'Start with a few huts in the year 1000 and lead your settlement through nine eras to a shining megalopolis.',
        ],
        dice: ['Dice', 'Roll a die in the chat and see who is lucky today.'],
        quiz: ['Quiz', 'Quick-fire questions for the whole team.'],
        tictactoe: ['Tic-tac-toe', 'The classic, one on one with a colleague.'],
        battleship: [
            'Battleship',
            'Place your fleet and sink your opponent’s ships.',
        ],
        words: ['Words', 'Guess the five-letter word in six tries.'],
    },
};

const uz: Dictionary = {
    games: 'O‘yinlar',
    subtitle:
        'Besh daqiqalik tanaffus yoki bir necha oqshom uchun mini o‘yinlar.',
    back_to_app: 'Ilovaga qaytish',
    all: 'Hammasi',
    play: 'O‘ynash',
    continue: 'Davom etish',
    soon: 'Tez orada',
    new: 'Yangi',
    empty: 'Bu janrda hozircha o‘yinlar yo‘q.',
    progress_city: ':year-yil · :epoch · :population aholi',
    progress_epochs: ':year-yil · :epoch · :population aholi',
    progress_sandbox:
        ':score ochko · g‘alabalar: :kills · artefaktlar: :artifacts',
    reset: 'Jarayonni tiklash',
    reset_confirm:
        '«:game» o‘yinidagi jarayon o‘chirilsinmi? Shahar, yutuqlar va kashfiyotlar butunlay o‘chiriladi.',
    reset_confirm_sandbox:
        '«:game» o‘yinidagi jarayon o‘chirilsinmi? Qahramon, buyumlar, qurilmalar, o‘rganilgan retseptlar va statistika butunlay o‘chiriladi.',
    reset_done: 'Jarayon tiklandi',
    rating_none: 'Hali baholar yo‘q',
    ratings_count: { other: ':n ta baho' },
    rate: 'Baholash',
    rate_edit: 'Sizning bahoingiz: :stars',
    leaderboard: 'Reyting',
    close: 'Yopish',
    loading: 'Yuklanmoqda…',
    load_error: 'Yuklab bo‘lmadi. Qaytadan urinib ko‘ring.',
    rate_title: '«:game» o‘yinini baholang',
    stars_label: { other: ':n yulduz' },
    rate_comment: 'Izoh (ixtiyoriy)',
    rate_comment_placeholder: 'Nima yoqdi, nimani yaxshilash kerak?',
    rate_save: 'Saqlash',
    rate_delete: 'Bahoni o‘chirish',
    rate_saved: 'Baho uchun rahmat!',
    rate_pick: '1 dan 5 gacha yulduz tanlang.',
    rate_locked: 'Baholash uchun yana :n daqiqa o‘ynang',
    rate_locked_hint:
        'O‘yinda kamida 5 daqiqa o‘tkazganlar baho qo‘yadi — shunda sharhlar halolroq bo‘ladi.',
    board_title: 'Reyting: :game',
    tab_leaders: 'Eng yaxshi o‘yinchilar',
    tab_reviews: 'Sharhlar',
    col_rank: 'O‘rin',
    col_player: 'O‘yinchi',
    col_progress: 'Natija',
    col_score: 'Ochko',
    board_empty: 'Reytingda hali hech kim yo‘q — birinchi bo‘ling!',
    board_me: 'Sizning o‘rningiz',
    board_you: 'siz',
    board_not_played: 'Reytingga kirish uchun o‘ynang.',
    board_details: ':epoch · :population aholi',
    board_details_sandbox:
        'g‘alabalar: :kills · daraxtlar: :trees · artefaktlar: :artifacts',
    board_anonymous: 'O‘yinchi',
    reviews_empty: 'Izohli sharhlar hali yo‘q.',
    reviews_distribution: 'Baholar taqsimoti',
    genres: {
        strategy: 'Strategiyalar',
        party: 'Davra uchun',
        quiz: 'Viktorinalar',
        duel: 'Duellar',
        puzzle: 'Boshqotirmalar',
        adventure: 'Sarguzashtlar',
    },
    epochs: [
        'Ilk manzilgoh',
        'Qishloq',
        'O‘rta asrlar shahri',
        'Qirollik',
        'Ilk yangi davr',
        'Savdo shahri',
        'Sanoat davri',
        'Zamonaviy shahar',
        'Megapolis',
    ],
    titles: {
        'gu-world': [
            'GU World',
            'Reverend Insanity asosidagi Gu ustalari dunyosi. Ishlab chiqilmoqda: dunyo poydevori qurilmoqda.',
        ],
        sandbox: [
            'Qumdon',
            'Kubiklarsiz ochiq dunyo: tepaliklar, tog‘lar, o‘rmon va toshlar. Hozircha yugurish, sakrash va kezish mumkin — davomi bor.',
        ],
        epochs: [
            'Shahar solnomasi 2',
            'Antik davrdan 3000-yilgacha o‘nta davr: 3D’da quring, uylarni kulbadan nanominoragacha yaxshilang, texnologiyalarni o‘rganing, chizmalarni oching va o‘z tumanlaringizni yarating.',
        ],
        city: [
            'Shahar solnomasi',
            '1000-yilda bir nechta kulbadan boshlang va manzilgohingizni to‘qqiz davr orqali megapolisgacha olib boring.',
        ],
        dice: [
            'Zar',
            'Chatda zar tashlang va bugun kimga omad kulishini biling.',
        ],
        quiz: ['Viktorina', 'Butun jamoa uchun tezkor savollar.'],
        tictactoe: ['X va O', 'Hamkasb bilan yakkama-yakka klassika.'],
        battleship: [
            'Dengiz jangi',
            'Flotingizni joylang va raqib kemalarini cho‘ktiring.',
        ],
        words: ['So‘zlar', 'Besh harfli so‘zni olti urinishda toping.'],
    },
};

const DICTIONARIES: Record<string, Dictionary> = { ru, en, uz };

export const locale = (document.documentElement.lang || 'ru').slice(0, 2);

export const t: Dictionary = DICTIONARIES[locale] ?? ru;

export function format(
    template: string,
    values: Record<string, string | number>,
): string {
    return Object.entries(values).reduce(
        (text, [key, value]) => text.replace(`:${key}`, String(value)),
        template,
    );
}

const pluralRules = new Intl.PluralRules(locale);

/** Picks the plural form for `count` and puts the number in for `:n`. */
export function plural(forms: PluralForms, count: number): string {
    const form = forms[pluralRules.select(count)] ?? forms.other;

    return form.replace(':n', new Intl.NumberFormat(locale).format(count));
}
