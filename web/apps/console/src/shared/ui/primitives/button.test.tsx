/**
 * Button — react-aria-components migration tests.
 *
 * Covers:
 * - renders as a real <button>
 * - applies CVA variant classes (default, outline, destructive)
 * - applies size classes
 * - forwards onClick handler
 * - disabled prop maps to isDisabled
 * - className passthrough
 * - data-slot is set
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Button } from "./button"

describe("Button", () => {
  it("renders as a <button> with data-slot=button", () => {
    render(<Button>Click me</Button>)
    const btn = screen.getByRole("button", { name: "Click me" })
    expect(btn).toBeInstanceOf(HTMLButtonElement)
    expect(btn.dataset["slot"]).toBe("button")
  })

  it("applies default variant classes (bg-primary)", () => {
    render(<Button>Default</Button>)
    const btn = screen.getByRole("button")
    expect(btn.className).toContain("bg-primary")
    expect(btn.className).toContain("text-primary-foreground")
  })

  it("applies outline variant classes (border-border)", () => {
    render(<Button variant="outline">Outline</Button>)
    const btn = screen.getByRole("button")
    expect(btn.className).toContain("border-border")
    expect(btn.className).not.toContain("bg-primary")
  })

  it("applies destructive variant classes (text-destructive)", () => {
    render(<Button variant="destructive">Delete</Button>)
    const btn = screen.getByRole("button")
    expect(btn.className).toContain("text-destructive")
  })

  it("applies size classes (icon-sm = size-6)", () => {
    render(<Button size="icon-sm">Icon</Button>)
    const btn = screen.getByRole("button")
    expect(btn.className).toContain("size-6")
  })

  it("calls onClick when activated", async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Click</Button>)
    await user.click(screen.getByRole("button"))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it("respects the disabled prop (no click handler fires)", async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    render(<Button disabled onClick={onClick}>Disabled</Button>)
    const btn = screen.getByRole("button")
    expect(btn).toBeDisabled()
    await user.click(btn)
    expect(onClick).not.toHaveBeenCalled()
  })

  it("merges custom className with variant classes", () => {
    render(<Button className="custom-class">X</Button>)
    expect(screen.getByRole("button").className).toContain("custom-class")
    expect(screen.getByRole("button").className).toContain("bg-primary")
  })
})
