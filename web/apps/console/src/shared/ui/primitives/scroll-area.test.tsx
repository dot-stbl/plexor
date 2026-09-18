/**
 * ScrollArea — react-aria-components migration tests.
 *
 * RAC doesn't ship a ScrollArea primitive; our wrapper uses native browser
 * scrollbars with custom CSS.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { ScrollArea } from "./scroll-area"

describe("ScrollArea", () => {
  it("renders with data-slot=scroll-area", () => {
    render(<ScrollArea data-testid="sa">content</ScrollArea>)
    expect(screen.getByTestId("sa").dataset["slot"]).toBe("scroll-area")
  })

  it("renders the viewport slot inside", () => {
    render(<ScrollArea>content</ScrollArea>)
    expect(document.querySelector("[data-slot='scroll-area-viewport']")).not.toBeNull()
  })

  it("applies overflow-auto base class", () => {
    render(<ScrollArea data-testid="sa">content</ScrollArea>)
    expect(screen.getByTestId("sa").className).toContain("overflow-auto")
  })

  it("merges custom className", () => {
    render(<ScrollArea data-testid="sa" className="h-32">content</ScrollArea>)
    expect(screen.getByTestId("sa").className).toContain("h-32")
  })
})
