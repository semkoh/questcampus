export function Alert({ kind, children }: { kind: 'error' | 'success' | 'info'; children: string | null }) {
  if (!children) return null;
  return <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>{children}</div>;
}
