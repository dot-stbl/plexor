import { Link } from '@tanstack/react-router';
import { FRAME_CLASS } from './site-frame';
import { GITHUB_URL, VERSION_LABEL } from './nav-config';
import { Panel } from './panel';

interface FooterLink {
  readonly label: string;
  readonly to?: string;
  readonly href?: string;
}

interface FooterColumn {
  readonly heading: string;
  readonly links: readonly FooterLink[];
}

/**
 * Four columns + a bottom bar (spec §2.3) — replaces the old one-row
 * footer on both marketing and docs chrome. Every link here resolves to
 * something that actually exists today: no invented community channels
 * (no Discord/Slack), no newsletter box, no logo wall.
 *
 * Rendered as one inverted `Panel` (the one deliberate "black block"
 * per page in the YC-informed panel system, panel.tsx) sitting inside
 * the shared `FRAME_CLASS` gutter. The outer `<footer>` keeps the
 * full-width bar convention the header already uses today so the page
 * above and the footer below stay left/right aligned site-wide; only
 * the inside became a Panel in the 2026-09-24 landing restyle.
 */
const COLUMNS: readonly FooterColumn[] = [
  {
    heading: 'Product',
    links: [
      { label: 'Changelog', to: '/changelog' },
      { label: 'Roadmap', to: '/changelog#planned' },
    ],
  },
  {
    heading: 'Docs',
    links: [
      { label: 'Getting started', to: '/docs/getting-started' },
      { label: 'Concepts', to: '/docs/concepts' },
      { label: 'How-to', to: '/docs/how-to' },
    ],
  },
  {
    heading: 'Community',
    links: [{ label: 'Discussions', href: `${GITHUB_URL}/discussions` }],
  },
  {
    heading: 'Project',
    links: [
      { label: 'Source (GitHub)', href: GITHUB_URL },
      { label: 'License (MIT)', href: `${GITHUB_URL}/blob/main/LICENSE` },
    ],
  },
];

function FooterLinkItem({ link }: { link: FooterLink }) {
  const className =
    'text-xs text-background/70 transition-colors duration-fast ease-out hover:text-background';
  if (link.to) {
    return (
      <Link to={link.to} className={className}>
        {link.label}
      </Link>
    );
  }
  return (
    <a href={link.href} className={className}>
      {link.label}
    </a>
  );
}

/**
 * One footer, one width (`FRAME_CLASS`) — marketing and docs used to
 * diverge (`max-w-6xl` rails vs. a wider `max-w-7xl` for the docs
 * 3-column grid); both are full width now, so there's no variant left
 * to pick. The inner `Panel` (fill="inverted") is the one deliberate
 * "black block" beat per page in the panel system — text colors and
 * the bottom-bar divider are flipped onto the background/foreground
 * pair (with opacity modifiers) so they read correctly in both light
 * and dark themes; `border-border` would be calibrated for a light
 * panel and is hard to see on an inverted one.
 */
export function SiteFooter() {
  return (
    <footer className={`border-t border-border ${FRAME_CLASS}`}>
      <Panel fill="inverted">
        <div className="grid grid-cols-2 gap-8 md:grid-cols-4">
          {COLUMNS.map((column) => (
            <div key={column.heading}>
              <div className="mb-3 font-mono text-[11px] font-medium uppercase tracking-[0.14em] text-background/50">
                {column.heading}
              </div>
              <ul className="flex flex-col gap-2">
                {column.links.map((link) => (
                  <li key={link.label}>
                    <FooterLinkItem link={link} />
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-10 flex flex-wrap items-center justify-between gap-3 border-t border-background/15 pt-6 text-xs text-background/50">
          <span>© {new Date().getFullYear()} .stbl</span>
          <span className="font-mono">plexor {VERSION_LABEL}</span>
        </div>
      </Panel>
    </footer>
  );
}
