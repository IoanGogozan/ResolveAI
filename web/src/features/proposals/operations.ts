/**
 * A proposal run costs money and is identified by a client-minted operation ID. The ID is persisted
 * *before* the request is sent, so a lost response never leads to a second paid invocation: the same
 * ID is replayed and the host returns the original stored result.
 */

export interface PendingOperation {
  operationId: string;
  startedAt: number;
}

const keyPrefix = 'resolveai.proposal.';

function key(caseId: string): string {
  return `${keyPrefix}${caseId}`;
}

export function readPendingOperation(caseId: string): PendingOperation | null {
  const raw = sessionStorage.getItem(key(caseId));
  if (raw === null) return null;
  try {
    const parsed = JSON.parse(raw) as Partial<PendingOperation>;
    if (typeof parsed.operationId !== 'string' || typeof parsed.startedAt !== 'number') return null;
    return { operationId: parsed.operationId, startedAt: parsed.startedAt };
  } catch {
    return null;
  }
}

/** Reuses an unresolved operation when one exists; only a deliberate new run mints a new ID. */
export function claimOperation(caseId: string, now: number = Date.now()): PendingOperation {
  const existing = readPendingOperation(caseId);
  if (existing !== null) return existing;
  const claimed: PendingOperation = { operationId: crypto.randomUUID(), startedAt: now };
  sessionStorage.setItem(key(caseId), JSON.stringify(claimed));
  return claimed;
}

export function startNewOperation(caseId: string, now: number = Date.now()): PendingOperation {
  const claimed: PendingOperation = { operationId: crypto.randomUUID(), startedAt: now };
  sessionStorage.setItem(key(caseId), JSON.stringify(claimed));
  return claimed;
}

export function releaseOperation(caseId: string): void {
  sessionStorage.removeItem(key(caseId));
}
