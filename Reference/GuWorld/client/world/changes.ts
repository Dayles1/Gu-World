/**
 * What has changed in the world since it was made: per entity id, that it
 * is gone, or the values of its state that differ from how its content
 * made it. Kept apart from the entities, so it outlives a chunk being let
 * go (and is applied again when the chunk comes back), and keyed by id,
 * not by chunk, so it does not care how the world is cut up. Saved with
 * the game.
 *
 * Every value is checked: a key is a short snake_case name, a value a
 * boolean, a finite number or a short string; there are at most
 * MAX_CHANGES entities with changes and MAX_STATE_KEYS values each.
 * Restoring a save drops anything else and says why.
 */

import { isEntityId } from '../entities/registry';
import type { EntityId } from '../entities/registry';

export type StateValue = boolean | number | string;
export type EntityState = Record<string, StateValue>;

export interface EntityChange {
    removed?: true;
    state?: EntityState;
}

export const MAX_CHANGES = 1000;
export const MAX_STATE_KEYS = 16;
export const MAX_STRING = 64;
export const STATE_KEY = /^[a-z][a-z0-9_]{0,31}$/;

export function isStateValue(value: unknown): value is StateValue {
    return (
        typeof value === 'boolean' ||
        (typeof value === 'number' && Number.isFinite(value)) ||
        (typeof value === 'string' && value.length <= MAX_STRING)
    );
}

export class WorldChangesError extends Error {}

export class WorldChanges {
    private changes = new Map<EntityId, EntityChange>();

    get size(): number {
        return this.changes.size;
    }

    get(id: EntityId): EntityChange | undefined {
        return this.changes.get(id);
    }

    isRemoved(id: EntityId): boolean {
        return this.changes.get(id)?.removed === true;
    }

    /** The changed state of an entity (empty when nothing changed). */
    state(id: EntityId): EntityState {
        return { ...this.changes.get(id)?.state };
    }

    setState(id: EntityId, key: string, value: StateValue): void {
        if (!isEntityId(id) || !STATE_KEY.test(key) || !isStateValue(value)) {
            throw new WorldChangesError(
                `Cannot set ${JSON.stringify(key)} of "${id}" to ${JSON.stringify(value)}`,
            );
        }

        const change = this.entry(id);
        const state = change.state ?? {};

        if (!(key in state) && Object.keys(state).length >= MAX_STATE_KEYS) {
            throw new WorldChangesError(`"${id}" has too many changed values`);
        }

        state[key] = value;
        change.state = state;
    }

    remove(id: EntityId): void {
        this.entry(id).removed = true;
    }

    /** Back to how the content made it. */
    forget(id: EntityId): void {
        this.changes.delete(id);
    }

    toJSON(): Record<EntityId, EntityChange> {
        return Object.fromEntries(
            [...this.changes].map(([id, change]) => [
                id,
                {
                    ...(change.removed ? { removed: true as const } : {}),
                    ...(change.state ? { state: { ...change.state } } : {}),
                },
            ]),
        );
    }

    /** Changes from a save, every part checked; what cannot be used is dropped with a reason. */
    static restore(raw: unknown): {
        changes: WorldChanges;
        problems: string[];
    } {
        const changes = new WorldChanges();
        const problems: string[] = [];

        if (raw === undefined || raw === null) {
            return { changes, problems };
        }

        if (typeof raw !== 'object' || Array.isArray(raw)) {
            return { changes, problems: ['world changes are not an object'] };
        }

        for (const [id, value] of Object.entries(raw)) {
            if (changes.size >= MAX_CHANGES) {
                problems.push(
                    `more than ${MAX_CHANGES} world changes: the rest dropped`,
                );
                break;
            }

            const change = value as Partial<EntityChange> | null;

            if (
                !isEntityId(id) ||
                typeof change !== 'object' ||
                change === null
            ) {
                problems.push(
                    `a broken world change for ${JSON.stringify(id)} dropped`,
                );
                continue;
            }

            if (change.removed === true) {
                changes.remove(id);
            }

            for (const [key, stateValue] of Object.entries(
                change.state ?? {},
            )) {
                try {
                    changes.setState(id, key, stateValue);
                } catch {
                    problems.push(
                        `a broken value ${JSON.stringify(key)} of "${id}" dropped`,
                    );
                }
            }
        }

        return { changes, problems };
    }

    private entry(id: EntityId): EntityChange {
        let change = this.changes.get(id);

        if (!change) {
            if (this.changes.size >= MAX_CHANGES) {
                throw new WorldChangesError('too many world changes');
            }

            change = {};
            this.changes.set(id, change);
        }

        return change;
    }
}
