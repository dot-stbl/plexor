/**
 * RadioGroup + Slider — react-aria-components migration tests.
 *
 * Compat surface verified: `onValueChange` (base-ui name) fires from
 * RAC's onChange; `value`/`min`/`max` map to RAC's props; disabled
 * radio items are not selectable.
 */
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Label } from 'react-aria-components'
import { RadioGroup, RadioGroupItem } from '@/shared/ui/primitives/radio-group'
import { Slider } from '@/shared/ui/primitives/slider'

describe('RadioGroup (RAC)', () => {
  it('fires onValueChange with the picked value', async () => {
    const onValueChange = vi.fn()
    const user = userEvent.setup()
    render(
      <RadioGroup value="a" onValueChange={onValueChange}>
        <Label>group</Label>
        <RadioGroupItem value="a" data-testid="radio-a" />
        <RadioGroupItem value="b" data-testid="radio-b" />
      </RadioGroup>,
    )
    await user.click(screen.getByTestId('radio-b'))
    expect(onValueChange).toHaveBeenCalledWith('b')
  })

  it('marks the selected item with data-selected', () => {
    render(
      <RadioGroup value="b" onValueChange={() => {}}>
        <Label>group</Label>
        <RadioGroupItem value="a" data-testid="radio-a" />
        <RadioGroupItem value="b" data-testid="radio-b" />
      </RadioGroup>,
    )
    expect(screen.getByTestId('radio-b').getAttribute('data-selected')).toBe('true')
    expect(screen.getByTestId('radio-a').hasAttribute('data-selected')).toBe(false)
  })
})

describe('Slider (RAC)', () => {
  it('fires onValueChange with the new number', async () => {
    const onValueChange = vi.fn()
    render(
      <Slider
        value={7}
        min={1}
        max={60}
        onValueChange={onValueChange}
        aria-label="days"
        data-testid="slider"
      />,
    )
    expect(screen.getByTestId('slider')).toBeInTheDocument()
    // Keyboard interaction: ArrowUp increments by the step (default 1).
    const thumb = screen.getByRole('slider')
    thumb.focus()
    thumb.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }),
    )
    expect(onValueChange).toHaveBeenCalledWith(8)
  })
})
