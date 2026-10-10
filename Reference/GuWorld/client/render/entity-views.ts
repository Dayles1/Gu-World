/** The pictures of each kind of entity (see views.ts). */

import * as THREE from 'three';
import { Mannequin } from '../body/mannequin';
import type {
    BlockEntity,
    ContentEntity,
    GameEntity,
    PlayerEntity,
    StoneEntity,
} from '../entities/types';
import { STONE_SINK } from '../physics/solids';
import type { Ground } from '../world/ground';
import type { EntityView, ViewFactory } from './views';

const BLOCK = new THREE.MeshStandardMaterial({
    color: 0xb9b4aa,
    roughness: 0.85,
});
const STONE = new THREE.MeshStandardMaterial({
    color: 0x8f8c84,
    roughness: 0.95,
    flatShading: true,
});
/** A thing marked with the debug tool (F6), so its change can be seen to last. */
const MARKED = new THREE.MeshStandardMaterial({
    color: 0xa8261f,
    roughness: 0.7,
    flatShading: true,
});

const material = (
    entity: ContentEntity,
    plain: THREE.MeshStandardMaterial,
): THREE.MeshStandardMaterial =>
    entity.state.marked === true ? MARKED : plain;

/** A block: a box standing on the ground where its entity is. */
function blockView(entity: BlockEntity): EntityView<GameEntity> {
    const geometry = new THREE.BoxGeometry(
        entity.width,
        entity.height,
        entity.depth,
    );
    const mesh = new THREE.Mesh(geometry, BLOCK);

    mesh.castShadow = true;
    mesh.receiveShadow = true;

    return {
        object: mesh,
        sync: (data) => {
            const block = data as BlockEntity;
            mesh.position.set(
                block.position.x,
                block.position.y + block.height / 2,
                block.position.z,
            );
            mesh.material = material(block, BLOCK);
        },
        dispose: () => geometry.dispose(),
    };
}

/** A stone: a rough rounded rock, partly sunk into the ground. */
function stoneView(entity: StoneEntity): EntityView<GameEntity> {
    const geometry = new THREE.IcosahedronGeometry(entity.radius, 1);
    const mesh = new THREE.Mesh(geometry, STONE);
    // Each stone turned and squashed its own way, from its id.
    let seed = 0;

    for (const char of entity.id) {
        seed = (seed * 31 + char.charCodeAt(0)) | 0;
    }

    mesh.rotation.set(seed % 7, seed % 5, seed % 3);
    mesh.scale.set(1, 0.7 + ((seed >>> 3) % 30) / 100, 1);
    mesh.castShadow = true;
    mesh.receiveShadow = true;

    return {
        object: mesh,
        sync: (data) => {
            const stone = data as StoneEntity;
            mesh.position.set(
                stone.position.x,
                stone.position.y + stone.radius * (1 - STONE_SINK * 2),
                stone.position.z,
            );
            mesh.material = material(stone, STONE);
        },
        dispose: () => geometry.dispose(),
    };
}

const lerpAngle = (from: number, to: number, t: number) =>
    from + Math.atan2(Math.sin(to - from), Math.cos(to - from)) * t;

/**
 * A person: the animated figure, drawn between the last two simulation
 * steps so it moves smoothly at any frame rate. Its feet are planted on
 * the location's ground.
 */
function personView(
    entity: PlayerEntity,
    ground: Ground,
    look: () => { yaw: number; pitch: number },
): EntityView<GameEntity> {
    const figure = new Mannequin();
    const heightAt = (x: number, z: number) => ground.heightAt(x, z);

    figure.setLook(entity.look);

    return {
        object: figure.root,
        sync(data, alpha, frame) {
            const person = data as PlayerEntity;
            const { previous, position, motion } = person;
            const facing = lerpAngle(person.previousYaw, person.yaw, alpha);

            figure.root.position.set(
                previous.x + (position.x - previous.x) * alpha,
                previous.y + (position.y - previous.y) * alpha,
                previous.z + (position.z - previous.z) * alpha,
            );
            figure.root.rotation.y = facing;

            const view = look();
            figure.update(
                frame,
                {
                    ...motion,
                    lookYaw: Math.atan2(
                        Math.sin(view.yaw - facing),
                        Math.cos(view.yaw - facing),
                    ),
                    lookPitch: view.pitch,
                    ground: motion.grounded ? heightAt : null,
                },
                motion.landingSpeed,
            );
        },
        dispose: () => figure.dispose(),
    };
}

export function viewFactories(
    ground: Ground,
    look: () => { yaw: number; pitch: number },
): Record<GameEntity['kind'], ViewFactory<GameEntity>> {
    return {
        block: (entity) => blockView(entity as BlockEntity),
        stone: (entity) => stoneView(entity as StoneEntity),
        player: (entity) => personView(entity as PlayerEntity, ground, look),
    };
}
