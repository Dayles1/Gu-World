import { describe, expect, it, vi } from 'vitest';
import { EventBus, Subscriptions } from './events';

interface TestEvents {
    ping: { n: number };
    pong: { text: string };
}

describe('EventBus', () => {
    it('delivers an event to its handlers only', () => {
        const bus = new EventBus<TestEvents>();
        const ping = vi.fn();
        const pong = vi.fn();

        bus.on('ping', ping);
        bus.on('pong', pong);
        bus.emit('ping', { n: 1 });

        expect(ping).toHaveBeenCalledWith({ n: 1 });
        expect(pong).not.toHaveBeenCalled();
    });

    it('stops delivering after unsubscribing', () => {
        const bus = new EventBus<TestEvents>();
        const handler = vi.fn();
        const unsubscribe = bus.on('ping', handler);

        unsubscribe();
        bus.emit('ping', { n: 1 });

        expect(handler).not.toHaveBeenCalled();
        expect(bus.count('ping')).toBe(0);
    });

    it('never registers the same handler twice', () => {
        const bus = new EventBus<TestEvents>();
        const handler = vi.fn();

        bus.on('ping', handler);
        bus.on('ping', handler);
        bus.emit('ping', { n: 1 });

        expect(handler).toHaveBeenCalledTimes(1);
        expect(bus.count('ping')).toBe(1);
    });

    it('reports a failing handler and still calls the others', () => {
        const errors: string[] = [];
        const bus = new EventBus<TestEvents>((type) =>
            errors.push(String(type)),
        );
        const after = vi.fn();

        bus.on('ping', () => {
            throw new Error('boom');
        });
        bus.on('ping', after);
        bus.emit('ping', { n: 2 });

        expect(errors).toEqual(['ping']);
        expect(after).toHaveBeenCalledWith({ n: 2 });
    });

    it('lets a handler unsubscribe while the event is being sent', () => {
        const bus = new EventBus<TestEvents>();
        const second = vi.fn();
        const unsubscribe = bus.on('ping', () => unsubscribe());

        bus.on('ping', second);
        bus.emit('ping', { n: 1 });
        bus.emit('ping', { n: 2 });

        expect(second).toHaveBeenCalledTimes(2);
        expect(bus.count('ping')).toBe(1);
    });
});

describe('Subscriptions', () => {
    it('drops everything a system listened to, so a restart does not double up', () => {
        const bus = new EventBus<TestEvents>();
        const calls: number[] = [];
        const start = () => {
            const subscriptions = new Subscriptions();
            // A fresh closure each start, as a real system would make.
            subscriptions.add(bus.on('ping', ({ n }) => calls.push(n)));

            return subscriptions;
        };

        start().dispose();
        start();
        bus.emit('ping', { n: 7 });

        expect(calls).toEqual([7]);
        expect(bus.count('ping')).toBe(1);
    });
});
