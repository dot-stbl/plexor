// MSW request handlers — composed from kubb-generated per-operation factories,
// fed with kubb-generated faker fixtures. Hand-maintained: add one line per new
// endpoint (or regenerate + append). Survives codegen `clean:true` (lives outside ./src).
import type { RequestHandler } from 'msw';
import {
  getHealthHandler,
  getHandler,
  createGetHealthQueryResponse,
  createGetQueryResponse,
} from '@/shared/api';

export const handlers: RequestHandler[] = [
  getHealthHandler(createGetHealthQueryResponse()),
  getHandler(createGetQueryResponse()),
];
