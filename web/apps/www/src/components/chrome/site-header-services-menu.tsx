import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { StatusPill } from '@/components/ui/status-pill';
import {
  BENTO_CELLS,
  bentoStatusLabel,
  bentoStatusVariant,
} from '@/components/marketing/bento/bento-data';

/**
 * "Product areas" mega-menu — the marketing header's desktop nav row
 * (`site-header.tsx`). Lists the six product surfaces (`BENTO_CELLS` —
 * the same data the landing's services panel reads) as a preview grid.
 *
 * Built on `Popover`, NOT `DropdownMenu`: RAC `Menu` renders its children
 * through a collection builder with a fake `Document`, so arbitrary
 * content (icons are SVG) inside it crashes with
 * "createElementNS is not a function". This panel is navigation content,
 * not a list of actions — a popover is the right primitive anyway.
 */
export function SiteHeaderServicesMenu() {
  const [open, setOpen] = useState(false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger className="h-auto items-center gap-0.5 bg-transparent px-1.5 py-1 text-xs font-normal text-muted-2 hover:bg-transparent hover:text-foreground">
        Product areas
        <KeyboardArrowDown aria-hidden className={cn('size-3.5 transition-transform', open && 'rotate-180')} />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[500px] gap-1 p-3">
        <div className="grid grid-cols-2 gap-1">
          {BENTO_CELLS.map((cell) => {
            const CellIcon = cell.icon;
            return (
              <Link
                key={cell.id}
                to="/"
                hash="services"
                onClick={() => setOpen(false)}
                className="flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted"
              >
                <CellIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                <span className="flex-1 truncate text-sm font-medium text-foreground">{cell.title}</span>
                <StatusPill variant={bentoStatusVariant(cell.status)} hideDot size="sm">
                  {bentoStatusLabel(cell.status)}
                </StatusPill>
              </Link>
            );
          })}
        </div>
        <Link
          to="/"
          hash="services"
          onClick={() => setOpen(false)}
          className="mt-1 rounded-md px-2 py-1.5 text-center text-sm font-medium text-foreground hover:bg-muted"
        >
          View all services →
        </Link>
      </PopoverContent>
    </Popover>
  );
}
