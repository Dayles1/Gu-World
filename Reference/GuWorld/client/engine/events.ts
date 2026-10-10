/**
 * A small typed event bus: systems tell each other what happened without
 * calling each other directly. Handlers are kept in a set, so the same
 * function is never registered twice; `on` gives back the way to
 * unsubscribe, and a Subscriptions bag lets a system drop everything it
 * listens to when it is disposed (and so a restarted system does not end
 * up listening twice). A handler that throws is reported and skipped —
 * the others still hear the event.
 */

export type Handler<T> = (payload: T) => void;

export class EventBus<Events extends object> {
    private handlers = new Map<keyof Events, Set<Handler<never>>>();

    /** @param onError where a failing handler is reported */
    constructor(
        private onError: (type: keyof Events, error: unknown) => void = (
            type,
            error,
        ) =>
            console.error(
                `GU World: a "${String(type)}" handler failed`,
                error,
            ),
    ) {}

    on<K extends keyof Events>(
        type: K,
        handler: Handler<Events[K]>,
    ): () => void {
        let set = this.handlers.get(type);

        if (!set) {
            set = new Set();
            this.handlers.set(type, set);
        }

        set.add(handler as Handler<never>);

        return () => this.off(type, handler);
    }

    off<K extends keyof Events>(type: K, handler: Handler<Events[K]>): void {
        this.handlers.get(type)?.delete(handler as Handler<never>);
    }

    emit<K extends keyof Events>(type: K, payload: Events[K]): void {
        const set = this.handlers.get(type);

        if (!set) {
            return;
        }

        // A copy: handlers may subscribe or unsubscribe while being called.
        for (const handler of [...set] as Handler<Events[K]>[]) {
            try {
                handler(payload);
            } catch (error) {
                this.onError(type, error);
            }
        }
    }

    /** How many handlers listen for `type`. */
    count(type: keyof Events): number {
        return this.handlers.get(type)?.size ?? 0;
    }

    clear(): void {
        this.handlers.clear();
    }
}

/** What a system listens to, dropped all at once. */
export class Subscriptions {
    private undo: (() => void)[] = [];

    add(unsubscribe: () => void): void {
        this.undo.push(unsubscribe);
    }

    dispose(): void {
        for (const unsubscribe of this.undo.splice(0)) {
            unsubscribe();
        }
    }
}
