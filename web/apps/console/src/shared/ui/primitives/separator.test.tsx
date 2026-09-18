/**
 * Separator — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Separator } from "./separator"

describe("Separator", () => {
  it("renders with data-slot=separator", () => {
    render(<Separator data-testid="s" />)
    expect(screen.getByTestId("s").dataset["slot"]).toBe("separator")
  })

  it("horizontal separator has h-px w-full classes", () => {
    render(<Separator data-testid="s" />)
    expect(screen.getByTestId("s").className).toContain("h-px")
    expect(screen.getByTestId("s").className).toContain("w-full")
  })

  it("vertical separator has w-px self-stretch classes", () => {
    render(<Separator orientation="vertical" data-testid="s" />)
    expect(screen.getByTestId("s").className).toContain("w-px")
    expect(screen.getByTestId("s").className).toContain("self-stretch")
  })

  it("merges custom className", () => {
    render(<Separator className="custom-class" data-testid="s" />)
    expect(screen.getByTestId("s").className).toContain("custom-class")
  })
})
