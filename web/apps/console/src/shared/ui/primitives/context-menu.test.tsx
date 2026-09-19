/**
 * ContextMenu — react-aria-components migration tests.
 *
 * Verifies the MenuTrigger trigger="contextMenu" wiring: right-click on
 * the wrapped target opens the menu; items fire their onClick via RAC's
 * onAction; labels/separators render inside the popover outside the Menu.
 */
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuTrigger,
} from '@/shared/ui/primitives/context-menu'

function renderMenu() {
  return render(
    <ContextMenu>
      <ContextMenuTrigger>
        <div data-testid="target">right-click me</div>
      </ContextMenuTrigger>
      <ContextMenuContent>
        <ContextMenuLabel>Actions</ContextMenuLabel>
        <ContextMenuSeparator />
        <ContextMenuItem data-testid="item-rename">Rename</ContextMenuItem>
        <ContextMenuItem data-testid="item-delete" variant="destructive">
          Delete
        </ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>,
  )
}

describe('ContextMenu (RAC)', () => {
  it('opens on contextmenu (right-click) of the wrapped target', () => {
    renderMenu()
    expect(screen.queryByRole('menu')).toBeNull()
    fireEvent.contextMenu(screen.getByTestId('target'))
    expect(screen.getByRole('menu')).toBeInTheDocument()
    expect(screen.getByText('Rename')).toBeInTheDocument()
    expect(screen.getByText('Actions')).toBeInTheDocument()
  })

  it('fires item onClick through RAC onAction', () => {
    const onClick = vi.fn()
    render(
      <ContextMenu>
        <ContextMenuTrigger>
          <div data-testid="target2">t</div>
        </ContextMenuTrigger>
        <ContextMenuContent>
          <ContextMenuItem onClick={onClick}>Act</ContextMenuItem>
        </ContextMenuContent>
      </ContextMenu>,
    )
    fireEvent.contextMenu(screen.getByTestId('target2'))
    fireEvent.click(screen.getByText('Act'))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('carries the destructive variant as data-variant', () => {
    renderMenu()
    fireEvent.contextMenu(screen.getByTestId('target'))
    expect(screen.getByText('Delete').getAttribute('data-variant')).toBe('destructive')
    expect(screen.getByText('Rename').getAttribute('data-variant')).toBe('default')
  })
})
