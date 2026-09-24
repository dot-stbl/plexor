import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';

/**
 * Shared open/close state for the global `CommandMenu` (§4.3). The dialog
 * is mounted once in `__root.tsx`; the header's search trigger (mounted
 * inside either route group, below root) needs a way to open it without
 * prop-drilling through both `(marketing)/route.tsx` and `(docs)/route.tsx`.
 * A small context is the least machinery that works for a value this
 * simple (one boolean + its setter).
 */
interface CommandMenuContextValue {
  readonly open: boolean;
  readonly setOpen: (open: boolean) => void;
}

const CommandMenuContext = createContext<CommandMenuContextValue | null>(null);

export function CommandMenuProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const value = useMemo<CommandMenuContextValue>(() => ({ open, setOpen }), [open]);
  return <CommandMenuContext.Provider value={value}>{children}</CommandMenuContext.Provider>;
}

export function useCommandMenu(): CommandMenuContextValue {
  const ctx = useContext(CommandMenuContext);
  if (!ctx) {
    throw new Error('useCommandMenu must be used within <CommandMenuProvider>');
  }
  return ctx;
}
