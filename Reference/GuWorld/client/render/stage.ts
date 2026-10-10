/**
 * What the world is drawn with: the renderer and its canvas, the scene,
 * the sun (with shadows following the hero) and the sky's light. It owns
 * these and frees them in `dispose`, window listeners included. It knows
 * nothing of entities: their pictures are added by the views.
 */

import * as THREE from 'three';
import { dayLight } from './sky';

const SHADOW_REACH = 40;

export class Stage {
    readonly renderer: THREE.WebGLRenderer;
    readonly scene = new THREE.Scene();
    private sun = new THREE.DirectionalLight(0xffffff, 2);
    private ambient = new THREE.HemisphereLight(0xffffff, 0x444444, 1);

    constructor(private host: HTMLElement) {
        this.renderer = new THREE.WebGLRenderer({
            antialias: true,
            powerPreference: 'high-performance',
        });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        this.renderer.toneMapping = THREE.AgXToneMapping;
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.domElement.className = 'gw-canvas';
        host.append(this.renderer.domElement);

        this.scene.fog = new THREE.Fog(0xa7bccd, 60, 260);
        this.sun.castShadow = true;
        this.sun.shadow.mapSize.setScalar(2048);
        Object.assign(this.sun.shadow.camera, {
            left: -SHADOW_REACH,
            right: SHADOW_REACH,
            top: SHADOW_REACH,
            bottom: -SHADOW_REACH,
            near: 1,
            far: 200,
        });
        this.sun.shadow.bias = -0.0004;
        this.sun.shadow.normalBias = 0.03;
        this.scene.add(this.ambient, this.sun, this.sun.target);
        this.resize();
        window.addEventListener('resize', this.resize);
    }

    get aspect(): number {
        return this.host.clientWidth / Math.max(1, this.host.clientHeight);
    }

    /** How far the world is drawn (m): the haze thickens to hide where the drawn chunks end. */
    setReach(reach: number): void {
        const fog = this.scene.fog as THREE.Fog;
        fog.near = reach * 0.4;
        fog.far = reach;
    }

    /** The light of the time of day, around `focus` (where the shadows are needed). */
    light(timeOfDay: number, focus: THREE.Vector3): void {
        const light = dayLight(timeOfDay);

        this.sun.color.copy(light.sunColor);
        this.sun.intensity = light.sunIntensity;
        this.sun.position.copy(focus).addScaledVector(light.sunDirection, 80);
        this.sun.target.position.copy(focus);
        this.ambient.color.copy(light.sky);
        this.ambient.groundColor.copy(light.ground);
        this.ambient.intensity = light.ambientIntensity;
        this.scene.background = light.sky;
        (this.scene.fog as THREE.Fog).color.copy(light.sky);
    }

    render(camera: THREE.Camera): void {
        this.renderer.render(this.scene, camera);
    }

    /** Frees the renderer, the canvas and what the scene still holds. */
    dispose(): void {
        window.removeEventListener('resize', this.resize);
        this.scene.traverse((object) => {
            if (object instanceof THREE.Mesh) {
                object.geometry.dispose();
            }
        });
        this.renderer.dispose();
        this.renderer.domElement.remove();
    }

    private resize = (): void => {
        this.renderer.setSize(
            this.host.clientWidth,
            this.host.clientHeight,
            false,
        );
    };
}
