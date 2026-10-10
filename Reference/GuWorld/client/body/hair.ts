/**
 * Hair and beards for the realistic body, made from the body itself:
 *
 * - everything is placed by the head's measurements (see head), in the
 *   model's rest pose;
 * - a cap of hair is cut from the scalp's skin (see shell), down to a
 *   hairline that runs over the ears to the nape — or, with a fringe,
 *   down to the brows in strands;
 * - long hair and a bob hang as a curtain from under the cap, kept off
 *   the body by its outline round the head's axis, the top turning with
 *   the head and the ends with the chest;
 * - a ponytail is a tapering tube from the crown;
 * - buns and the parted cut are the models' own hair, carried over to
 *   the other body when it is the other gender's;
 * - a beard is a thin shell cut from the jaw, chin and upper lip.
 */

import * as THREE from 'three';
import { measureHead, named, restPoint } from './head';
import type { HeadFrame } from './head';
import type { Appearance, Beard, BodyStyle } from './looks';
import { HAIR_COLORS } from './looks';
import { boneShare, cutOut, cutShell } from './shell';

/** How wide (m) an edge fades over before it is cut. */
const EDGE = 0.005;

const smoothstep = THREE.MathUtils.smoothstep;
const clamp = THREE.MathUtils.clamp;
const lerp = THREE.MathUtils.lerp;

/** How much a point is covered, 0…1, from its distance inside an edge. */
const inside = (distance: number, edge = EDGE) =>
    clamp(0.5 + distance / edge, 0, 1);

/** A triangle wave, 0…1, `period` long: strand tips along an edge. */
const strands = (at: number, period: number) =>
    1 - Math.abs(((at / period) % 1) * 2 - 1);

const hairMaterials = new Map<number, THREE.MeshStandardMaterial>();

/** The plain material for hair (or a beard) of a colour, shared. */
function hairMaterial(color: number): THREE.MeshStandardMaterial {
    let material = hairMaterials.get(color);

    if (!material) {
        material = new THREE.MeshStandardMaterial({ color, roughness: 0.75 });
        hairMaterials.set(color, material);
    }

    return material;
}

/** Which styles have a fringe, a curtain, a tail, or the models' own hair. */
const FRINGE: Partial<Record<Appearance['hair'], boolean>> = {
    bob: true,
    long: true,
};
const CAPPED: Appearance['hair'][] = ['short', 'bob', 'ponytail', 'long'];
export const MODEL_HAIR: Partial<Record<Appearance['hair'], string>> = {
    parted: 'Hair_SimpleParted',
    buns: 'Hair_Buns',
};

/** What a hairstyle adds to the body; `covered` hides under a helmet. */
export interface Grown {
    covered: THREE.Object3D[];
    loose: THREE.Object3D[];
}

/**
 * Grows the hairstyle (and beard) on a body in its rest pose. The models'
 * own hair is handled by the caller (it is already there, or taken from
 * the other model with `borrow`).
 */
export function growHair(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    appearance: Appearance,
    style: BodyStyle,
): Grown {
    const grown: Grown = { covered: [], loose: [] };
    const color = HAIR_COLORS[appearance.hair_color];
    const material = cutOut(hairMaterial(color), style);
    const anime = style === 'anime';

    if (CAPPED.includes(appearance.hair)) {
        const cap = cutShell(
            body,
            scalp(body, frame, FRINGE[appearance.hair] === true, anime),
            (anime ? 0.008 : 0.005) * frame.scale,
            material,
        );
        cap.name = 'Hair_cap';
        grown.covered.push(cap);
    }

    if (appearance.hair === 'bob' || appearance.hair === 'long') {
        grown.loose.push(
            curtain(body, frame, appearance.hair, anime, material),
        );
    }

    if (appearance.hair === 'ponytail') {
        grown.loose.push(ponytail(body, frame, anime, material));
    }

    if (appearance.beard !== 'none') {
        grown.loose.push(beard(body, frame, appearance.beard, color, style));
    }

    for (const object of [...grown.covered, ...grown.loose]) {
        body.parent!.add(object);
    }

    return grown;
}

/**
 * The scalp under a hairline: at the nape behind, above the ears at the
 * sides, a little up the forehead in front — or, with a fringe, down to
 * the brows in pointed strands.
 */
function scalp(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    fringe: boolean,
    anime: boolean,
): Float32Array {
    const s = frame.scale;
    const eyeY = (frame.eyes[0].y + frame.eyes[1].y) / 2;
    const position = body.geometry.getAttribute('position');
    const cover = new Float32Array(position.count);
    const p = new THREE.Vector3();
    // Hairline height from the back (0) of the head to the front (1).
    const at = [0, 0.3, 0.5, 0.72, 0.86, 1];
    const front = fringe ? frame.brow - 0.002 * s : frame.brow + 0.03 * s;
    const line = [
        frame.mouth,
        frame.mouth,
        eyeY + 0.014 * s,
        eyeY + 0.026 * s,
        front,
        front,
    ];

    for (let i = 0; i < position.count; i++) {
        if (frame.onHead[i] < 0.3) {
            continue;
        }

        restPoint(body, i, p);
        const t = clamp((p.z - frame.back) / (frame.front - frame.back), 0, 1);
        let k = 1;

        while (k < at.length - 1 && t > at[k]) {
            k++;
        }

        let hairline = lerp(
            line[k - 1],
            line[k],
            (t - at[k - 1]) / (at[k] - at[k - 1]),
        );

        if (fringe && t > 0.8) {
            // Strands hang down over the forehead.
            hairline -=
                strands(p.x + 0.5, (anime ? 0.024 : 0.016) * s) *
                (anime ? 0.016 : 0.008) *
                s;
        }

        cover[i] = Math.min(frame.onHead[i], inside(p.y - hairline));
    }

    return cover;
}

/**
 * How far out the body reaches round the head's upright axis: by angle
 * (from the back, toward +x) and height. The arms (out in the rest pose)
 * are left out.
 */
class Outline {
    private static readonly ANGLES = 36;
    private static readonly STEP = 0.01;
    private reach: Float32Array;
    private bottom: number;
    private rows: number;

    constructor(
        body: THREE.SkinnedMesh,
        private frame: HeadFrame,
    ) {
        const position = body.geometry.getAttribute('position');
        const share = boneShare(body);
        const p = new THREE.Vector3();

        this.bottom = frame.centre.y - 0.8;
        this.rows = Math.ceil(1.1 / Outline.STEP);
        this.reach = new Float32Array(Outline.ANGLES * this.rows);

        for (let i = 0; i < position.count; i++) {
            const arm = share(i, (name) =>
                /^(upperarm|lowerarm|hand|index|middle|ring|pinky|thumb)/.test(
                    name,
                ),
            );

            if (arm > 0.3) {
                continue;
            }

            restPoint(body, i, p);
            const cell = this.cell(this.angle(p), p.y);

            if (cell >= 0) {
                this.reach[cell] = Math.max(this.reach[cell], this.radius(p));
            }
        }
    }

    angle(point: THREE.Vector3): number {
        return Math.atan2(
            point.x - this.frame.centre.x,
            this.frame.centre.z - point.z,
        );
    }

    radius(point: THREE.Vector3): number {
        return Math.hypot(
            point.x - this.frame.centre.x,
            point.z - this.frame.centre.z,
        );
    }

    /** The furthest the body reaches near an angle and height. */
    at(angle: number, y: number): number {
        let most = 0;

        for (let a = -1; a <= 1; a++) {
            for (let dy = -0.02; dy <= 0.02; dy += Outline.STEP) {
                const cell = this.cell(
                    angle + (a * 2 * Math.PI) / Outline.ANGLES,
                    y + dy,
                );

                if (cell >= 0) {
                    most = Math.max(most, this.reach[cell]);
                }
            }
        }

        return most;
    }

    private cell(angle: number, y: number): number {
        const row = Math.floor((y - this.bottom) / Outline.STEP);
        const turn = (angle / (2 * Math.PI) + 1.5) % 1;

        if (row < 0 || row >= this.rows) {
            return -1;
        }

        return row * Outline.ANGLES + Math.floor(turn * Outline.ANGLES);
    }
}

/** Skin weights shared between the head and the chest: 1 all on the head. */
function headAndChest(
    body: THREE.SkinnedMesh,
    onHead: number[],
): { index: THREE.BufferAttribute; weight: THREE.BufferAttribute } {
    const bones = body.skeleton.bones;
    const head = bones.findIndex((bone) => bone.name === 'Head');
    const chest = bones.findIndex((bone) => bone.name === 'spine_03');
    const index = new Uint16Array(onHead.length * 4);
    const weight = new Float32Array(onHead.length * 4);

    onHead.forEach((share, i) => {
        index[i * 4] = Math.max(0, head);
        index[i * 4 + 1] = Math.max(0, chest);
        weight[i * 4] = chest < 0 ? 1 : share;
        weight[i * 4 + 1] = chest < 0 ? 0 : 1 - share;
    });

    return {
        index: new THREE.Uint16BufferAttribute(index, 4),
        weight: new THREE.Float32BufferAttribute(weight, 4),
    };
}

/** A skinned mesh on the body's skeleton from points in the rest pose. */
function skinned(
    body: THREE.SkinnedMesh,
    geometry: THREE.BufferGeometry,
    onHead: number[],
    material: THREE.Material,
    name: string,
): THREE.SkinnedMesh {
    const { index, weight } = headAndChest(body, onHead);
    const count = geometry.getAttribute('position').count;
    const colors = geometry.getAttribute('color');

    if (!colors) {
        geometry.setAttribute(
            'color',
            new THREE.Float32BufferAttribute(
                new Float32Array(count * 4).fill(1),
                4,
            ),
        );
    }

    // Rest pose → the body mesh's own space.
    geometry.applyMatrix4(body.bindMatrixInverse);
    geometry.setAttribute('skinIndex', index);
    geometry.setAttribute('skinWeight', weight);

    const mesh = new THREE.SkinnedMesh(geometry, material);

    mesh.name = name;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.frustumCulled = false;
    mesh.bind(body.skeleton, body.bindMatrix);

    return mesh;
}

/**
 * Hair hanging round the back and sides from under the cap: to the jaw
 * (bob) or down the back (long), its ends cut in strands.
 */
function curtain(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    hair: 'bob' | 'long',
    anime: boolean,
    material: THREE.Material,
): THREE.SkinnedMesh {
    const s = frame.scale;
    const outline = new Outline(body, frame);
    const columns = 28;
    const rows = 18;
    const widest = hair === 'bob' ? 2.06 : 1.96;
    const top = frame.centre.y + 0.03 * s;
    const backEnd =
        hair === 'bob'
            ? frame.chin - 0.02 * s
            : frame.centre.y - (anime ? 0.5 : 0.42);
    const sideEnd =
        hair === 'bob' ? frame.chin + 0.005 * s : frame.chin - 0.1 * s;
    const clearance = 0.012 * s;
    const positions: number[] = [];
    const colors: number[] = [];
    const onHead: number[] = [];
    const radii: number[][] = [];
    const point = new THREE.Vector3();

    for (let c = 0; c <= columns; c++) {
        const angle = lerp(-widest, widest, c / columns);
        const end = lerp(backEnd, sideEnd, Math.abs(angle / widest) ** 1.5);
        const column: number[] = [];
        let radius = 0;

        for (let r = 0; r <= rows; r++) {
            const y = lerp(top, end, r / rows);
            radius = Math.max(
                radius,
                outline.at(angle, y) + clearance + 0.012 * s * (r / rows),
            );
            column.push(radius);
        }

        radii.push(column);
    }

    for (let c = 0; c <= columns; c++) {
        const angle = lerp(-widest, widest, c / columns);
        const end = lerp(backEnd, sideEnd, Math.abs(angle / widest) ** 1.5);
        const tip = c % 2 === 1 ? (anime ? 0.035 : 0.012) * s : 0;

        for (let r = 0; r <= rows; r++) {
            // Smoothed across the columns, never pulled in.
            const radius = Math.max(
                radii[c][r],
                (radii[Math.max(0, c - 1)][r] +
                    radii[c][r] +
                    radii[Math.min(columns, c + 1)][r]) /
                    3,
            );
            const y = lerp(top, end, r / rows) + (r === rows ? tip : 0);

            point.set(
                frame.centre.x + Math.sin(angle) * radius,
                y,
                frame.centre.z - Math.cos(angle) * radius,
            );
            positions.push(point.x, point.y, point.z);
            const shade = 1 - 0.18 * (r / rows);
            colors.push(shade, shade, shade, 1);
            onHead.push(1 - smoothstep(frame.centre.y - y, 0.05, 0.2));
        }
    }

    const indices: number[] = [];

    for (let c = 0; c < columns; c++) {
        for (let r = 0; r < rows; r++) {
            const a = c * (rows + 1) + r;
            const b = a + rows + 1;
            indices.push(a, b, a + 1, b, b + 1, a + 1);
        }
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute(
        'position',
        new THREE.Float32BufferAttribute(positions, 3),
    );
    geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 4));
    geometry.setIndex(indices);
    geometry.computeVertexNormals();

    return skinned(body, geometry, onHead, material, 'Hair_curtain');
}

/** A tail tied at the crown, falling down the back and narrowing to a point. */
function ponytail(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    anime: boolean,
    material: THREE.Material,
): THREE.SkinnedMesh {
    const s = frame.scale;
    const outline = new Outline(body, frame);
    const behind = (y: number, gap: number) =>
        frame.centre.z - outline.at(0, y) - gap;
    const tie = frame.centre.y + 0.035 * s;
    const length = (anime ? 0.36 : 0.28) * s;
    const curve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(0, tie, behind(tie, -0.012 * s)),
        new THREE.Vector3(0, tie - 0.01 * s, behind(tie, 0.04 * s)),
        new THREE.Vector3(0, tie - 0.12 * s, behind(tie - 0.12 * s, 0.035 * s)),
        new THREE.Vector3(0, tie - length, behind(tie - length, 0.025 * s)),
    ]);
    const segments = 24;
    const sides = 10;
    const geometry = new THREE.TubeGeometry(
        curve,
        segments,
        0.026 * s,
        sides,
        false,
    );
    const position = geometry.getAttribute('position');
    const centre = new THREE.Vector3();
    const p = new THREE.Vector3();
    const onHead: number[] = [];

    for (let i = 0; i <= segments; i++) {
        const u = i / segments;
        // Thick just below the tie, thin at the tip.
        const thickness =
            u < 0.2
                ? lerp(0.7, 1.15, u / 0.2)
                : lerp(1.15, 0.12, (u - 0.2) / 0.8);
        curve.getPointAt(u, centre);

        for (let j = 0; j <= sides; j++) {
            const k = i * (sides + 1) + j;
            p.fromBufferAttribute(position, k).sub(centre);
            p.multiplyScalar(thickness).add(centre);
            position.setXYZ(k, p.x, p.y, p.z);
            onHead.push(1 - smoothstep(u, 0.15, 0.55));
        }
    }

    geometry.deleteAttribute('uv');
    geometry.computeVertexNormals();

    return skinned(body, geometry, onHead, material, 'Hair_tail');
}

/**
 * A beard cut from the face: stubble (faint, everywhere a full beard
 * grows), a moustache, a goatee (moustache and chin) or a full beard
 * (along the jaw up to the sideburns). The lips stay clear.
 */
function beard(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    kind: Exclude<Beard, 'none'>,
    color: number,
    style: BodyStyle,
): THREE.SkinnedMesh {
    const s = frame.scale;
    const eyeY = (frame.eyes[0].y + frame.eyes[1].y) / 2;
    const position = body.geometry.getAttribute('position');
    const cover = new Float32Array(position.count);
    const p = new THREE.Vector3();
    const edge = 0.004 * s;

    for (let i = 0; i < position.count; i++) {
        if (frame.onHead[i] < 0.2) {
            continue;
        }

        restPoint(body, i, p);
        const x = Math.abs(p.x);
        const ahead = inside(p.z - (frame.centre.z - 0.01 * s), 0.01 * s);
        const lips = Math.min(
            inside(0.026 * s - x, edge),
            inside(0.0075 * s - Math.abs(p.y - frame.mouth), edge),
        );
        const moustache = Math.min(
            inside(0.03 * s - x, edge),
            inside(p.y - (frame.mouth + 0.006 * s), edge),
            inside(frame.nose.y - 0.014 * s - p.y, edge),
            inside(p.z - (frame.nose.z - 0.045 * s), edge),
        );
        const chin = Math.min(
            inside(0.028 * s - x, edge),
            inside(frame.mouth - 0.007 * s - p.y, edge),
        );
        // The beard line rises from the corners of the mouth to the ears.
        const line = lerp(
            frame.mouth - 0.004 * s,
            eyeY - 0.03 * s,
            smoothstep(x, 0.025 * s, 0.065 * s),
        );
        const jaw = inside(line - p.y, edge);
        const under = inside(p.y - (frame.chin - 0.035 * s), edge);
        const ears = inside(x - (frame.halfWidth - 0.016 * s), edge);
        const grows =
            kind === 'mustache'
                ? moustache
                : kind === 'goatee'
                  ? Math.max(moustache, chin)
                  : Math.max(moustache, jaw);

        cover[i] = Math.min(
            grows,
            1 - lips,
            ahead,
            under,
            1 - ears,
            frame.onHead[i] + 0.3,
        );
    }

    const stubble = kind === 'stubble';
    const shade = new THREE.Color(color).multiplyScalar(stubble ? 0.7 : 0.85);
    const material = new THREE.MeshStandardMaterial({
        color: shade,
        roughness: 0.9,
        transparent: stubble,
        opacity: stubble ? 0.5 : 1,
        alphaTest: stubble ? 0.2 : 0.5,
        depthWrite: !stubble,
    });
    const piece = cutShell(
        body,
        cover,
        (stubble ? 0.0012 : 0.003) * s,
        cutOut(material, style),
    );

    piece.name = 'Beard';

    return piece;
}

/**
 * The models' own hairstyle taken from the other model: its hair mesh
 * fitted to this head (rest-pose to rest-pose, by the heads' sizes) and
 * tied to the head bone.
 */
export function borrowHair(
    body: THREE.SkinnedMesh,
    frame: HeadFrame,
    donor: THREE.Object3D,
    name: string,
    material: THREE.Material,
): THREE.SkinnedMesh | null {
    const theirs = measureHead(donor);
    const hair = named(donor, name);

    if (!theirs || !hair) {
        return null;
    }

    const geometry = hair.geometry.clone();
    const position = geometry.getAttribute('position');
    const p = new THREE.Vector3();
    const size = new THREE.Vector3(
        frame.halfWidth / theirs.halfWidth,
        (frame.top - frame.chin) / (theirs.top - theirs.chin),
        (frame.front - frame.back) / (theirs.front - theirs.back),
    );

    for (let i = 0; i < position.count; i++) {
        restPoint(hair, i, p)
            .sub(theirs.centre)
            .multiply(size)
            .add(frame.centre);
        position.setXYZ(i, p.x, p.y, p.z);
    }

    geometry.deleteAttribute('skinIndex');
    geometry.deleteAttribute('skinWeight');

    return skinned(
        body,
        geometry,
        new Array<number>(position.count).fill(1),
        material,
        name,
    );
}
