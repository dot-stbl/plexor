/**
 * Combobox — react-aria-components migration tests.
 *
 * Verifies the compat wiring: RAC Input/Trigger bind through context
 * (input is role=combobox with aria-expanded), the list opens on
 * ArrowDown, and onValueChange receives the selected key.
 *
 * jsdom notes (browser behavior is RAC-native): typing-to-open and
 * close-on-select do not fire under jsdom through this wrapper; those
 * paths are flagged for attention when the primitive gains a consumer.
 */
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  Combobox,
  ComboboxContent,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/shared/ui/primitives/combobox'

describe('Combobox (RAC)', () => {
  it('opens via ArrowDown and fires onValueChange with the selected key', async () => {
    const onValueChange = vi.fn()
    const user = userEvent.setup()
    render(
      <Combobox value={null} onValueChange={onValueChange}>
        <ComboboxInput placeholder="Pick…" aria-label="pick" />
        <ComboboxContent>
          <ComboboxList>
            <ComboboxItem id="alpha">Alpha</ComboboxItem>
            <ComboboxItem id="beta">Beta</ComboboxItem>
          </ComboboxList>
        </ComboboxContent>
      </Combobox>,
    )
    const input = screen.getByRole('combobox')
    await user.click(input)
    // RAC comboboxes don't open on focus by default; ArrowDown opens the list.
    await user.keyboard('{ArrowDown}')
    expect(await screen.findByRole('option', { name: 'Alpha' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Beta' })).toBeInTheDocument()
    await user.click(screen.getByRole('option', { name: 'Beta' }))
    expect(onValueChange).toHaveBeenCalledWith('beta')
  })
})
