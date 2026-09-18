// Browser MSW worker — started from main.tsx only when VITE_USE_MOCKS=true.
//
// The handlers live in `./handlers.ts` — composed from kubb-generated
// per-operation factories fed with kubb-generated faker fixtures. This
// file only wires the `setupWorker` + the `passthrough` fallback for
// everything the MSW worker should NOT intercept (Vite dev server, HMR,
// asset loads, route navigation).
//
// `onUnhandledRequest: 'bypass'` in `worker.start(...)` already covers the
// SPA's route navigation (TanStack Router History API hits like `/vms/new`
// would otherwise be intercepted by an over-broad `*` MSW matcher). The
// explicit `http.all('*', passthrough)` is a belt-and-suspenders fallback
// for requests MSW might otherwise attempt to handle.
import { setupWorker } from 'msw/browser';
import { http, passthrough } from 'msw';
import { handlers } from './handlers';

const workerHandlers = [
  ...handlers,
  http.all('*', () => passthrough()),
];

export const worker = setupWorker(...workerHandlers);
