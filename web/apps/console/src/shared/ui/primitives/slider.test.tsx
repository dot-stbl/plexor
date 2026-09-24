/**
 * Slider — react-aria-components migration tests.
 *
 * Regression for the slider-thumb-leaks-past-track bug: the thumb was a
 * sibling of the `<span data-slot="slider-track">` inside `<SliderTrack>`,
 * which placed it OUTSIDE the visual track bar. The fix puts the thumb
 * INSIDE the track span as a sibling of `<SliderFill>`, so the thumb's
 * RAC-driven absolute positioning anchors against the same box the
 * track bar fills.
 */
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'

import { Slider } from './slider'

describe('Slider', () => {
  it('renders with data-slot=slider', () => {
    render(<Slider aria-label="Volume" data-testid="s" />)
    expect(screen.getByTestId('s').dataset['slot']).toBe('slider')
  })

  it('renders the track, range, and thumb slots', () => {
    render(<Slider aria-label="Volume" />)
    expect(document.querySelector("[data-slot='slider-track']")).not.toBeNull()
    expect(document.querySelector("[data-slot='slider-range']")).not.toBeNull()
    expect(document.querySelector("[data-slot='slider-thumb']")).not.toBeNull()
  })

  it('applies h-2 bg-foreground/30 to the track bar', () => {
    render(<Slider aria-label="Volume" />)
    const track = document.querySelector("[data-slot='slider-track']") as HTMLElement
    expect(track.className).toContain('h-2')
    expect(track.className).toContain('bg-foreground/30')
  })

  it('applies h-full bg-primary to the range fill', () => {
    render(<Slider aria-label="Volume" />)
    const range = document.querySelector("[data-slot='slider-range']") as HTMLElement
    expect(range.className).toContain('h-full')
    expect(range.className).toContain('bg-primary')
  })

  it('applies size-3 rounded-md border to the thumb', () => {
    render(<Slider aria-label="Volume" />)
    const thumb = document.querySelector("[data-slot='slider-thumb']") as HTMLElement
    expect(thumb.className).toContain('size-3')
    expect(thumb.className).toContain('rounded-md')
    expect(thumb.className).toContain('border')
  })

  it('nests the thumb INSIDE the track span (regression for thumb-leaks-past-track bug)', () => {
    render(<Slider aria-label="Volume" />)
    const track = document.querySelector("[data-slot='slider-track']") as HTMLElement
    const thumb = track.querySelector("[data-slot='slider-thumb']")
    expect(thumb).not.toBeNull()
    // The thumb must be a descendant of the track span — not a sibling
    // outside it. RAC positions the thumb via the inline `left: %` style
    // applied to the thumb, so the thumb must be inside the same box
    // that defines the percentage reference frame.
  })

  it('keeps the range fill as a sibling of the thumb, both inside the track span', () => {
    render(<Slider aria-label="Volume" />)
    const track = document.querySelector("[data-slot='slider-track']") as HTMLElement
    const range = track.querySelector("[data-slot='slider-range']")
    const thumb = track.querySelector("[data-slot='slider-thumb']")
    expect(range).not.toBeNull()
    expect(thumb).not.toBeNull()
    // Both elements share the track span as their parent — RAC renders
    // the fill behind the thumb in DOM order, so the fill paints first.
    expect(range?.parentElement).toBe(track)
    expect(thumb?.parentElement).toBe(track)
  })

  it('renders one thumb per value in array mode', () => {
    render(<Slider aria-label="Range" defaultValue={[20, 80]} />)
    const thumbs = document.querySelectorAll("[data-slot='slider-thumb']")
    expect(thumbs.length).toBe(2)
  })
})