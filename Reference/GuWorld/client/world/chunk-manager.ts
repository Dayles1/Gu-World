/**
 * Loads the world around the hero chunk by chunk (world/chunks.ts) and
 * lets it go again behind them.
 *
 * A chunk is in one of four states:
 *
 * - `unloaded`: nothing of it is in memory (its changes are kept in the
 *   WorldChanges, by entity id);
 * - `loading`: being prepared — its things made into entities and
 *   colliders, then its ground drawn — a slice at a time;
 * - `active`: its things are in the world (entities, colliders) but not
 *   drawn;
 * - `visible`: in the world and drawn.
 *
 * Which state a chunk should be in follows from its distance to the hero:
 * within `visualRadius` it is drawn, within `simulationRadius` its things
 * are simulated (`isSimulated`), within the larger of the two its data is
 * loaded. A chunk keeps a state until it is `margin` beyond that radius,
 * so walking along an edge does not load and unload it every frame.
 *
 * The work is done nearest first, within a time budget per frame; the
 * ground of a chunk is drawn by a Job that can stop and go on in the next
 * frame, so even a large chunk never stalls a frame for long. Letting a
 * chunk go writes what changed about its things into the WorldChanges,
 * drops their pictures, colliders and entities. Loading it again makes
 * the same entities with the same ids — never a second copy — and puts
 * their changes back.
 *
 * Entities that do not come from a chunk's content (the hero, later the
 * villagers) are never unloaded with a chunk: they live in the world all
 * the time, and only their pictures come and go with the chunk they are
 * in. No three.js here: the ground is drawn by a ChunkPainter and the
 * entities by the views.
 */

import type { ContentObject, LocationContent } from '../content/types';
import type { EntityId, EntityRegistry } from '../entities/registry';
import type { ContentEntity, GameEntity } from '../entities/types';
import type { Collider, ColliderGrid } from '../physics/colliders';
import { colliderFor } from '../physics/solids';
import type { WorldChanges } from './changes';
import {
    chunkArea,
    chunkAt,
    chunkKey,
    chunksWithin,
    distanceToChunk,
} from './chunks';
import type { Area, ChunkCoord, ChunkKey } from './chunks';
import type { ChunkSettings, LocationId } from './config';
import type { Bounds } from './space';

export type ChunkState = 'unloaded' | 'loading' | 'active' | 'visible';

/** Work done a slice at a time: `step` works until `deadline` (ms) and says whether it is done. */
export interface Job {
    step(deadline: number): boolean;
}

/** Draws and erases the ground of chunks (render/terrain-chunks.ts). */
export interface ChunkPainter {
    paint(key: ChunkKey, area: Area): Job;
    erase(key: ChunkKey): void;
}

/** The pictures of entities (render/views.ts). */
export interface ChunkViews {
    show(id: EntityId): void;
    drop(id: EntityId): void;
}

export interface ChunkManagerOptions {
    location: LocationId;
    bounds: Bounds;
    content: LocationContent;
    settings: ChunkSettings;
    registry: EntityRegistry<GameEntity>;
    colliders: ColliderGrid;
    changes: WorldChanges;
    views: ChunkViews;
    painter: ChunkPainter;
    /** Milliseconds, steadily increasing (performance.now). */
    now: () => number;
    /** Entities whose pictures are always shown (the hero). */
    alwaysShown?: EntityId[];
    /** A thing of the content that cannot be loaded (two chunks claiming one id…). */
    problem?: (message: string) => void;
}

interface ChunkRecord {
    key: ChunkKey;
    coord: ChunkCoord;
    area: Area;
    distance: number;
    state: ChunkState;
    loaded: boolean;
    drawn: boolean;
    wantDrawn: boolean;
    simulated: boolean;
    painting: Job | null;
    /** The chunk's own things and their colliders. */
    owned: Map<EntityId, Collider>;
    startedAt: number;
}

export interface ChunkInfo {
    key: ChunkKey;
    coord: ChunkCoord;
    state: ChunkState;
    simulated: boolean;
    things: number;
}

export interface ChunkStats {
    /** By state. */
    counts: Record<Exclude<ChunkState, 'unloaded'>, number>;
    /** Chunks still to finish. */
    queue: number;
    things: number;
    /** Time the last update took, and the most any update took, ms. */
    lastMs: number;
    maxMs: number;
    /** Chunks made ready, how long they took on average (ms, wall time from asked to ready), chunks let go. */
    loads: number;
    averageLoadMs: number;
    unloads: number;
}

export class ChunkManager {
    private records = new Map<ChunkKey, ChunkRecord>();
    /** Which chunk owns each content entity. */
    private owner = new Map<EntityId, ChunkKey>();
    /** Entities that are not any chunk's: they live in the world all the time. */
    private roaming = new Set<EntityId>();
    private claiming: EntityId | null = null;
    private stopObserving: () => void;
    private stats = { lastMs: 0, maxMs: 0, loads: 0, loadMs: 0, unloads: 0 };

    constructor(private options: ChunkManagerOptions) {
        for (const entity of options.registry.all()) {
            this.roaming.add(entity.id);
        }

        this.stopObserving = options.registry.observe({
            added: (entity) => {
                if (entity.id !== this.claiming) {
                    this.roaming.add(entity.id);
                }
            },
            removed: (entity) => this.roaming.delete(entity.id),
        });
    }

    get settings(): ChunkSettings {
        return this.options.settings;
    }

    /** Brings the chunks up to date around (x, z), working for at most `budget` ms (at least one step). */
    update(x: number, z: number, budget: number): void {
        const { now, settings, bounds } = this.options;
        const started = now();
        const deadline = started + budget;
        const loadRadius = Math.max(
            settings.simulationRadius,
            settings.visualRadius,
        );

        for (const record of [...this.records.values()]) {
            record.distance = distanceToChunk(
                x,
                z,
                record.coord,
                settings.size,
            );

            if (record.distance > loadRadius + settings.margin) {
                this.unload(record);
                continue;
            }

            const keep = (on: boolean, radius: number) =>
                record.distance <= radius + (on ? settings.margin : 0);

            record.simulated = keep(
                record.simulated,
                settings.simulationRadius,
            );
            const wantDrawn = keep(record.wantDrawn, settings.visualRadius);

            if (!wantDrawn && record.wantDrawn) {
                this.hide(record);
            }

            record.wantDrawn = wantDrawn;
        }

        for (const { chunk, distance } of chunksWithin(
            x,
            z,
            loadRadius,
            settings.size,
            bounds,
        )) {
            const key = chunkKey(chunk.cx, chunk.cz);

            if (!this.records.has(key)) {
                this.records.set(key, {
                    key,
                    coord: chunk,
                    area: chunkArea(chunk, settings.size),
                    distance,
                    state: 'loading',
                    loaded: false,
                    drawn: false,
                    wantDrawn: distance <= settings.visualRadius,
                    simulated: distance <= settings.simulationRadius,
                    painting: null,
                    owned: new Map(),
                    startedAt: now(),
                });
            }
        }

        const queue = [...this.records.values()]
            .filter((record) => this.needsWork(record))
            .sort((a, b) => a.distance - b.distance);
        let first = true;

        for (const record of queue) {
            if (!first && now() >= deadline) {
                break;
            }

            first = false;
            this.work(record, deadline);
        }

        for (const record of this.records.values()) {
            this.settle(record);
        }

        this.syncRoaming();

        const took = now() - started;
        this.stats.lastMs = took;
        this.stats.maxMs = Math.max(this.stats.maxMs, took);
    }

    /** Whether things at (x, z) are simulated (their chunk is near enough and loaded). */
    isSimulated(x: number, z: number): boolean {
        const { cx, cz } = chunkAt(x, z, this.options.settings.size);
        const record = this.records.get(chunkKey(cx, cz));

        return Boolean(record?.loaded && record.simulated);
    }

    stateAt(x: number, z: number): ChunkState {
        const { cx, cz } = chunkAt(x, z, this.options.settings.size);

        return this.records.get(chunkKey(cx, cz))?.state ?? 'unloaded';
    }

    /** Ready to play at (x, z): every chunk to be simulated is loaded and the one there is drawn. */
    ready(x: number, z: number): boolean {
        const { settings, bounds } = this.options;

        return (
            chunksWithin(
                x,
                z,
                settings.simulationRadius,
                settings.size,
                bounds,
            ).every(
                ({ chunk }) =>
                    this.records.get(chunkKey(chunk.cx, chunk.cz))?.loaded,
            ) && this.stateAt(x, z) === 'visible'
        );
    }

    /** How far along loading is, for the loading screen: chunks within the simulation radius loaded. */
    progress(x: number, z: number): { done: number; total: number } {
        const { settings, bounds } = this.options;
        const near = chunksWithin(
            x,
            z,
            settings.simulationRadius,
            settings.size,
            bounds,
        );

        return {
            done: near.filter(
                ({ chunk }) =>
                    this.records.get(chunkKey(chunk.cx, chunk.cz))?.loaded,
            ).length,
            total: near.length,
        };
    }

    chunks(): ChunkInfo[] {
        return [...this.records.values()].map((record) => ({
            key: record.key,
            coord: record.coord,
            state: record.state,
            simulated: record.simulated,
            things: record.owned.size,
        }));
    }

    summary(): ChunkStats {
        const counts = { loading: 0, active: 0, visible: 0 };
        let things = 0;

        for (const record of this.records.values()) {
            if (record.state !== 'unloaded') {
                counts[record.state]++;
            }

            things += record.owned.size;
        }

        return {
            counts,
            queue: [...this.records.values()].filter((record) =>
                this.needsWork(record),
            ).length,
            things,
            lastMs: this.stats.lastMs,
            maxMs: this.stats.maxMs,
            loads: this.stats.loads,
            averageLoadMs: this.stats.loads
                ? this.stats.loadMs / this.stats.loads
                : 0,
            unloads: this.stats.unloads,
        };
    }

    /** Forgets the highest update time (to measure a stretch of play on its own). */
    resetPeak(): void {
        this.stats.maxMs = 0;
    }

    /** Writes the changes of every loaded thing into the WorldChanges (before a save). */
    capture(): void {
        for (const record of this.records.values()) {
            this.captureChanges(record);
        }
    }

    /** Lets every chunk go and stops listening. */
    dispose(): void {
        for (const record of [...this.records.values()]) {
            this.unload(record);
        }

        this.stopObserving();
    }

    private needsWork(record: ChunkRecord): boolean {
        return !record.loaded || (record.wantDrawn && !record.drawn);
    }

    private work(record: ChunkRecord, deadline: number): void {
        if (!record.loaded) {
            this.load(record);
        }

        if (record.wantDrawn && !record.drawn) {
            record.painting ??= this.options.painter.paint(
                record.key,
                record.area,
            );

            if (record.painting.step(deadline)) {
                record.painting = null;
                record.drawn = true;

                for (const id of record.owned.keys()) {
                    this.options.views.show(id);
                }
            }
        }

        if (!this.needsWork(record) && record.startedAt >= 0) {
            this.stats.loads++;
            this.stats.loadMs += this.options.now() - record.startedAt;
            record.startedAt = -1;
        }
    }

    /** Makes the chunk's things into entities and colliders, with their changes. */
    private load(record: ChunkRecord): void {
        const { content, changes, registry, colliders, bounds, location } =
            this.options;
        const ground = content.ground;

        for (const object of content.objectsIn(record.area)) {
            if (
                object.x < bounds.minX ||
                object.x > bounds.maxX ||
                object.z < bounds.minZ ||
                object.z > bounds.maxZ ||
                changes.isRemoved(object.id)
            ) {
                continue;
            }

            const holder = this.owner.get(object.id);

            if (holder !== undefined && holder !== record.key) {
                this.options.problem?.(
                    `"${object.id}" is claimed by chunks ${holder} and ${record.key}`,
                );
                continue;
            }

            this.claiming = object.id;

            try {
                registry.ensure(object.id, () =>
                    makeEntity(
                        object,
                        location,
                        ground.heightAt(object.x, object.z),
                    ),
                );
            } finally {
                this.claiming = null;
            }

            const entity = registry.get(object.id) as ContentEntity;
            Object.assign(entity.state, changes.state(object.id));

            const collider = colliderFor(object, ground);
            colliders.add(collider);
            record.owned.set(object.id, collider);
            this.owner.set(object.id, record.key);
            this.roaming.delete(object.id);
        }

        record.loaded = true;
    }

    /** Takes the chunk's ground and pictures away; its things stay in the world. */
    private hide(record: ChunkRecord): void {
        if (record.drawn || record.painting) {
            this.options.painter.erase(record.key);
        }

        for (const id of record.owned.keys()) {
            this.options.views.drop(id);
        }

        record.drawn = false;
        record.painting = null;
    }

    private unload(record: ChunkRecord): void {
        const { registry, colliders } = this.options;

        this.captureChanges(record);
        this.hide(record);

        for (const [id, collider] of record.owned) {
            registry.remove(id);
            colliders.remove(collider);
            this.owner.delete(id);
        }

        record.owned.clear();
        record.state = 'unloaded';
        this.records.delete(record.key);
        this.stats.unloads++;
    }

    private captureChanges(record: ChunkRecord): void {
        const { registry, changes } = this.options;

        for (const id of record.owned.keys()) {
            const entity = registry.get(id) as ContentEntity | undefined;

            for (const [key, value] of Object.entries(entity?.state ?? {})) {
                changes.setState(id, key, value);
            }
        }
    }

    private settle(record: ChunkRecord): void {
        if (!record.loaded || (record.wantDrawn && !record.drawn)) {
            record.state = 'loading';
        } else {
            record.state = record.drawn ? 'visible' : 'active';
        }
    }

    /** The pictures of roaming entities follow whether their chunk is drawn. */
    private syncRoaming(): void {
        const { registry, views, settings, alwaysShown = [] } = this.options;

        for (const id of this.roaming) {
            const entity = registry.get(id);

            if (!entity) {
                continue;
            }

            if (alwaysShown.includes(id)) {
                views.show(id);
                continue;
            }

            const { cx, cz } = chunkAt(
                entity.position.x,
                entity.position.z,
                settings.size,
            );
            const record = this.records.get(chunkKey(cx, cz));

            if (record?.drawn) {
                views.show(id);
            } else {
                views.drop(id);
            }
        }
    }
}

/** The entity of a thing of the content, standing on the ground at `base`. */
function makeEntity(
    object: ContentObject,
    location: LocationId,
    base: number,
): ContentEntity {
    const common = {
        id: object.id,
        location,
        position: { x: object.x, y: base, z: object.z },
        yaw: 0,
        state: {},
    };

    return object.kind === 'stone'
        ? { ...common, kind: 'stone', radius: object.radius }
        : {
              ...common,
              kind: 'block',
              width: object.width,
              depth: object.depth,
              height: object.height,
          };
}
