import { Component, type ErrorInfo, type ReactNode } from 'react';

interface State {
  message: string | null;
}

/** A render failure must not leave a blank page in a console used to demonstrate the backend. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  override state: State = { message: null };

  static getDerivedStateFromError(error: unknown): State {
    return { message: error instanceof Error ? error.message : String(error) };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('Unhandled render failure', error, info.componentStack);
  }

  override render(): ReactNode {
    if (this.state.message === null) return this.props.children;
    return (
      <div className="shell">
        <main className="shell__main">
          <div className="outcome" role="alert">
            <div className="outcome__header">
              <span className="outcome__code">Console failure</span>
            </div>
            <p className="outcome__explanation">{this.state.message}</p>
            <button type="button" className="button" onClick={() => this.setState({ message: null })}>
              Try again
            </button>
          </div>
        </main>
      </div>
    );
  }
}
