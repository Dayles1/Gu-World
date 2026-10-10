/**
 * How a person looks: gender, how the body is drawn (realistic or anime),
 * hairstyle, hair colour, beard and eye colour. Copied from the Sandbox's
 * hero looks; in GU World it is plain data for any body — the hero's or,
 * later, a villager's.
 */

export type Gender = 'male' | 'female';

/** How a body is drawn: a realistic body or an anime one. */
export type BodyStyle = 'realistic' | 'anime';

export type HairStyle =
    'short' | 'parted' | 'buns' | 'bob' | 'ponytail' | 'long' | 'bald';
export type HairColor =
    | 'black'
    | 'brown'
    | 'chestnut'
    | 'auburn'
    | 'blonde'
    | 'platinum'
    | 'pink'
    | 'blue';
export type Beard = 'none' | 'stubble' | 'mustache' | 'goatee' | 'full';
export type EyeColor = 'brown' | 'hazel' | 'green' | 'blue' | 'grey' | 'violet';

export interface Appearance {
    hair: HairStyle;
    hair_color: HairColor;
    beard: Beard;
    eyes: EyeColor;
}

export const HAIR_STYLES: HairStyle[] = [
    'short',
    'parted',
    'buns',
    'bob',
    'ponytail',
    'long',
    'bald',
];

export const HAIR_COLORS: Record<HairColor, number> = {
    black: 0x1c1b22,
    brown: 0x4b2e1c,
    chestnut: 0x7a3f1e,
    auburn: 0x9c3a1a,
    blonde: 0xd9b86c,
    platinum: 0xe8e4dc,
    pink: 0xe88bb2,
    blue: 0x4677cf,
};

export const HAIR_COLOR_NAMES = Object.keys(HAIR_COLORS) as HairColor[];

export const BEARDS: Beard[] = [
    'none',
    'stubble',
    'mustache',
    'goatee',
    'full',
];

export const EYE_COLORS: Record<EyeColor, number> = {
    brown: 0x6b3a18,
    hazel: 0x8f6a26,
    green: 0x3d8f4c,
    blue: 0x3d80d6,
    grey: 0x8b98a3,
    violet: 0x8d50cc,
};

export const EYE_COLOR_NAMES = Object.keys(EYE_COLORS) as EyeColor[];

/** The look of a hero who has not chosen one: as the models come. */
export function defaultAppearance(gender: Gender): Appearance {
    return gender === 'female'
        ? { hair: 'buns', hair_color: 'chestnut', beard: 'none', eyes: 'brown' }
        : { hair: 'parted', hair_color: 'brown', beard: 'none', eyes: 'brown' };
}

/** A saved look, each part checked (a wrong one is the default's). */
export function readAppearance(saved: unknown, gender: Gender): Appearance {
    const source = (saved ?? {}) as Partial<Appearance>;
    const fallback = defaultAppearance(gender);

    return {
        hair: HAIR_STYLES.includes(source.hair as HairStyle)
            ? source.hair!
            : fallback.hair,
        hair_color: HAIR_COLOR_NAMES.includes(source.hair_color as HairColor)
            ? source.hair_color!
            : fallback.hair_color,
        beard: BEARDS.includes(source.beard as Beard)
            ? source.beard!
            : fallback.beard,
        eyes: EYE_COLOR_NAMES.includes(source.eyes as EyeColor)
            ? source.eyes!
            : fallback.eyes,
    };
}
