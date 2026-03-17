import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { Alert, Snackbar } from '@mui/material';

export type ToastSeverity = 'success' | 'info' | 'warning' | 'error';

interface Toast {
  id: number;
  message: string;
  severity: ToastSeverity;
}

interface ToastContextValue {
  push: (message: string, severity?: ToastSeverity) => void;
}

const ToastContext = createContext<ToastContextValue | undefined>(undefined);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [queue, setQueue] = useState<Toast[]>([]);
  const [active, setActive] = useState<Toast | null>(null);

  const processQueue = useCallback(() => {
    if (!active && queue.length > 0) {
      setActive(queue[0]);
      setQueue((prev) => prev.slice(1));
    }
  }, [active, queue]);

  const push = useCallback((message: string, severity: ToastSeverity = 'info') => {
    setQueue((prev) => [...prev, { id: Date.now() + Math.random(), message, severity }]);
  }, []);

  const handleClose = () => {
    setActive(null);
  };

  React.useEffect(() => {
    processQueue();
  }, [processQueue, queue, active]);

  const value = useMemo(() => ({ push }), [push]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <Snackbar
        open={Boolean(active)}
        autoHideDuration={4000}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert
          onClose={handleClose}
          severity={active?.severity ?? 'info'}
          variant="filled"
          sx={{ visibility: active ? 'visible' : 'hidden' }}
        >
          {active?.message ?? ''}
        </Alert>
      </Snackbar>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error('useToast must be used within ToastProvider');
  }
  return ctx;
}
