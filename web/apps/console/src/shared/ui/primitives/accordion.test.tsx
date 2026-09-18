/**
 * Accordion — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "./accordion"

describe("Accordion", () => {
  it("renders items and triggers", () => {
    render(
      <Accordion>
        <AccordionItem value="a">
          <AccordionTrigger>A</AccordionTrigger>
          <AccordionContent>Content A</AccordionContent>
        </AccordionItem>
      </Accordion>,
    )
    expect(screen.getByRole("button", { name: /A/ })).toBeInTheDocument()
  })

  it("expands and collapses on trigger click", async () => {
    const user = userEvent.setup()
    render(
      <Accordion>
        <AccordionItem value="a">
          <AccordionTrigger>A</AccordionTrigger>
          <AccordionContent>Content A</AccordionContent>
        </AccordionItem>
      </Accordion>,
    )
    // The Button has aria-expanded reflecting the disclosure state
    const trigger = screen.getByRole("button", { name: /A/ })
    expect(trigger.getAttribute("aria-expanded")).toBe("false")
    await user.click(trigger)
    expect(trigger.getAttribute("aria-expanded")).toBe("true")
  })

  it("applies border + rounded-md base classes to root", () => {
    const { container } = render(
      <Accordion>
        <AccordionItem value="a">
          <AccordionTrigger>A</AccordionTrigger>
          <AccordionContent>Content A</AccordionContent>
        </AccordionItem>
      </Accordion>,
    )
    const root = container.querySelector("[data-slot='accordion']") as HTMLElement | null
    expect(root).not.toBeNull()
    expect(root?.className ?? "").toContain("rounded-md")
    expect(root?.className ?? "").toContain("border")
  })

  it("applies not-last:border-b to items", () => {
    render(
      <Accordion>
        <AccordionItem value="a" data-testid="ai">
          <AccordionTrigger>A</AccordionTrigger>
          <AccordionContent>Content A</AccordionContent>
        </AccordionItem>
        <AccordionItem value="b" data-testid="ai2">
          <AccordionTrigger>B</AccordionTrigger>
          <AccordionContent>Content B</AccordionContent>
        </AccordionItem>
      </Accordion>,
    )
    expect(screen.getByTestId("ai").className).toContain("not-last:border-b")
  })

  it("calls onValueChange on toggle", async () => {
    const user = userEvent.setup()
    const onValueChange = vi.fn()
    render(
      <Accordion onValueChange={onValueChange}>
        <AccordionItem value="a">
          <AccordionTrigger>A</AccordionTrigger>
          <AccordionContent>Content A</AccordionContent>
        </AccordionItem>
      </Accordion>,
    )
    await user.click(screen.getByRole("button", { name: /A/ }))
    expect(onValueChange).toHaveBeenCalled()
  })
})
