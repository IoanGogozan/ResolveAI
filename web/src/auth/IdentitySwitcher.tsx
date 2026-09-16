import { useCaller } from './caller';
import { developmentIdentities, developmentIdentitiesEnabled, useIdentity } from './identity';

export function IdentitySwitcher() {
  const { selected, select } = useIdentity();
  const { data: caller } = useCaller();

  if (!developmentIdentitiesEnabled) return null;

  const active = developmentIdentities.find((identity) => identity.id === selected);
  return (
    <div className="identity">
      <label className="identity__label" htmlFor="identity">
        Identity
      </label>
      <select
        id="identity"
        className="identity__select"
        value={selected ?? ''}
        onChange={(event) => select(event.target.value === '' ? null : event.target.value)}
      >
        <option value="">Not signed in</option>
        {developmentIdentities.map((identity) => (
          <option key={identity.id} value={identity.id}>
            {identity.label} ({identity.tenant})
          </option>
        ))}
      </select>
      {active ? (
        <span className="badge" title={`Tenant ${caller?.tenantId ?? 'unknown'}`}>
          Tenant {active.tenant}
        </span>
      ) : null}
      {active ? <span className="identity__note">{active.note}</span> : null}
    </div>
  );
}
