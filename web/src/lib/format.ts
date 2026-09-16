export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
  } catch {
    // An unexpected currency code must not break the page.
    return `${amount} ${currency}`;
  }
}

export function formatDateTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'medium' });
}

export function formatDuration(milliseconds: number): string {
  if (milliseconds < 1000) return `${milliseconds} ms`;
  return `${(milliseconds / 1000).toFixed(1)} s`;
}

/** Estimated provider cost is reported in USD with more precision than a currency formatter allows. */
export function formatCostUsd(value: number): string {
  return `$${value.toFixed(6)} USD`;
}

export function shortId(value: string): string {
  return value.length > 13 ? `${value.slice(0, 8)}…${value.slice(-4)}` : value;
}
