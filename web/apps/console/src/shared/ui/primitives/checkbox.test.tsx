/**
 * Checkbox — react-aria-components migration tests.
 *
 * RAC renders the visual Checkbox as a <label data-slot="checkbox"> with
 * a hidden <input type="checkbox"> inside. We assert against the label
 * (the visual surface) and against the hidden input for checked state.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Checkbox } from "./checkbox"

describe("Checkbox", () => {
  it("renders with data-slot=checkbox on the label wrapper", () => {
    render(<Checkbox aria-label="accept" />)
    const input = screen.getByLabelText("accept")
    // RAC renders the Checkbox as <label data-slot="checkbox">…<input/></label>
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.dataset["slot"]).toBe("checkbox")
  })

  it("applies base classes (size-4, rounded-[4px]) on the label wrapper", () => {
    render(<Checkbox aria-label="accept" />)
    const input = screen.getByLabelText("accept")
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.className ?? "").toContain("size-4")
    expect(label?.className ?? "").toContain("rounded-[4px]")
  })

  it("reflects the checked prop on the hidden input", () => {
    render(<Checkbox checked aria-label="accept" />)
    expect(screen.getByLabelText("accept")).toBeChecked()
  })

  it("calls onCheckedChange on click", async () => {
    const user = userEvent.setup()
    const onCheckedChange = vi.fn()
    render(<Checkbox checked={false} onCheckedChange={onCheckedChange} aria-label="accept" />)
    await user.click(screen.getByLabelText("accept"))
    expect(onCheckedChange).toHaveBeenCalledWith(true)
  })

  it("respects disabled", async () => {
    const user = userEvent.setup()
    const onCheckedChange = vi.fn()
    render(<Checkbox disabled onCheckedChange={onCheckedChange} aria-label="accept" />)
    await user.click(screen.getByLabelText("accept"))
    expect(onCheckedChange).not.toHaveBeenCalled()
  })

  it("darkens border on hover so the box feels interactive (micro-interaction)", () => {
    render(<Checkbox aria-label="accept" />)
    const input = screen.getByLabelText("accept")
    const label = input.closest("label") as HTMLLabelElement | null
    const cls = label?.className ?? ""
    // transition-all (not transition-shadow) so hover:border-foreground/40 animates
    expect(cls).toContain("transition-all")
    expect(cls).toContain("hover:border-foreground/40")
  })
})
