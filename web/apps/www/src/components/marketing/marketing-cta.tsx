import { Link } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';
import { Reveal } from '@/components/motion';
import { GitHubIcon } from '@/components/chrome/github-icon';
import { GITHUB_URL } from '@/components/chrome/nav-config';
import {
  INVERTED_BUTTON_FILLED_CLASS,
  INVERTED_BUTTON_OUTLINE_CLASS,
} from '@/components/chrome/panel';

/**
 * Final CTA. Destination stays the install + first-boot path
 * (`/docs/getting-started`), not a theming page — this is where an
 * operator starts running Plexor, not where they configure its look.
 *
 * No background canvas any more (see `marketing-hero.tsx` — same
 * product-owner feedback applies here, this was the storyline's other
 * `SceneBackground` mount).
 */
export function MarketingCta() {
  return (
    <Reveal className="flex flex-col items-start gap-6">
      <p className="text-sm font-medium text-background/50">Next step</p>
      <h2 className="max-w-2xl text-5xl font-extrabold tracking-tight text-background md:text-6xl">
        Read the operator guide.
      </h2>
      <p className="max-w-xl text-base leading-7 text-background/70 md:text-lg">
        Install, run, troubleshoot and extend — the full path from a
        clone to a working cluster.
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <Button
          size="lg"
          className={INVERTED_BUTTON_FILLED_CLASS}
          render={<Link to="/docs/getting-started">Open the docs</Link>}
        />
        <Button
          variant="outline"
          size="lg"
          className={INVERTED_BUTTON_OUTLINE_CLASS}
          render={<a href={GITHUB_URL} target="_blank" rel="noreferrer" />}
        >
          <GitHubIcon className="size-4" />
          GitHub
        </Button>
      </div>
    </Reveal>
  );
}
