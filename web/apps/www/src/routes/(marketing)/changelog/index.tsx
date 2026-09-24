import { createFileRoute } from '@tanstack/react-router';
import { SiteFrame } from '@/components/chrome/site-frame';
import { EYEBROW_CLASS, Panel, PanelContainer, PanelStack } from '@/components/chrome/panel';
import { GITHUB_URL } from '@/components/chrome/nav-config';
import { ChangelogList } from '@/components/changelog/changelog-list';

/**
 * `/changelog` — same flat-panel language as the landing (YC-informed
 * restyle, 2026-09-24): a hero panel + the shipped/roadmap panels
 * `ChangelogList` renders, all stacked inside one `PanelContainer`.
 * MDX-driven release history (spec §5, amendment A8): shipped releases
 * first, then a clearly separate "Roadmap" group for `next`/`design`
 * entries. No calendar dates anywhere — there are no git tags yet,
 * ordering is by version number only (`ChangelogList` sorts via
 * `sortByVersionDescending`).
 */
export const Route = createFileRoute('/(marketing)/changelog/')({
  component: ChangelogPage,
  head: () => ({
    meta: [
      { title: 'plexor — changelog' },
      {
        name: 'description',
        content:
          'Every Plexor release and open proposal, in version order — what shipped, and what is next.',
      },
    ],
  }),
});

function ChangelogPage() {
  return (
    <SiteFrame>
      <PanelContainer>
        <PanelStack>
          <Panel fill="card">
            <p className={EYEBROW_CLASS}>Changelog</p>
            <h1 className="mt-2 text-4xl font-extrabold tracking-tight text-foreground md:text-5xl">
              Changelog
            </h1>
            <p className="mt-3 max-w-2xl text-base leading-7 text-muted-foreground md:text-lg md:leading-8">
              What shipped, and what is next — sourced from the same curated
              list the project tracks internally. No fabricated dates: there
              are no release tags yet, so entries are ordered by version
              number only.
            </p>
            <a
              href={`${GITHUB_URL}/commits/main`}
              target="_blank"
              rel="noreferrer"
              className="mt-4 inline-flex items-center gap-1 text-xs text-muted-2 transition-colors duration-fast ease-out hover:text-foreground"
            >
              Full commit history on GitHub →
            </a>
          </Panel>

          <ChangelogList />
        </PanelStack>
      </PanelContainer>
    </SiteFrame>
  );
}
