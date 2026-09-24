/**
 * Textarea — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Textarea } from "./textarea"

describe("Textarea", () => {
  it("renders a <textarea> with data-slot=textarea", () => {
    render(<Textarea placeholder="notes" />)
    const el = screen.getByPlaceholderText("notes")
    expect(el).toBeInstanceOf(HTMLTextAreaElement)
    expect(el.dataset["slot"]).toBe("textarea")
  })

  it("applies the textarea classes (min-h-16)", () => {
    render(<Textarea placeholder="x" />)
    expect(screen.getByPlaceholderText("x").className).toContain("min-h-16")
  })

  it("respects disabled", () => {
    render(<Textarea disabled placeholder="x" />)
    expect(screen.getByPlaceholderText("x")).toBeDisabled()
  })
})
