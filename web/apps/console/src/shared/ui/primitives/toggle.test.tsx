/**
 * Toggle — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Toggle } from "./toggle"

describe("Toggle", () => {
  it("renders with data-slot=toggle", () => {
    render(<Toggle aria-label="bold">B</Toggle>)
    expect(screen.getByRole("button", { name: "bold" })).toBeInTheDocument()
  })

  it("applies h-7 min-w-7 base classes", () => {
    render(<Toggle aria-label="bold">B</Toggle>)
    expect(screen.getByRole("button", { name: "bold" }).className).toContain("h-7")
  })

  it("reflects pressed prop", () => {
    render(<Toggle pressed aria-label="bold">B</Toggle>)
    expect(screen.getByRole("button", { name: "bold" }).getAttribute("aria-pressed")).toBe("true")
  })

  it("calls onPressedChange on click", async () => {
    const user = userEvent.setup()
    const onPressedChange = vi.fn()
    render(<Toggle pressed={false} onPressedChange={onPressedChange} aria-label="bold">B</Toggle>)
    await user.click(screen.getByRole("button", { name: "bold" }))
    expect(onPressedChange).toHaveBeenCalledWith(true)
  })

  it("merges custom className with variant classes", () => {
    render(<Toggle variant="outline" className="custom" aria-label="bold">B</Toggle>)
    expect(screen.getByRole("button", { name: "bold" }).className).toContain("custom")
    expect(screen.getByRole("button", { name: "bold" }).className).toContain("border")
  })
})
