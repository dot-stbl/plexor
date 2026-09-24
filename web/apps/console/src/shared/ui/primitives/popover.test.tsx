/**
 * Popover — react-aria-components migration tests.
 *
 * Behavior notes carried over from the base-ui era:
 *   - `<PopoverTrigger render={<Button/>}>` merges trigger props onto the
 *     render target; caller children win over the render element's own.
 *   - Uncontrolled popovers toggle on trigger press. (RAC popovers also
 *     close on outside interaction in real browsers, but that path does
 *     not fire under jsdom — verified against raw RAC primitives — so
 *     the toggle is asserted instead.)
 */
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button } from '@/shared/ui/primitives/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover'

function userEventSetup() {
  return userEvent.setup()
}

describe('Popover', () => {
  it('opens on trigger press and toggles closed on second press (uncontrolled)', async () => {
    const user = userEventSetup()
    render(
      <div>
        <Popover>
          <PopoverTrigger render={<Button variant="outline">Menu</Button>} />
          <PopoverContent align="end" side="top">
            <div>popover body</div>
          </PopoverContent>
        </Popover>
        <button type="button">outside</button>
      </div>,
    )
    expect(screen.queryByText('popover body')).toBeNull()
    const trigger = screen.getByRole('button', { name: 'Menu' })
    await user.click(trigger)
    expect(await screen.findByText('popover body')).toBeInTheDocument()
    // RAC hides the rest of the document with aria-hidden while open, so
    // re-use the trigger element handle instead of a role query.
    await user.click(trigger)
    expect(screen.queryByText('popover body')).toBeNull()
  })

  it('controlled open + trigger children survive render prop', () => {
    render(
      <Popover open onOpenChange={() => {}}>
        <PopoverTrigger render={<Button variant="ghost" />}>Triggered</PopoverTrigger>
        <PopoverContent>
          <div>body</div>
        </PopoverContent>
      </Popover>,
    )
    expect(screen.getByText('body')).toBeInTheDocument()
    expect(screen.getByText('Triggered')).toBeInTheDocument()
  })
})
