import { describe, expect, it } from 'vitest';
import type { ProposalRun } from '@/api/types';
import { proposalRun } from '@/test/backend';
import { pollingInterval } from './api';

function runAt(status: string, ageMs: number): ProposalRun[] {
  return [proposalRun({ status, createdAt: new Date(Date.now() - ageMs).toISOString() })];
}

describe('run polling', () => {
  it('stops as soon as every run is terminal', () => {
    expect(pollingInterval(runAt('Completed', 0))).toBe(false);
    expect(pollingInterval(runAt('Failed', 0))).toBe(false);
    expect(pollingInterval(runAt('Interrupted', 0))).toBe(false);
    expect(pollingInterval([])).toBe(false);
    expect(pollingInterval(undefined)).toBe(false);
  });

  it('backs off while a run is still in flight', () => {
    expect(pollingInterval(runAt('Running', 500))).toBe(1000);
    expect(pollingInterval(runAt('Running', 8000))).toBe(2000);
    expect(pollingInterval(runAt('Running', 40_000))).toBe(5000);
  });

  it('gives up past the lease window instead of polling forever', () => {
    // The host resolves an abandoned claim to an uncertain outcome; the client does not need to wait longer.
    expect(pollingInterval(runAt('Running', 200_000))).toBe(false);
  });
});
