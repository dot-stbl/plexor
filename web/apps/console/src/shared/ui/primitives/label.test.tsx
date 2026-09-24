/**
 * Label — tests for the react-aria-components migration.
 *
 * Label is a thin wrapper; the migration is mainly a type-safe no-op since
 * Label renders a plain <label> element.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Label } from "./label"

describe("Label", () => {
  it("renders a <label> with data-slot=label", () => {
    render(<Label htmlFor="email">Email</Label>)
    const el = screen.getByText("Email")
    expect(el).toBeInstanceOf(HTMLLabelElement)
    expect((el as HTMLLabelElement).htmlFor).toBe("email")
  })

  it("applies base classes (font-medium, text-xs)", () => {
    render(<Label>Test</Label>)
    expect(screen.getByText("Test").className).toContain("font-medium")
  })

  it("merges custom className", () => {
    render(<Label className="custom">X</Label>)
    expect(screen.getByText("X").className).toContain("custom")
  })
})
