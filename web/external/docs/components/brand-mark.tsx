import type { ReactNode } from 'react';

const BrandColor = '#4f46e5';
const BrandColorAccent = '#22d3ee';

/**
 * Plexor brand mark — three nested squares representing the 3-tier scope
 * hierarchy (Organization → Project → Resource). The outer + inner squares
 * share the brand color; the middle layer is the accent to keep the icon
 * legible at small sizes (16-20px in the sidebar).
 */
export function BrandMark({ className }: { className?: string }): ReactNode {
  return (
    <svg
      viewBox="0 0 32 32"
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      aria-label="Plexor"
    >
      <rect
        x="2"
        y="2"
        width="28"
        height="28"
        rx="6"
        fill="none"
        stroke={BrandColor}
        strokeWidth="2"
      />
      <rect
        x="8"
        y="8"
        width="16"
        height="16"
        rx="3"
        fill="none"
        stroke={BrandColorAccent}
        strokeWidth="2"
      />
      <rect
        x="13"
        y="13"
        width="6"
        height="6"
        rx="1"
        fill={BrandColor}
      />
    </svg>
  );
}