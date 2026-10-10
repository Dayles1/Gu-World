/**
 * GU World put together: the world's data (state/world.ts), the systems
 * that change it, the pictures of it and the loop that drives them.
 *
 * - Simulation systems run in fixed steps (engine/loop.ts): the clock,
 *   the hero, autosaving. They change only data.
 * - Frame systems run once a frame: the camera, the entities' pictures,
 *   the light, the HUD, the renderer. They only read the data.
 * - Each runs on its own (engine/systems.ts): one that keeps failing is
 *   switched off and said so; the rest go on.
 * - Systems talk through the event bus where it pays: saving, repairs,
 *   failures and the time of day reach the HUD and the log as events.
 * - The location is loaded around the hero chunk by chunk
 *   (world/chunk-manager.ts), first of all in the frame, within a time
 *   budget: its things become entities and colliders, its ground and
 *   their pictures are drawn, and all of it is let go again behind.
 *
 * The game starts by loading the chunks around the hero (the loading
 * screen), then waits on the pause card; a click plays (and locks the
 * pointer). Losing the pointer, a hidden tab or a window out of focus
 * pauses it; pausing saves, and so does leaving the page.
 */

import * as THREE from 'three';
import { defaultAppearance } from './body/looks';
import type { Look } from './body/mannequin';
import { EventBus } from './engine/events';
import { GameLoop, watchPage } from './engine/loop';
import { SystemRunner } from './engine/systems';
import type { GameSystem, SystemReport } from './engine/systems';
import type { ContentEntity, GameEntity } from './entities/types';
import type { GameEvents } from './game-events';
import { t } from './i18n';
import { Input } from './input';
import type { Action } from './input';
import type { Stance } from './physics/character';
import { ThirdPersonCamera } from './render/camera';
import { viewFactories } from './render/entity-views';
import { Stage } from './render/stage';
import { TerrainChunks } from './render/terrain-chunks';
import { EntityViews } from './render/views';
import type { RestoredGame } from './state/save';
import { Saver } from './state/saver';
import type { SaveTransport } from './state/saver';
import { buildWorld, saveWorld } from './state/world';
import type { World } from './state/world';
import { autosaveSystem } from './systems/autosave';
import { PlayerSystem } from './systems/player';
import { timeSystem } from './systems/time';
import { drawChunkMap } from './ui/chunk-map';
import { Hud } from './ui/hud';
import { ChunkManager } from './world/chunk-manager';
import type { ChunkStats } from './world/chunk-manager';
import type { ChunkSettings, WorldConfig } from './world/config';

export interface GameOptions {
    root: HTMLElement;
    config: WorldConfig;
    restored: RestoredGame;
    transport: SaveTransport;
}

/** Time the chunks may take in a frame, ms: while loading, and while playing. */
const LOADING_BUDGET = 12;
const PLAYING_BUDGET = 4;
/** How near (m) a thing must be for the debug mark (F6). */
const MARK_REACH = 3;

/** The eyes' height for the camera, by stance. */
const EYES: Record<Stance, number> = { stand: 1.55, crouch: 1.05, crawl: 0.5 };

/** The hero's look until heroes can be made: the man as the model comes. */
const HERO_LOOK: Look = {
    gender: 'male',
    style: 'realistic',
    appearance: defaultAppearance('male'),
};

/** What the game is doing, as plain data (for the debug panel and checks). */
export interface GameSnapshot {
    location: string;
    day: number;
    time: string;
    minutes: number;
    phase: string;
    player: { x: number; y: number; z: number; yaw: number; grounded: boolean };
    paused: boolean;
    pauseReasons: string[];
    ticks: number;
    entities: number;
    views: number;
    systems: { name: string; enabled: boolean; failures: number }[];
    loading: boolean;
    chunks: ChunkStats & { settings: ChunkSettings; terrainVertices: number };
    renderer: { calls: number; triangles: number; geometries: number };
}

export class Game {
    readonly events = new EventBus<GameEvents>();
    private world: World;
    private stage: Stage;
    private camera: ThirdPersonCamera;
    private views: EntityViews<GameEntity>;
    private input: Input;
    private hud: Hud;
    private loop: GameLoop;
    private simulation: SystemRunner;
    private frame: SystemRunner;
    private player: PlayerSystem;
    private saver: Saver;
    private terrain: TerrainChunks;
    private chunks: ChunkManager;
    /** The chunks around the hero are ready: play can begin. */
    private loaded = false;
    private stopWatching: () => void = () => {};
    private started = false;
    /** The camera jumps straight into place on the first frame, then follows softly. */
    private cameraPlaced = false;
    private alpha = 1;
    private focus = new THREE.Vector3();
    private fps = { frames: 0, time: 0, value: 0 };

    constructor(options: GameOptions) {
        const { root, config, restored, transport } = options;
        const world = buildWorld(config, restored, HERO_LOOK);
        const report: SystemReport = {
            failed: (system, error, failures) =>
                this.events.emit('system:failed', { system, error, failures }),
            disabled: (system, error) =>
                this.events.emit('system:disabled', { system, error }),
        };

        this.world = world;
        this.stage = new Stage(root);
        this.stage.setReach(config.chunks.visualRadius);
        this.terrain = new TerrainChunks(
            this.stage.scene,
            world.location.ground,
            () => performance.now(),
        );
        this.camera = new ThirdPersonCamera(
            this.stage.aspect,
            world.location.ground,
        );
        // Behind the hero, looking the way they face.
        this.camera.yaw = world.player.yaw + Math.PI;
        this.views = new EntityViews(
            world.registry,
            this.stage.scene,
            viewFactories(world.location.ground, () => ({
                yaw: this.camera.facing,
                pitch: this.camera.pitch,
            })),
            false,
        );
        this.chunks = new ChunkManager({
            location: world.location.id,
            bounds: world.location.bounds,
            content: world.location,
            settings: config.chunks,
            registry: world.registry,
            colliders: world.colliders,
            changes: world.changes,
            views: this.views,
            painter: this.terrain,
            now: () => performance.now(),
            alwaysShown: [world.player.id],
            problem: (message) =>
                console.error(`GU World: chunk content: ${message}`),
        });
        this.input = new Input(this.stage.renderer.domElement);
        this.hud = new Hud(root, world.location.id, () => void this.play());
        this.saver = new Saver(transport, config, {
            written: () => this.events.emit('save:written', {}),
            failed: (message) => this.events.emit('save:failed', { message }),
            refused: (problems) =>
                this.events.emit('save:refused', { problems }),
        });

        this.player = new PlayerSystem(world, {
            forward: () => this.input.forward,
            strafe: () => this.input.strafe,
            sprint: () => this.input.sprint,
            rise: () => this.input.rise,
            dive: () => this.input.dive,
            axes: () => this.camera.groundAxes(),
        });
        this.simulation = new SystemRunner(report);
        this.simulation.add(timeSystem(world.clock, this.events));
        this.simulation.add(this.player);
        this.simulation.add(autosaveSystem(() => this.save()));

        this.frame = new SystemRunner(report);

        for (const system of this.frameSystems()) {
            this.frame.add(system);
        }

        this.loop = new GameLoop({
            tick: (step) => this.simulation.run(step),
            render: (alpha, frame) => {
                this.alpha = alpha;
                this.frame.run(frame);
            },
            overrun: (dropped) => this.events.emit('loop:overrun', { dropped }),
        });

        this.listen();

        if (restored.problems.length) {
            this.events.emit('state:repaired', { problems: restored.problems });
        }
    }

    start(): void {
        this.loop.pause('loading');
        this.loop.pause('player');
        this.hud.showPause(false);
        this.hud.setLoading({ done: 0, total: 1 });
        this.loop.start();
        this.stopWatching = watchPage(this.loop, { document, window });
        window.addEventListener('pagehide', this.leave);
    }

    /** Plays: from the pause card, a click. */
    async play(): Promise<void> {
        if (!this.loaded) {
            return;
        }

        this.started = true;
        this.input.active = true;
        this.hud.showPause(false);
        this.loop.resume('player');
        // The mouse turns the camera only with the pointer locked; a
        // browser that refuses still lets one walk.
        await this.input.lock();
    }

    pause(): void {
        if (this.loop.pauseReasons.includes('player')) {
            return;
        }

        this.input.active = false;
        this.loop.pause('player');
        this.hud.showPause(true, this.started);
        this.save();
    }

    save(keepalive = false): void {
        this.hud.setStatus(t.saving);
        this.chunks.capture();
        void this.saver.save(saveWorld(this.world), keepalive);
    }

    snapshot(): GameSnapshot {
        const { clock, player, body, registry, location } = this.world;

        return {
            location: location.id,
            day: clock.day,
            time: clock.label,
            minutes: clock.minutes,
            phase: clock.phase,
            player: {
                ...player.position,
                yaw: player.yaw,
                grounded: body.grounded,
            },
            paused: this.loop.paused,
            pauseReasons: this.loop.pauseReasons,
            ticks: this.loop.ticks,
            entities: registry.size,
            views: this.views.size,
            systems: [...this.simulation.status(), ...this.frame.status()].map(
                ({ name, enabled, failures }) => ({ name, enabled, failures }),
            ),
            loading: !this.loaded,
            chunks: {
                ...this.chunks.summary(),
                settings: this.chunks.settings,
                terrainVertices: this.terrain.vertices,
            },
            renderer: {
                calls: this.stage.renderer.info.render.calls,
                triangles: this.stage.renderer.info.render.triangles,
                geometries: this.stage.renderer.info.memory.geometries,
            },
        };
    }

    /** Forgets the slowest chunk update so far (to measure a stretch on its own). */
    resetChunkPeak(): void {
        this.chunks.resetPeak();
    }

    dispose(): void {
        this.loop.stop();
        this.stopWatching();
        window.removeEventListener('pagehide', this.leave);
        this.simulation.dispose();
        this.frame.dispose();
        this.chunks.dispose();
        this.terrain.dispose();
        this.views.dispose();
        this.input.dispose();
        this.hud.dispose();
        this.stage.dispose();
        this.events.clear();
    }

    private frameSystems(): GameSystem[] {
        const { world } = this;

        return [
            {
                name: 'chunks',
                update: () => {
                    const { x, z } = world.player.position;

                    this.chunks.update(
                        x,
                        z,
                        this.loaded ? PLAYING_BUDGET : LOADING_BUDGET,
                    );

                    if (this.loaded) {
                        return;
                    }

                    if (this.chunks.ready(x, z)) {
                        this.loaded = true;
                        this.hud.setLoading(null);
                        this.hud.showPause(true, false);
                        this.loop.resume('loading');
                    } else {
                        this.hud.setLoading(this.chunks.progress(x, z));
                    }
                },
            },
            {
                name: 'camera',
                update: (frame) => {
                    const look = this.input.takeLook();
                    const { player } = world;
                    const { previous, position, motion } = player;
                    const alpha = this.alpha;

                    this.focus.set(
                        previous.x + (position.x - previous.x) * alpha,
                        previous.y + (position.y - previous.y) * alpha,
                        previous.z + (position.z - previous.z) * alpha,
                    );

                    if (
                        Math.abs(
                            this.camera.camera.aspect - this.stage.aspect,
                        ) > 1e-3
                    ) {
                        this.camera.resize(this.stage.aspect);
                    }

                    this.camera.look(look.x, look.y, look.wheel);
                    this.camera.update(
                        frame,
                        this.focus,
                        motion.swimming ? 1.4 : EYES[motion.stance],
                        motion.speed > motion.walkSpeed + 0.5,
                        !this.cameraPlaced,
                    );
                    this.cameraPlaced = true;
                },
            },
            {
                name: 'views',
                update: (frame) => this.views.sync(this.alpha, frame),
            },
            {
                name: 'light',
                update: () =>
                    this.stage.light(world.clock.timeOfDay, this.focus),
            },
            {
                name: 'hud',
                update: (frame) => {
                    this.hud.setClock(world.clock);
                    this.countFrame(frame);

                    if (this.hud.debugShown) {
                        this.hud.setDebug(this.debugLines());
                    }
                },
            },
            {
                name: 'render',
                update: () => this.stage.render(this.camera.camera),
            },
        ];
    }

    private listen(): void {
        const { events, hud } = this;

        events.on('save:written', () => hud.setStatus(t.saved, 'good'));
        events.on('save:failed', ({ message }) => {
            hud.setStatus(t.save_failed, 'bad');
            console.warn('GU World: saving failed', message);
        });
        events.on('save:refused', ({ problems }) => {
            hud.setStatus(t.save_refused, 'bad');
            console.error('GU World: a broken save was not sent', problems);
        });
        events.on('state:repaired', ({ problems }) => {
            hud.notice(`${t.repaired}: ${problems.join('; ')}`);
            console.warn('GU World: the save was repaired', problems);
        });
        events.on('system:failed', ({ system, error, failures }) =>
            console.error(
                `GU World: system "${system}" failed (${failures} in a row)`,
                error,
            ),
        );
        events.on('system:disabled', ({ system, error }) => {
            hud.notice(`${t.system_disabled}: ${system}`);
            console.error(
                `GU World: system "${system}" was switched off`,
                error,
            );
        });
        events.on('loop:overrun', ({ dropped }) =>
            console.warn(
                `GU World: the simulation fell ${dropped.toFixed(3)} s behind`,
            ),
        );

        this.input.onAction = (action) => this.act(action);
        this.input.onLockChange = (locked) => {
            if (!locked) {
                this.pause();
            }
        };
    }

    private act(action: Action): void {
        if (action === 'debug') {
            this.hud.toggleDebug();

            return;
        }

        if (action === 'escape') {
            this.pause();

            return;
        }

        if (!this.input.active) {
            return;
        }

        if (action === 'jump') {
            this.player.jump();
        } else if (action === 'mark') {
            this.markNearest();
        } else {
            this.player.toggleStance(action);
        }
    }

    /**
     * The debug mark (F6): marks or unmarks the nearest thing within reach.
     * A change to the world like any other, kept through letting its chunk
     * go and through saving — what it is there to show.
     */
    private markNearest(): void {
        const { registry, player, changes } = this.world;
        let nearest: ContentEntity | null = null;
        let best = MARK_REACH;

        for (const entity of registry.all()) {
            if (entity.kind === 'player') {
                continue;
            }

            const distance = Math.hypot(
                entity.position.x - player.position.x,
                entity.position.z - player.position.z,
            );

            if (distance < best) {
                best = distance;
                nearest = entity;
            }
        }

        if (nearest) {
            const marked = nearest.state.marked !== true;
            nearest.state.marked = marked;
            changes.setState(nearest.id, 'marked', marked);
            this.hud.notice(`F6: ${nearest.id} ${marked ? '✓' : '✕'}`);
        }
    }

    private countFrame(frame: number): void {
        this.fps.frames++;
        this.fps.time += frame;

        if (this.fps.time >= 0.5) {
            this.fps.value = this.fps.frames / this.fps.time;
            this.fps.frames = 0;
            this.fps.time = 0;
        }
    }

    private debugLines(): string[] {
        const snapshot = this.snapshot();
        const { x, y, z, yaw, grounded } = snapshot.player;

        const chunks = snapshot.chunks;

        drawChunkMap(this.hud.chunkMap, this.chunks.chunks(), chunks.settings, {
            x,
            z,
        });

        return [
            `${Math.round(this.fps.value)} fps · ${snapshot.ticks} steps`,
            `chunks ${chunks.settings.size} m: ${chunks.counts.visible} visible · ${chunks.counts.active} active · ${chunks.counts.loading} loading · queue ${chunks.queue}`,
            `chunk work ${chunks.lastMs.toFixed(1)} ms (max ${chunks.maxMs.toFixed(1)}) · ${chunks.loads} loads, ${chunks.averageLoadMs.toFixed(0)} ms avg · ${chunks.unloads} unloads`,
            `things ${chunks.things} · ground ${chunks.terrainVertices} vertices · ${snapshot.renderer.calls} draw calls`,
            `${snapshot.location} · day ${snapshot.day} ${snapshot.time} (${snapshot.phase})`,
            `x ${x.toFixed(2)}  y ${y.toFixed(2)}  z ${z.toFixed(2)}  yaw ${yaw.toFixed(2)}${grounded ? '  ground' : ''}`,
            `entities ${snapshot.entities} · views ${snapshot.views}`,
            `paused: ${snapshot.pauseReasons.join(', ') || 'no'}`,
            ...snapshot.systems.map(
                (system) =>
                    `${system.enabled ? '·' : '✕'} ${system.name}${system.failures ? ` (${system.failures} failures)` : ''}`,
            ),
        ];
    }

    private leave = (): void => {
        this.save(true);
    };
}
