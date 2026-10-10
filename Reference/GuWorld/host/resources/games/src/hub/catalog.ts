/**
 * Every game in the Games menu, in menu order. A new game is an entry here
 * plus its own folder under src/ (and, if it saves anything, under
 * app/Games on the server).
 */

export type Genre =
    'strategy' | 'adventure' | 'party' | 'quiz' | 'duel' | 'puzzle';

export interface GameEntry {
    slug: string;
    cover: string;
    hue: string;
    genre: Genre;
    /** Client route; null while the game is not out yet. */
    path: string | null;
    /**
     * A self-contained game with a page of its own (outside this SPA):
     * opened with a full page load instead of `path`.
     */
    href?: string;
    /** API path (under /api/games) that deletes the player's progress. */
    resetPath?: string;
    /**
     * A game with an API of its own (like the Sandbox's /api/sandbox): the
     * hub reads its leaderboard and the card's progress line there
     * (`/leaderboard`, `/summary`) and resets it with DELETE `/player`.
     */
    api?: string;
}

export const CATALOG: GameEntry[] = [
    {
        slug: 'epochs',
        cover: '🏙️',
        hue: '#35b8e0',
        genre: 'strategy',
        path: '/city2',
        resetPath: 'epochs/world',
    },
    {
        slug: 'gu-world',
        cover: '🐛',
        hue: '#a8261f',
        genre: 'adventure',
        path: null,
        href: '/games/gu-world',
    },
    {
        slug: 'sandbox',
        cover: '🏃',
        hue: '#7c8b9a',
        genre: 'adventure',
        path: null,
        href: '/games/sandbox',
        api: '/api/sandbox',
    },
    {
        slug: 'city',
        cover: '🏰',
        hue: '#e8a33d',
        genre: 'strategy',
        path: '/city',
        resetPath: 'city/save',
    },
    { slug: 'dice', cover: '🎲', hue: '#8b5cf6', genre: 'party', path: null },
    { slug: 'quiz', cover: '🧠', hue: '#0ea5e9', genre: 'quiz', path: null },
    {
        slug: 'tictactoe',
        cover: '⭕',
        hue: '#f43f5e',
        genre: 'duel',
        path: null,
    },
    {
        slug: 'battleship',
        cover: '🚢',
        hue: '#3b82f6',
        genre: 'duel',
        path: null,
    },
    { slug: 'words', cover: '🔤', hue: '#14b8a6', genre: 'puzzle', path: null },
];
