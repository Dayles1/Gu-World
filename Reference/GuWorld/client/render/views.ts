/**
 * The pictures of entities, kept apart from the entities themselves. Each
 * entity may have one view (made by its kind's factory) under the same
 * stable id; every frame the views are brought up to date from the data.
 *
 * A view is only a picture: dropping it (out of sight, out of a drawn
 * chunk) leaves the entity in the world, and showing it again makes a
 * fresh one. Removing an entity drops its view. Disposing the views frees
 * what they made and stops listening to the registry.
 *
 * By default every entity is shown as it is added; with `autoShow` off
 * something else decides (the chunks: world/chunk-manager.ts).
 */

import type * as THREE from 'three';
import type { Entity, EntityId, EntityRegistry } from '../entities/registry';

export interface EntityView<E extends Entity = Entity> {
    readonly object: THREE.Object3D;
    /** Brings the picture up to date; `alpha` is how far between the last two simulation steps. */
    sync(entity: E, alpha: number, frame: number): void;
    dispose(): void;
}

export type ViewFactory<E extends Entity> = (entity: E) => EntityView<E> | null;

export class EntityViews<E extends Entity> {
    private views = new Map<EntityId, EntityView<E>>();
    private stopObserving: () => void;

    constructor(
        private registry: EntityRegistry<E>,
        private scene: THREE.Object3D,
        private factories: Partial<Record<E['kind'], ViewFactory<E>>>,
        autoShow = true,
    ) {
        if (autoShow) {
            for (const entity of registry.all()) {
                this.show(entity.id);
            }
        }

        this.stopObserving = registry.observe({
            added: (entity) => {
                if (autoShow) {
                    this.show(entity.id);
                }
            },
            removed: (entity) => this.drop(entity.id),
        });
    }

    get size(): number {
        return this.views.size;
    }

    has(id: EntityId): boolean {
        return this.views.has(id);
    }

    /** Makes the entity's view, if it has none and its kind has a factory. */
    show(id: EntityId): void {
        const entity = this.registry.get(id);

        if (!entity || this.views.has(id)) {
            return;
        }

        const factory = this.factories[entity.kind as E['kind']];
        const view = factory?.(entity) ?? null;

        if (view) {
            this.views.set(id, view);
            this.scene.add(view.object);
        }
    }

    /** Drops the entity's view; the entity itself stays in the world. */
    drop(id: EntityId): void {
        const view = this.views.get(id);

        if (view) {
            this.views.delete(id);
            view.object.removeFromParent();
            view.dispose();
        }
    }

    sync(alpha: number, frame: number): void {
        for (const [id, view] of this.views) {
            const entity = this.registry.get(id);

            if (entity) {
                view.sync(entity, alpha, frame);
            }
        }
    }

    dispose(): void {
        this.stopObserving();

        for (const id of [...this.views.keys()]) {
            this.drop(id);
        }
    }
}
