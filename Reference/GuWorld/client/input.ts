/**
 * Keyboard and mouse. Held keys (moving, running, swimming up and down)
 * are read when the simulation steps, while the game is `active`; one-off
 * keys (jump, crouch, crawl, the debug panel, Esc) arrive as actions. The
 * mouse turns the camera only while the pointer is locked to the game: a
 * click locks it, Esc frees it (a browser that refuses the lock still
 * lets one walk). Everything it listens to is let go in `dispose`.
 *
 * Built on the Sandbox's input, trimmed to what GU World has so far (no
 * touch controls yet).
 */

export type Action = 'jump' | 'crouch' | 'crawl' | 'debug' | 'mark' | 'escape';

const BINDINGS: Record<string, Action> = {
    Space: 'jump',
    KeyC: 'crouch',
    ControlLeft: 'crouch',
    KeyZ: 'crawl',
    F3: 'debug',
    F6: 'mark',
    Escape: 'escape',
};

const FORWARD = ['KeyW', 'ArrowUp'];
const BACK = ['KeyS', 'ArrowDown'];
const LEFT = ['KeyA', 'ArrowLeft'];
const RIGHT = ['KeyD', 'ArrowRight'];
const SPRINT = ['ShiftLeft', 'ShiftRight'];
const RISE = ['Space'];
const DIVE = ['KeyC', 'ControlLeft'];
/** Keys the browser must not act on while playing. */
const CAPTURED = [...FORWARD, ...BACK, ...LEFT, ...RIGHT, 'Space', 'F3', 'F6'];

export class Input {
    /** Pointer locked to the game: the mouse turns the camera. */
    locked = false;
    /** Playing (not paused): held keys count. */
    active = false;
    onLockChange: (locked: boolean) => void = () => {};
    onAction: (action: Action) => void = () => {};

    private keys = new Set<string>();
    private mouseX = 0;
    private mouseY = 0;
    private wheel = 0;
    private listening: [
        EventTarget,
        string,
        EventListener,
        AddEventListenerOptions?,
    ][] = [];

    constructor(private element: HTMLElement) {
        this.listen(window, 'keydown', this.keyDown);
        this.listen(window, 'keyup', this.keyUp);
        this.listen(window, 'blur', this.release);
        this.listen(document, 'mousemove', this.mouseMove);
        this.listen(document, 'pointerlockchange', this.lockChange);
        this.listen(element, 'wheel', this.mouseWheel, { passive: false });
        this.listen(element, 'contextmenu', (event) => event.preventDefault());
    }

    /** Starts playing: locks the pointer; false when the browser refuses. */
    async lock(): Promise<boolean> {
        try {
            await this.element.requestPointerLock?.();

            return true;
        } catch {
            return false;
        }
    }

    unlock(): void {
        if (document.pointerLockElement) {
            document.exitPointerLock();
        }
    }

    /** -1…1 along each axis, relative to where the camera looks. */
    get forward(): number {
        return this.active ? this.axis(FORWARD, BACK) : 0;
    }

    get strafe(): number {
        return this.active ? this.axis(RIGHT, LEFT) : 0;
    }

    get sprint(): boolean {
        return this.active && this.held(SPRINT);
    }

    get rise(): boolean {
        return this.active && this.held(RISE);
    }

    get dive(): boolean {
        return this.active && this.held(DIVE);
    }

    /** Mouse movement and wheel since the last call. */
    takeLook(): { x: number; y: number; wheel: number } {
        const look = { x: this.mouseX, y: this.mouseY, wheel: this.wheel };
        this.mouseX = 0;
        this.mouseY = 0;
        this.wheel = 0;

        return look;
    }

    dispose(): void {
        for (const [target, type, listener, options] of this.listening.splice(
            0,
        )) {
            target.removeEventListener(type, listener, options);
        }

        this.unlock();
        this.release();
    }

    private listen<E extends Event>(
        target: EventTarget,
        type: string,
        listener: (event: E) => void,
        options?: AddEventListenerOptions,
    ): void {
        target.addEventListener(type, listener as EventListener, options);
        this.listening.push([target, type, listener as EventListener, options]);
    }

    private held(codes: string[]): boolean {
        return codes.some((code) => this.keys.has(code));
    }

    private axis(positive: string[], negative: string[]): number {
        return (this.held(positive) ? 1 : 0) - (this.held(negative) ? 1 : 0);
    }

    private keyDown = (event: KeyboardEvent): void => {
        if (this.active && CAPTURED.includes(event.code)) {
            event.preventDefault();
        }

        if (this.active) {
            this.keys.add(event.code);
        }

        const action = BINDINGS[event.code];

        if (action && !event.repeat) {
            this.onAction(action);
        }
    };

    private keyUp = (event: KeyboardEvent): void => {
        this.keys.delete(event.code);
    };

    private mouseMove = (event: MouseEvent): void => {
        if (this.locked) {
            this.mouseX += event.movementX;
            this.mouseY += event.movementY;
        }
    };

    private mouseWheel = (event: WheelEvent): void => {
        event.preventDefault();
        this.wheel += Math.sign(event.deltaY);
    };

    private release = (): void => {
        this.keys.clear();
    };

    private lockChange = (): void => {
        this.locked = document.pointerLockElement === this.element;

        if (!this.locked) {
            this.release();
        }

        this.onLockChange(this.locked);
    };
}
