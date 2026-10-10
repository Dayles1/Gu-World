/**
 * The ground drawn chunk by chunk (a ChunkPainter for the ChunkManager).
 * A chunk's ground is made by a Job a slice at a time: its heights row by
 * row until the frame's deadline, then the mesh. The heights are sampled
 * one square beyond the chunk's edges, so the normals at an edge are
 * worked out from both sides — neighbouring chunks meet without a seam
 * in the light. Chunks of one size share one index buffer.
 *
 * The ground carries a faint metre grid (a bolder line every ten metres)
 * so distances and slopes can be judged on the proving ground.
 */

import * as THREE from 'three';
import type { ChunkPainter, Job } from '../world/chunk-manager';
import type { Area, ChunkKey } from '../world/chunks';
import type { Ground } from '../world/ground';

function gridTexture(): THREE.CanvasTexture {
    const size = 256;
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const context = canvas.getContext('2d')!;

    context.fillStyle = '#d6d6d0';
    context.fillRect(0, 0, size, size);
    context.fillStyle = '#b6b6ae';

    for (let line = 0; line < 10; line++) {
        const at = (line * size) / 10;
        context.fillRect(at, 0, 1, size);
        context.fillRect(0, at, size, 1);
    }

    context.fillStyle = '#8e8e86';
    context.fillRect(0, 0, 3, size);
    context.fillRect(0, 0, size, 3);

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.anisotropy = 8;

    return texture;
}

export class TerrainChunks implements ChunkPainter {
    private meshes = new Map<ChunkKey, THREE.Mesh>();
    private indices = new Map<number, THREE.BufferAttribute>();
    private material = new THREE.MeshStandardMaterial({
        color: 0x8c9478,
        map: gridTexture(),
        roughness: 0.95,
    });

    /** @param detail squares per metre */
    constructor(
        private scene: THREE.Object3D,
        private ground: Ground,
        private now: () => number,
        private detail = 1,
    ) {}

    get count(): number {
        return this.meshes.size;
    }

    /** Vertices of all the ground drawn. */
    get vertices(): number {
        let total = 0;

        for (const mesh of this.meshes.values()) {
            total += mesh.geometry.getAttribute('position').count;
        }

        return total;
    }

    paint(key: ChunkKey, area: Area): Job {
        const segments = Math.max(
            1,
            Math.round((area.maxX - area.minX) * this.detail),
        );
        const step = (area.maxX - area.minX) / segments;
        // One square of heights beyond each edge, for the normals.
        const side = segments + 3;
        const heights = new Float32Array(side * side);
        let row = 0;

        return {
            step: (deadline) => {
                while (row < side) {
                    const z = area.minZ + (row - 1) * step;

                    for (let i = 0; i < side; i++) {
                        heights[row * side + i] = this.ground.heightAt(
                            area.minX + (i - 1) * step,
                            z,
                        );
                    }

                    row++;

                    if (row < side && this.now() >= deadline) {
                        return false;
                    }
                }

                this.place(key, this.mesh(area, segments, step, heights));

                return true;
            },
        };
    }

    erase(key: ChunkKey): void {
        const mesh = this.meshes.get(key);

        if (mesh) {
            this.meshes.delete(key);
            mesh.removeFromParent();
            mesh.geometry.dispose();
        }
    }

    dispose(): void {
        for (const key of [...this.meshes.keys()]) {
            this.erase(key);
        }

        this.material.map?.dispose();
        this.material.dispose();
    }

    private place(key: ChunkKey, mesh: THREE.Mesh): void {
        this.erase(key);
        this.meshes.set(key, mesh);
        this.scene.add(mesh);
    }

    private mesh(
        area: Area,
        segments: number,
        step: number,
        heights: Float32Array,
    ): THREE.Mesh {
        const row = segments + 1;
        const side = segments + 3;
        const count = row * row;
        const positions = new Float32Array(count * 3);
        const normals = new Float32Array(count * 3);
        const uvs = new Float32Array(count * 2);
        const normal = new THREE.Vector3();

        for (let j = 0; j <= segments; j++) {
            for (let i = 0; i <= segments; i++) {
                const at = (j + 1) * side + (i + 1);
                const vertex = j * row + i;
                const x = area.minX + i * step;
                const z = area.minZ + j * step;

                positions.set([x, heights[at], z], vertex * 3);
                normal
                    .set(
                        heights[at - 1] - heights[at + 1],
                        2 * step,
                        heights[at - side] - heights[at + side],
                    )
                    .normalize();
                normals.set([normal.x, normal.y, normal.z], vertex * 3);
                // The texture covers ten metres.
                uvs.set([x / 10, z / 10], vertex * 2);
            }
        }

        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute(
            'position',
            new THREE.BufferAttribute(positions, 3),
        );
        geometry.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
        geometry.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
        geometry.setIndex(this.index(segments));
        geometry.computeBoundingSphere();

        const mesh = new THREE.Mesh(geometry, this.material);
        mesh.receiveShadow = true;
        mesh.matrixAutoUpdate = false;

        return mesh;
    }

    /** The triangles of a grid of `segments`², shared by every chunk of that size. */
    private index(segments: number): THREE.BufferAttribute {
        let index = this.indices.get(segments);

        if (!index) {
            const row = segments + 1;
            const values = new Uint32Array(segments * segments * 6);
            let k = 0;

            for (let j = 0; j < segments; j++) {
                for (let i = 0; i < segments; i++) {
                    const a = j * row + i;
                    const b = a + 1;
                    const c = a + row;
                    const d = c + 1;
                    values.set([a, c, b, b, c, d], k);
                    k += 6;
                }
            }

            index = new THREE.BufferAttribute(values, 1);
            this.indices.set(segments, index);
        }

        return index;
    }
}
