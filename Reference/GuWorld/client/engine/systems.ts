/**
 * Runs the game's systems one after another, each on its own: a system
 * that throws is reported, the others still run. A system failing
 * FAILURE_LIMIT times in a row is switched off (and reported once) so a
 * broken one does not flood the log or keep the frame from finishing;
 * one success resets its count. Switching one system off never touches
 * the others; `enable` gives it another chance.
 */

export interface GameSystem {
    /** Unique among the systems of one runner. */
    readonly name: string;
    /** `step` is seconds: the fixed simulation step, or a frame's time. */
    update(step: number): void;
    dispose?(): void;
}

export interface SystemStatus {
    name: string;
    enabled: boolean;
    /** Failures in a row (reset by a success). */
    failures: number;
    totalFailures: number;
    lastError: string | null;
}

export interface SystemReport {
    failed: (system: string, error: string, failures: number) => void;
    disabled: (system: string, error: string) => void;
}

/** Failures in a row before a system is switched off. */
export const FAILURE_LIMIT = 5;

interface Entry {
    system: GameSystem;
    status: SystemStatus;
}

function describe(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

export class SystemRunner {
    private entries: Entry[] = [];

    constructor(
        private report: SystemReport,
        private limit = FAILURE_LIMIT,
    ) {}

    add(system: GameSystem): void {
        if (this.entries.some((entry) => entry.system.name === system.name)) {
            throw new Error(
                `A system named "${system.name}" is already running`,
            );
        }

        this.entries.push({
            system,
            status: {
                name: system.name,
                enabled: true,
                failures: 0,
                totalFailures: 0,
                lastError: null,
            },
        });
    }

    /** Takes a system out (disposing it). */
    remove(name: string): void {
        const index = this.entries.findIndex(
            (entry) => entry.system.name === name,
        );

        if (index >= 0) {
            const [entry] = this.entries.splice(index, 1);
            this.disposeEntry(entry);
        }
    }

    run(step: number): void {
        for (const entry of this.entries) {
            if (!entry.status.enabled) {
                continue;
            }

            try {
                entry.system.update(step);
                entry.status.failures = 0;
            } catch (error) {
                this.fail(entry, error);
            }
        }
    }

    /** Lets a switched-off system run again. */
    enable(name: string): void {
        const entry = this.entries.find((each) => each.system.name === name);

        if (entry) {
            entry.status.enabled = true;
            entry.status.failures = 0;
        }
    }

    status(): SystemStatus[] {
        return this.entries.map((entry) => ({ ...entry.status }));
    }

    dispose(): void {
        for (const entry of this.entries.splice(0)) {
            this.disposeEntry(entry);
        }
    }

    private fail(entry: Entry, error: unknown): void {
        const status = entry.status;
        status.failures++;
        status.totalFailures++;
        status.lastError = describe(error);
        this.report.failed(status.name, status.lastError, status.failures);

        if (status.failures >= this.limit) {
            status.enabled = false;
            this.report.disabled(status.name, status.lastError);
        }
    }

    private disposeEntry(entry: Entry): void {
        try {
            entry.system.dispose?.();
        } catch (error) {
            this.report.failed(entry.system.name, describe(error), 0);
        }
    }
}
