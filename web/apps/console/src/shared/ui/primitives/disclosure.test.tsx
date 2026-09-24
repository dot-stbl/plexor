/**
 * Disclosure — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Disclosure } from "./disclosure"

describe("Disclosure", () => {
  it("renders the summary text", () => {
    render(<Disclosure summary="Advanced">content</Disclosure>)
    expect(screen.getByText("Advanced")).toBeInTheDocument()
  })

  it("starts collapsed by default", () => {
    render(<Disclosure summary="S">body</Disclosure>)
    expect(screen.queryByText("body")).not.toBeVisible()
  })

  it("starts open when defaultOpen=true", () => {
    render(<Disclosure summary="S" defaultOpen>body</Disclosure>)
    expect(screen.getByText("body")).toBeVisible()
  })

  it("toggles content on click of the trigger (inline variant)", async () => {
    const user = userEvent.setup()
    render(<Disclosure summary="S">body</Disclosure>)
    await user.click(screen.getByRole("button", { name: /S/ }))
    expect(screen.getByText("body")).toBeVisible()
  })

  it("card variant renders with border + rounded-md classes", () => {
    const { container } = render(
      <Disclosure summary="S" variant="card">
        body
      </Disclosure>,
    )
    const root = container.querySelector("[data-slot='collapsible']") as HTMLElement | null
    expect(root).not.toBeNull()
    expect(root?.className ?? "").toContain("rounded-md")
    expect(root?.className ?? "").toContain("border")
  })
})

describe("Disclosure — caret micro-interactions", () => {
  it("caret nudges right on hover so the trigger feels responsive", () => {
    const { container } = render(
      <Disclosure summary="S" defaultOpen>
        body
      </Disclosure>,
    )
    // Caret is the only <svg> in the trigger row.
    const caret = container.querySelector("svg")
    expect(caret).not.toBeNull()
    // jsdom: SVGSVGElement.className is an SVGAnimatedString; the live
    // string lives on .baseVal. Use getAttribute("class") for plain
    // string compare (toContain) without reaching into baseVal.
    const cls = caret?.getAttribute("class") ?? ""
    expect(cls).toContain("transition-transform")
    expect(cls).toContain("duration-200")
    expect(cls).toContain("group-hover/disclosure:translate-x-0.5")
  })

  it("inline trigger carries the group/disclosure hook so caret hover fires", () => {
    render(<Disclosure summary="S">body</Disclosure>)
    const trigger = screen.getByRole("button", { name: /S/ })
    expect(trigger.className).toContain("group/disclosure")
  })

  it("card variant trigger also carries the group/disclosure hook", () => {
    const { container } = render(
      <Disclosure summary="S" variant="card">
        body
      </Disclosure>,
    )
    const trigger = container.querySelector("[data-slot='collapsible-trigger']")
    expect(trigger?.className ?? "").toContain("group/disclosure")
  })
})
