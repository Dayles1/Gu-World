import { describe, expect, it } from 'vitest';
import {
    BLOCKS,
    GENTLE_MOUND,
    STEEP_MOUND,
    testGrounds,
} from '../content/test-grounds';
import { SIM_STEP } from '../engine/loop';
import { Character, RADIUS, STANCE_SPEED } from './character';
import type { CharacterInput } from './character';
import { ColliderGrid } from './colliders';
import { colliderFor } from './solids';

const ground = testGrounds.ground;
const bounds = {
    minX: -80,
    maxX: 80,
    minY: -20,
    maxY: 120,
    minZ: -80,
    maxZ: 80,
};

function body(): Character {
    const grid = new ColliderGrid(ground);

    for (const block of BLOCKS) {
        grid.add(colliderFor(block, ground));
    }

    return new Character(grid, ground, bounds);
}

const still: CharacterInput = {
    moveX: 0,
    moveZ: 0,
    sprint: false,
    jump: false,
    speedScale: 1,
    rise: false,
    dive: false,
};

/** Runs the body for `seconds` of simulation steps, checking it never sinks into the ground. */
function run(
    character: Character,
    seconds: number,
    input: Partial<CharacterInput> = {},
): void {
    for (let t = 0; t < seconds; t += SIM_STEP) {
        character.update(SIM_STEP, { ...still, ...input });
        const { x, y, z } = character.position;

        expect(y).toBeGreaterThanOrEqual(ground.heightAt(x, z) - 0.02);
    }
}

const block = (name: string) =>
    BLOCKS.find((each) => each.id === `block:test_grounds:${name}`)!;

describe('the body on the proving ground', () => {
    it('falls, lands and stands on flat ground without sinking', () => {
        const character = body();

        character.placeAt(0, 6, 40);
        run(character, 2);

        expect(character.grounded).toBe(true);
        expect(character.position.y).toBeCloseTo(ground.heightAt(0, 40), 2);
    });

    it('walks at walking speed on the flat', () => {
        const character = body();

        character.placeAt(0, 0, 40);
        run(character, 2, { moveX: 1 });

        const [walk] = STANCE_SPEED.stand;

        expect(character.position.x).toBeGreaterThan(walk * 1.5);
        expect(character.position.x).toBeLessThan(walk * 2.01);
    });

    it('walks up the gentle mound', () => {
        const character = body();
        const startX = GENTLE_MOUND.x - 18;

        character.placeAt(startX, 0, GENTLE_MOUND.z);
        run(character, 3.5, { moveX: 1 });

        expect(character.position.x).toBeGreaterThan(startX + 10);
        expect(character.position.y).toBeGreaterThan(GENTLE_MOUND.height * 0.6);
        expect(character.grounded).toBe(true);
    });

    it('slides down the steep mound instead of standing on it', () => {
        const character = body();
        const startX = STEEP_MOUND.x + 3;

        character.placeAt(startX, 10, STEEP_MOUND.z);
        const startY = ground.heightAt(startX, STEEP_MOUND.z);
        run(character, 3);

        expect(character.position.x).toBeGreaterThan(startX + 1);
        expect(character.position.y).toBeLessThan(startY - 1);
    });

    it('cannot walk through the wall', () => {
        const character = body();
        const wall = block('wall');

        character.placeAt(wall.x + 4, 0, wall.z);
        run(character, 3, { moveX: -1 });

        expect(character.position.x).toBeGreaterThanOrEqual(
            wall.x + wall.width / 2 + RADIUS - 0.02,
        );
    });

    it('walks up the steps onto the platform', () => {
        const character = body();
        const platform = block('platform');

        character.placeAt(4, 0, platform.z);

        // Up the steps until the middle of the platform (not off its far side).
        for (let t = 0; t < 5 && character.position.x < platform.x; t += 0.1) {
            run(character, 0.1, { moveX: 1 });
        }

        run(character, 0.5);

        expect(character.position.x).toBeGreaterThan(platform.x - 1);
        expect(character.position.x).toBeLessThan(
            platform.x + platform.width / 2,
        );
        expect(character.grounded).toBe(true);
        // Blocks stand on the ground, which rises a little toward the mound.
        expect(character.position.y).toBeCloseTo(
            ground.heightAt(platform.x, platform.z) + platform.height,
            1,
        );
    });

    it('climbs onto the crate, which is too high to step onto', () => {
        const character = body();
        const crate = block('crate');

        character.placeAt(crate.x + 1.2, 0, crate.z);
        run(character, 0.5);

        expect(character.tryClimb(-1, 0)).toBe(true);

        run(character, 2);

        expect(character.position.y).toBeCloseTo(
            ground.heightAt(crate.x, crate.z) + crate.height,
            1,
        );
    });

    it('does not fall through the ground however long the frames are', () => {
        const character = body();

        character.placeAt(30, 60, 30);

        for (let frame = 0; frame < 120; frame++) {
            character.update(0.5, still);
            const { x, y, z } = character.position;

            expect(y).toBeGreaterThanOrEqual(ground.heightAt(x, z) - 0.02);
        }

        expect(character.grounded).toBe(true);
    });

    it('stays inside the location', () => {
        const character = body();

        character.placeAt(70, 0, 0);
        run(character, 6, { moveX: 1, sprint: true });

        expect(character.position.x).toBeCloseTo(
            bounds.maxX - Character.EDGE,
            5,
        );
    });
});
