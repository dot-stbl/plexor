import type { SVGProps } from 'react';

/**
 * Plexor brand mark (the "P" + stacked squares) rendered monochrome in
 * `currentColor`. The source logo uses a lavender→navy gradient; the console
 * direction is monochrome + status accents only, so the mark inherits ink.
 */
export function PlexorMark(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 500 500" fill="currentColor" aria-hidden="true" {...props}>
      <path d="M229 379L229 120L336.305 120C361.343 120 381.552 124.295 396.932 132.886C412.312 141.477 423.579 152.932 430.733 167.25C438.244 181.21 442 196.96 442 214.499C442 227.386 440.033 239.377 436.098 250.473C432.164 261.57 425.904 271.414 417.32 280.004C409.093 288.595 398.542 295.217 385.665 299.871C372.788 304.524 357.229 306.851 338.987 306.851L267.63 306.851L267.63 379L229 379ZM267.63 273.561L334.695 273.561C351.149 273.561 364.383 271.235 374.398 266.581C384.413 261.57 391.746 254.769 396.395 246.178C401.045 237.229 403.37 227.028 403.37 215.573C403.37 204.477 401.045 194.454 396.395 185.505C391.746 176.556 384.413 169.397 374.398 164.028C364.383 158.301 351.327 155.437 335.232 155.437L267.63 155.437L267.63 273.561Z" />
      <rect x="57" y="120" width="88" height="88" />
      <rect x="57" y="292" width="88" height="88" />
    </svg>
  );
}
