/**
 * PNG→WEBP conversion via ffmpeg — Playwright's `page.screenshot()`
 * only writes png/jpeg, so the raw capture always lands as a `.png`
 * first. Screenshots are taken at `deviceScaleFactor: 2` (see
 * `capture.ts`) and downsampled here to the published 1280px width —
 * the same supersample-then-downscale trick keeps text crisp.
 */

import { statSync } from 'node:fs';
import { join } from 'node:path';

const TARGET_WIDTH = 1280;

async function run(cmd: string[]): Promise<void> {
  const proc = Bun.spawn({ cmd, stdout: 'pipe', stderr: 'pipe' });
  const [code, stderr] = await Promise.all([proc.exited, new Response(proc.stderr).text()]);
  if (code !== 0) {
    throw new Error(`${cmd[0]} failed (exit ${code}): ${cmd.join(' ')}\n${stderr.slice(-2000)}`);
  }
}

export interface EncodeStillInput {
  readonly pngPath: string;
  readonly outDir: string;
  readonly id: string;
  readonly theme: 'light' | 'dark';
}

export interface EncodeStillOutput {
  readonly webpPath: string;
  readonly sizeBytes: number;
}

export async function encodeStill(input: EncodeStillInput): Promise<EncodeStillOutput> {
  const webpPath = join(input.outDir, `${input.id}.${input.theme}.webp`);

  await run([
    'ffmpeg',
    '-y',
    '-i',
    input.pngPath,
    '-vf',
    `scale=${TARGET_WIDTH}:-2`,
    '-c:v',
    'libwebp',
    '-q:v',
    '82',
    webpPath,
  ]);

  return { webpPath, sizeBytes: statSync(webpPath).size };
}
