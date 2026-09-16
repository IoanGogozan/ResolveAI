import { beforeEach, describe, expect, it } from 'vitest';
import { claimOperation, readPendingOperation, releaseOperation, startNewOperation } from './operations';

const caseId = '0199a0a0-0000-7000-8000-000000000001';

describe('paid operation identity', () => {
  beforeEach(() => sessionStorage.clear());

  it('persists the operation before the request so a lost response can be replayed', () => {
    const claimed = claimOperation(caseId);
    expect(readPendingOperation(caseId)?.operationId).toBe(claimed.operationId);
  });

  it('reuses an unresolved operation instead of buying a second invocation', () => {
    const first = claimOperation(caseId);
    const second = claimOperation(caseId);
    expect(second.operationId).toBe(first.operationId);
  });

  it('only mints a new identity for a deliberate new run', () => {
    const first = claimOperation(caseId);
    const deliberate = startNewOperation(caseId);
    expect(deliberate.operationId).not.toBe(first.operationId);
  });

  it('releases the claim once its run is terminal, and keeps cases independent', () => {
    const other = '0199a0a0-0000-7000-8000-000000000002';
    claimOperation(caseId);
    claimOperation(other);
    releaseOperation(caseId);
    expect(readPendingOperation(caseId)).toBeNull();
    expect(readPendingOperation(other)).not.toBeNull();
  });

  it('ignores corrupted storage rather than replaying a meaningless identifier', () => {
    sessionStorage.setItem(`resolveai.proposal.${caseId}`, '{not json');
    expect(readPendingOperation(caseId)).toBeNull();
  });
});
