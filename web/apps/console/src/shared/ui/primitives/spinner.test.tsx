import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Spinner } from "./spinner"

describe("Spinner", () => {
  it("renders an SVG with role=status and aria-label", () => {
    render(<Spinner data-testid="sp" />)
    const el = screen.getByTestId("sp")
    expect(el.tagName.toLowerCase()).toBe("svg")
    expect(el.getAttribute("role")).toBe("status")
    expect(el.getAttribute("aria-label")).toBe("Loading")
  })

  it("applies size-4 and animate-spin base classes", () => {
    render(<Spinner data-testid="sp" />)
    const el = screen.getByTestId("sp")
    expect(el.getAttribute("class") ?? "").toContain("size-4")
  })

  it("merges custom className", () => {
    render(<Spinner data-testid="sp" className="size-8" />)
    const el = screen.getByTestId("sp")
    expect(el.getAttribute("class") ?? "").toContain("size-8")
  })
})
