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

  it("TooltipTrigger render + children puts children inside the rendered element (base-ui semantics)", () => {
    render(
      <Tooltip>
        <TooltipTrigger render={<button type="button" aria-label="Help" />}>
          <span data-testid="trigger-icon">?</span>
        </TooltipTrigger>
        <TooltipContent>Tip</TooltipContent>
      </Tooltip>,
    )
    const trigger = screen.getByRole("button", { name: "Help" })
    expect(trigger).toContainElement(screen.getByTestId("trigger-icon"))
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

  it("TooltipContent applies slide-from-X classes for each side", () => {
    render(
      <Tooltip open>
        <TooltipTrigger>
          <button type="button">Trigger</button>
        </TooltipTrigger>
        <TooltipContent data-testid="tt">Tip</TooltipContent>
      </Tooltip>,
    )
    const cls = screen.getByTestId("tt").className
    expect(cls).toContain("data-[side=bottom]:slide-in-from-top-2")
    expect(cls).toContain("data-[side=left]:slide-in-from-right-2")
    expect(cls).toContain("data-[side=right]:slide-in-from-left-2")
    expect(cls).toContain("data-[side=top]:slide-in-from-bottom-2")
    expect(cls).toContain("data-[side=inline-end]:slide-in-from-left-2")
    expect(cls).toContain("data-[side=inline-start]:slide-in-from-right-2")
  })

  it("TooltipContent applies kbd child styling for slot=kbd", () => {
    render(
      <Tooltip open>
        <TooltipTrigger>
          <button type="button">Trigger</button>
        </TooltipTrigger>
        <TooltipContent data-testid="tt">
          <span data-slot="kbd">⌘K</span>
        </TooltipContent>
      </Tooltip>,
    )
    const cls = screen.getByTestId("tt").className
    expect(cls).toContain("**:data-[slot=kbd]:relative")
    expect(cls).toContain("**:data-[slot=kbd]:z-50")
    expect(cls).toContain("**:data-[slot=kbd]:rounded-sm")
  })
})
