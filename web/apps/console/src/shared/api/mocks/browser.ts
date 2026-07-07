// Browser MSW worker — started from main.tsx only when VITE_USE_MOCKS=true.
import { setupWorker } from 'msw/browser';
import { handlers } from './handlers';

export const worker = setupWorker(...handlers);
