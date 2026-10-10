/**
 * Light by the time of day: where the sun is, how bright and what colour,
 * the sky's and the haze's colour. A pure function of the clock's time of
 * day (0 midnight, 0.5 noon), so it is the same for anyone at the same
 * game minute and can be tested without a screen. The sun rises at 06:00
 * in the east (+X) and sets at 18:00 in the west.
 */

import * as THREE from 'three';

export interface DayLight {
    /** Toward the sun, unit length. */
    sunDirection: THREE.Vector3;
    sunColor: THREE.Color;
    sunIntensity: number;
    sky: THREE.Color;
    ground: THREE.Color;
    ambientIntensity: number;
}

const NIGHT_SKY = new THREE.Color(0x0b1020);
const LOW_SKY = new THREE.Color(0xc99a80);
const DAY_SKY = new THREE.Color(0xa7bccd);
const NIGHT_GROUND = new THREE.Color(0x14171d);
const DAY_GROUND = new THREE.Color(0x6b6a5c);
const LOW_SUN = new THREE.Color(0xffb07a);
const HIGH_SUN = new THREE.Color(0xfff3e2);

const SUN_STRENGTH = 2.2;

const smoothstep = THREE.MathUtils.smoothstep;

export function dayLight(timeOfDay: number): DayLight {
    const angle = (timeOfDay - 0.25) * Math.PI * 2;
    const sunDirection = new THREE.Vector3(
        Math.cos(angle) * 0.85,
        Math.sin(angle),
        0.35,
    ).normalize();
    const height = sunDirection.y;
    const day = smoothstep(height, -0.1, 0.25);
    const low = 1 - smoothstep(Math.abs(height), 0, 0.3);

    return {
        sunDirection,
        sunColor: HIGH_SUN.clone().lerp(LOW_SUN, low),
        sunIntensity: SUN_STRENGTH * smoothstep(height, -0.02, 0.18),
        sky: NIGHT_SKY.clone()
            .lerp(DAY_SKY, day)
            .lerp(LOW_SKY, low * 0.5),
        ground: NIGHT_GROUND.clone().lerp(DAY_GROUND, day),
        ambientIntensity: 0.3 + 1.1 * day,
    };
}
