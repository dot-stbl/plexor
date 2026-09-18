/**
 * Switch — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Switch } from "./switch"

describe("Switch", () => {
  it("renders with data-slot=switch on the label wrapper", () => {
    render(<Switch aria-label="wifi" />)
    const input = screen.getByLabelText("wifi")
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.dataset["slot"]).toBe("switch")
  })

  it("applies default size (h-[16.6px]) on the label wrapper", () => {
    render(<Switch aria-label="wifi" />)
    const input = screen.getByLabelText("wifi")
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.className ?? "").toContain("h-[16.6px]")
  })

  it("applies sm size (h-[14px]) on the label wrapper", () => {
    render(<Switch size="sm" aria-label="wifi" />)
    const input = screen.getByLabelText("wifi")
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.className ?? "").toContain("h-[14px]")
  })

  it("reflects the checked prop on the hidden input", () => {
    render(<Switch checked aria-label="wifi" />)
    expect(screen.getByLabelText("wifi")).toBeChecked()
  })

  it("calls onCheckedChange on click", async () => {
    const user = userEvent.setup()
    const onCheckedChange = vi.fn()
    render(<Switch checked={false} onCheckedChange={onCheckedChange} aria-label="wifi" />)
    await user.click(screen.getByLabelText("wifi"))
    expect(onCheckedChange).toHaveBeenCalledWith(true)
  })

  it("applies data-selected:bg-primary (RAC state attribute)", () => {
    render(<Switch checked aria-label="wifi" />)
    const input = screen.getByLabelText("wifi")
    const label = input.closest("label") as HTMLLabelElement | null
    expect(label?.className ?? "").toContain("data-selected:bg-primary")
  })

  it("applies data-disabled state classes for both cursor and opacity", () => {
    render(<Switch disabled aria-label="wifi" />)
    const input = screen.getByLabelText("wifi")
    const label = input.closest("label") as HTMLLabelElement | null
    const cls = label?.className ?? ""
    expect(cls).toContain("data-disabled:cursor-not-allowed")
    expect(cls).toContain("data-disabled:opacity-50")
  })
})
