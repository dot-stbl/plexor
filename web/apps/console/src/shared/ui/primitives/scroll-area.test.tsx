/**
 * ScrollArea — react-aria-components migration tests.
 *
 * RAC doesn't ship a ScrollArea primitive; our wrapper uses native browser
 * scrollbars with custom CSS. The `variant` prop switches between the
 * subtle default and a themed rail used by the launcher.
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

  it("tags the host with plexor-scroll-area-host so the inline scrollbar CSS targets it", () => {
    render(<ScrollArea data-testid="sa">content</ScrollArea>)
    expect(screen.getByTestId("sa").className).toContain("plexor-scroll-area-host")
  })

  it("defaults to data-bar-variant=default", () => {
    render(<ScrollArea data-testid="sa">content</ScrollArea>)
    expect(screen.getByTestId("sa").dataset["barVariant"]).toBe("default")
  })

  it("applies the themed variant class + data attr when variant=\"themed\"", () => {
    render(<ScrollArea data-testid="sa" variant="themed">content</ScrollArea>)
    const host = screen.getByTestId("sa")
    expect(host.className).toContain("plexor-scroll-area-themed")
    expect(host.dataset["barVariant"]).toBe("themed")
  })
})
