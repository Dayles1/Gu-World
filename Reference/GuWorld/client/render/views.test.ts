import * as THREE from 'three';
import { describe, expect, it, vi } from 'vitest';
import { EntityRegistry } from '../entities/registry';
import type { Entity } from '../entities/registry';
import { EntityViews } from './views';
import type { EntityView } from './views';

function entity(id: string): Entity {
    return {
        id,
        kind: id.split(':')[0],
        location: 'test_grounds',
        position: { x: 1, y: 2, z: 3 },
        yaw: 0,
    };
}

function setup() {
    const registry = new EntityRegistry();
    const scene = new THREE.Scene();
    const disposed: string[] = [];
    const made: string[] = [];
    const factory = (subject: Entity): EntityView => {
        made.push(subject.id);
        const object = new THREE.Object3D();

        return {
            object,
            sync: (data) =>
                object.position.set(
                    data.position.x,
                    data.position.y,
                    data.position.z,
                ),
            dispose: () => disposed.push(subject.id),
        };
    };
    const views = new EntityViews(registry, scene, { block: factory });

    return { registry, scene, views, disposed, made };
}

describe('EntityViews', () => {
    it('makes a view for each entity with a factory, kept apart from it', () => {
        const { registry, scene, views } = setup();

        registry.add(entity('block:a'));
        registry.add(entity('player'));
        views.sync(1, 0.016);

        expect(views.has('block:a')).toBe(true);
        // No factory for players here: no view, the entity is still there.
        expect(views.has('player')).toBe(false);
        expect(scene.children).toHaveLength(1);
        expect(scene.children[0].position.toArray()).toEqual([1, 2, 3]);
    });

    it('dropping a view leaves the entity in the world', () => {
        const { registry, views, disposed } = setup();

        registry.add(entity('block:a'));
        views.drop('block:a');

        expect(registry.has('block:a')).toBe(true);
        expect(disposed).toEqual(['block:a']);
    });

    it('shows a dropped entity again with one fresh view', () => {
        const { registry, scene, views, made } = setup();

        registry.add(entity('block:a'));
        views.drop('block:a');
        views.show('block:a');
        views.show('block:a');

        expect(made).toEqual(['block:a', 'block:a']);
        expect(scene.children).toHaveLength(1);
    });

    it('drops the view when the entity leaves the world', () => {
        const { registry, scene, views, disposed } = setup();

        registry.add(entity('block:a'));
        registry.remove('block:a');

        expect(views.size).toBe(0);
        expect(scene.children).toHaveLength(0);
        expect(disposed).toEqual(['block:a']);
    });

    it('frees every view and stops listening when disposed', () => {
        const { registry, scene, views, disposed } = setup();
        const sync = vi.fn();

        registry.add(entity('block:a'));
        registry.add(entity('block:b'));
        views.dispose();
        registry.add(entity('block:c'));
        views.sync(1, 0);

        expect(disposed.sort()).toEqual(['block:a', 'block:b']);
        expect(scene.children).toHaveLength(0);
        expect(views.size).toBe(0);
        expect(sync).not.toHaveBeenCalled();
    });
});
