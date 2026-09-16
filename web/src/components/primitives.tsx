import type { ReactNode } from 'react';
import { formatDateTime, formatMoney } from '@/lib/format';

export function Panel({ title, actions, children }: { title: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="panel">
      <header className="panel__header">
        <h2 className="panel__title">{title}</h2>
        {actions ? <div className="panel__actions">{actions}</div> : null}
      </header>
      <div className="panel__body">{children}</div>
    </section>
  );
}

export function Facts({ children }: { children: ReactNode }) {
  return <dl className="facts">{children}</dl>;
}

export function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="facts__row">
      <dt className="facts__label">{label}</dt>
      <dd className="facts__value">{children}</dd>
    </div>
  );
}

/** Amounts are displayed exactly as the backend returned them; the client never does money arithmetic. */
export function Money({ amount, currency }: { amount: number; currency: string }) {
  return <span className="money">{formatMoney(amount, currency)}</span>;
}

export function Instant({ value }: { value: string }) {
  return <time dateTime={value}>{formatDateTime(value)}</time>;
}

export function StatusBadge({ status }: { status: string }) {
  const tone =
    status === 'Completed'
      ? 'good'
      : status === 'Failed'
        ? 'bad'
        : status === 'Interrupted'
          ? 'warn'
          : status === 'Running'
            ? 'busy'
            : 'neutral';
  return <span className={`status status--${tone}`}>{status}</span>;
}

export function Loading({ what }: { what: string }) {
  return (
    <p className="muted" role="status">
      Loading {what}…
    </p>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="muted">{children}</p>;
}

export function Code({ children }: { children: ReactNode }) {
  return <code className="code">{children}</code>;
}
