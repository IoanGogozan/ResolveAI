import { NavLink, Outlet } from 'react-router-dom';
import { IdentitySwitcher } from '@/auth/IdentitySwitcher';
import { developmentIdentitiesEnabled, useIdentity } from '@/auth/identity';

export function AppShell() {
  const { selected } = useIdentity();

  return (
    <div className="shell">
      <header className="shell__header">
        <div className="shell__brand">
          <strong>ResolveAI</strong>
          <span className="muted">operator console</span>
        </div>
        <IdentitySwitcher />
      </header>
      <nav className="shell__nav" aria-label="Sections">
        <NavLink to="/" end>
          Cases
        </NavLink>
        <NavLink to="/diagnostics">Diagnostics</NavLink>
      </nav>
      <main className="shell__main">
        {!developmentIdentitiesEnabled ? (
          <div className="outcome" role="alert">
            <div className="outcome__header">
              <span className="outcome__code">No authentication provider configured</span>
            </div>
            <p className="outcome__explanation">
              Development identities are disabled in this build and no OIDC provider is wired yet, so the console cannot
              authenticate. This mirrors the host, which also refuses to start without a configured provider.
            </p>
          </div>
        ) : selected === null ? (
          <div className="notice">
            Select an identity to begin. Each fixture identity maps to a tenant and a permission set on the backend; the
            console never sends a tenant or a permission of its own.
          </div>
        ) : (
          <Outlet />
        )}
      </main>
      <footer className="shell__footer muted">
        AI proposes; the backend validates, authorizes and executes. Policy evaluation, approval and refund execution are
        not implemented yet, so nothing in this console can execute a business action.
      </footer>
    </div>
  );
}
