import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Outcome } from '@/components/Outcome';
import { Panel } from '@/components/primitives';
import { useCreateCase } from './api';

// These limits mirror the backend's validation so the operator gets immediate feedback. The backend
// remains the authority: a request that slips past them is still rejected with 400 ValidationFailed.
const subjectLimit = 200;
const descriptionLimit = 10000;
const referenceLimit = 100;

export function NewCasePage() {
  const navigate = useNavigate();
  const create = useCreateCase();
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [linked, setLinked] = useState(false);
  const [customerId, setCustomerId] = useState('customer-monitor');
  const [orderId, setOrderId] = useState('order-monitor');
  const [paymentId, setPaymentId] = useState('payment-monitor');

  const references = [customerId, orderId, paymentId];
  const referencesValid =
    !linked || references.every((value) => value.trim().length > 0 && value.length <= referenceLimit && value === value.trim());
  const valid =
    subject.trim().length > 0 &&
    subject.length <= subjectLimit &&
    description.trim().length > 0 &&
    description.length <= descriptionLimit &&
    referencesValid;

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || create.isPending) return;
    const item = await create.mutateAsync({
      subject,
      description,
      ...(linked ? { references: { customerId, orderId, paymentId } } : {}),
    });
    void navigate(`/cases/${item.id}`);
  }

  return (
    <Panel title="New case">
      <form className="form" onSubmit={(event) => void submit(event)}>
        <label className="field">
          <span className="field__label">Subject</span>
          <input
            className="field__input"
            value={subject}
            maxLength={subjectLimit}
            onChange={(event) => setSubject(event.target.value)}
            required
          />
          <span className="field__hint">
            {subject.length}/{subjectLimit}
          </span>
        </label>
        <label className="field">
          <span className="field__label">Description</span>
          <textarea
            className="field__input field__input--area"
            value={description}
            maxLength={descriptionLimit}
            rows={5}
            onChange={(event) => setDescription(event.target.value)}
            required
          />
          <span className="field__hint">
            {description.length}/{descriptionLimit}
          </span>
        </label>
        <label className="field field--inline">
          <input type="checkbox" checked={linked} onChange={(event) => setLinked(event.target.checked)} />
          <span>
            Link business references. Without a complete set the case has no resolvable facts and cannot be used for a
            grounded proposal.
          </span>
        </label>
        {linked ? (
          <div className="form__group">
            <label className="field">
              <span className="field__label">Customer ID</span>
              <input className="field__input" value={customerId} onChange={(event) => setCustomerId(event.target.value)} />
            </label>
            <label className="field">
              <span className="field__label">Order ID</span>
              <input className="field__input" value={orderId} onChange={(event) => setOrderId(event.target.value)} />
            </label>
            <label className="field">
              <span className="field__label">Payment ID</span>
              <input className="field__input" value={paymentId} onChange={(event) => setPaymentId(event.target.value)} />
            </label>
          </div>
        ) : null}
        {create.error ? <Outcome error={create.error} requiredPermission="cases.create" /> : null}
        <div className="form__actions">
          <button type="submit" className="button" disabled={!valid || create.isPending}>
            {create.isPending ? 'Creating…' : 'Create case'}
          </button>
          <button type="button" className="button button--quiet" onClick={() => void navigate('/')}>
            Cancel
          </button>
        </div>
      </form>
    </Panel>
  );
}
