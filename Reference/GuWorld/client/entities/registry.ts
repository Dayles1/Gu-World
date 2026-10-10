/**
 * Every thing of the game world, by its stable id. An id is given by the
 * data that defines the thing ("player", "block:test_grounds:crate",
 * later "npc:gu_yue:fang_yuan") — never by where it sits in an array, the
 * order things were made or loaded in, or the area it happens to be in —
 * so the same thing always has the same id and can never be there twice:
 * `add` refuses a second one, `ensure` hands back the one already there.
 *
 * Entities are plain game data — kind, location, position, facing, their
 * own state — with nothing of three.js in them; pictures of them (render/)
 * are made and dropped separately. Observers hear every addition, removal
 * and move: that is where a spatial index (stage 3, chunks) plugs in.
 */

import type { LocationId } from '../world/config';
import { isFiniteNumber, isVec3 } from '../world/space';
import type { Vec3 } from '../world/space';

export type EntityId = string;

/** "kind" or "kind:part:part…": lower-case letters, digits, "_" and "-". */
export const ENTITY_ID = /^[a-z][a-z0-9_]*(?::[a-z0-9_-]+)*$/;
const MAX_ID_LENGTH = 128;

export interface Entity {
    readonly id: EntityId;
    readonly kind: string;
    location: LocationId;
    position: Vec3;
    /** Radians around Y (0 looks along +Z). */
    yaw: number;
}

export interface RegistryObserver<E extends Entity> {
    added?(entity: E): void;
    removed?(entity: E): void;
    moved?(entity: E, from: Vec3): void;
}

export class EntityError extends Error {}

export function isEntityId(value: unknown): value is EntityId {
    return (
        typeof value === 'string' &&
        value.length <= MAX_ID_LENGTH &&
        ENTITY_ID.test(value)
    );
}

export class EntityRegistry<E extends Entity = Entity> {
    private entities = new Map<EntityId, E>();
    private observers = new Set<RegistryObserver<E>>();

    get size(): number {
        return this.entities.size;
    }

    /** Adds a new entity; an id already there is an error. */
    add(entity: E): E {
        if (this.entities.has(entity.id)) {
            throw new EntityError(`The entity "${entity.id}" is already there`);
        }

        this.check(entity);
        this.entities.set(entity.id, entity);

        for (const observer of this.observers) {
            observer.added?.(entity);
        }

        return entity;
    }

    /**
     * The entity with this id: the one already there, or `make()` added.
     * Loading the same things again never makes a second copy.
     */
    ensure(id: EntityId, make: () => E): E {
        const existing = this.entities.get(id);

        if (existing) {
            return existing;
        }

        const made = make();

        if (made.id !== id) {
            throw new EntityError(`Asked for "${id}", made "${made.id}"`);
        }

        return this.add(made);
    }

    remove(id: EntityId): E | null {
        const entity = this.entities.get(id);

        if (!entity) {
            return null;
        }

        this.entities.delete(id);

        for (const observer of this.observers) {
            observer.removed?.(entity);
        }

        return entity;
    }

    get(id: EntityId): E | undefined {
        return this.entities.get(id);
    }

    has(id: EntityId): boolean {
        return this.entities.has(id);
    }

    all(): IterableIterator<E> {
        return this.entities.values();
    }

    ofKind(kind: E['kind']): E[] {
        return [...this.entities.values()].filter(
            (entity) => entity.kind === kind,
        );
    }

    /**
     * Moves an entity. A position or facing that is not finite is refused
     * (the entity stays where it was) rather than written into the world.
     */
    move(id: EntityId, position: Vec3, yaw?: number): void {
        const entity = this.entities.get(id);

        if (!entity) {
            throw new EntityError(`There is no entity "${id}" to move`);
        }

        if (!isVec3(position) || (yaw !== undefined && !isFiniteNumber(yaw))) {
            throw new EntityError(`"${id}" cannot move to a broken position`);
        }

        const from = { ...entity.position };
        entity.position = { x: position.x, y: position.y, z: position.z };

        if (yaw !== undefined) {
            entity.yaw = yaw;
        }

        for (const observer of this.observers) {
            observer.moved?.(entity, from);
        }
    }

    /** Hears additions, removals and moves; returns the way to stop. */
    observe(observer: RegistryObserver<E>): () => void {
        this.observers.add(observer);

        return () => this.observers.delete(observer);
    }

    clear(): void {
        for (const id of [...this.entities.keys()]) {
            this.remove(id);
        }
    }

    private check(entity: E): void {
        if (!isEntityId(entity.id)) {
            throw new EntityError(`"${entity.id}" is not a valid entity id`);
        }

        if (!isVec3(entity.position) || !isFiniteNumber(entity.yaw)) {
            throw new EntityError(`"${entity.id}" has a broken position`);
        }
    }
}
