/**
 * Tooltip — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "./tooltip"

describe("Tooltip", () => {
  it("TooltipProvider renders its children without error", () => {
    render(
      <TooltipProvider delay={100}>
        <span>child</span>
      </TooltipProvider>,
    )
    expect(screen.getByText("child")).toBeInTheDocument()
  })

  it("TooltipTrigger renders the trigger element", () => {
    render(
      <Tooltip>
        <TooltipTrigger>
          <button type="button">Hover me</button>
        </TooltipTrigger>
        <TooltipContent>Tip</TooltipContent>
      </Tooltip>,
    )
    expect(screen.getByRole("button", { name: "Hover me" })).toBeInTheDocument()
  })

  it("TooltipContent applies bg-foreground and rounded-md base classes when shown via open", () => {
    render(
      <Tooltip open>
        <TooltipTrigger>
          <button type="button">Trigger</button>
        </TooltipTrigger>
        <TooltipContent data-testid="tt">Tip body</TooltipContent>
      </Tooltip>,
    )
    const content = screen.getByTestId("tt")
    expect(content.className).toContain("bg-foreground")
    expect(content.className).toContain("rounded-md")
  })

  it("TooltipContent applies z-50 base classes", () => {
    render(
      <Tooltip open>
        <TooltipTrigger>
          <button type="button">Trigger</button>
        </TooltipTrigger>
        <TooltipContent data-testid="tt">Tip</TooltipContent>
      </Tooltip>,
    )
    const content = screen.getByTestId("tt")
    expect(content.className).toContain("z-50")
  })
})
