/**
 * Sends saves to the server, carefully: a save that fails its own check
 * is never sent (and said so); only one request is on its way at a time,
 * and a save made meanwhile is sent right after (only the latest — older
 * ones are worthless by then); a failed request is reported and the game
 * goes on (the next save tries again).
 */

import type { WorldConfig } from '../world/config';
import { checkSave } from './save';
import type { Save } from './save';

export interface SaveTransport {
    put(save: Save, keepalive: boolean): Promise<void>;
}

export interface SaveReport {
    written(): void;
    failed(message: string): void;
    refused(problems: string[]): void;
}

export type SaveOutcome = 'saved' | 'queued' | 'refused' | 'failed';

export class Saver {
    private sending = false;
    private next: { save: Save; keepalive: boolean } | null = null;

    constructor(
        private transport: SaveTransport,
        private config: WorldConfig,
        private report: SaveReport,
    ) {}

    get busy(): boolean {
        return this.sending;
    }

    async save(save: Save, keepalive = false): Promise<SaveOutcome> {
        const problems = checkSave(save, this.config);

        if (problems.length) {
            this.report.refused(problems);

            return 'refused';
        }

        if (this.sending) {
            this.next = { save, keepalive };

            return 'queued';
        }

        this.sending = true;
        let outcome: SaveOutcome;

        try {
            await this.transport.put(save, keepalive);
            this.report.written();
            outcome = 'saved';
        } catch (error) {
            this.report.failed(
                error instanceof Error ? error.message : String(error),
            );
            outcome = 'failed';
        } finally {
            this.sending = false;
        }

        if (this.next) {
            const { save: latest, keepalive: rush } = this.next;
            this.next = null;
            void this.save(latest, rush);
        }

        return outcome;
    }
}
