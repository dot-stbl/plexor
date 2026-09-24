/**
 * Avatar — pure-React primitives tests.
 *
 * Covers the three behaviors the consumer relies on:
 *  - derive two-letter initials from `name` when no image is given
 *  - render an `<img>` with `alt = name` when `src` is provided
 *  - merge a consumer-supplied `className` onto the container
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Avatar } from "./avatar"

describe("Avatar", () => {
  it("renders initials when no image src is provided", () => {
    render(<Avatar name="Jane Doe" />)
    expect(screen.getByText("JD")).toBeInTheDocument()
  })

  it("renders image when src is provided", () => {
    render(<Avatar name="Jane Doe" src="https://example.com/jane.jpg" />)
    const img = screen.getByRole("img")
    expect(img).toHaveAttribute("alt", "Jane Doe")
  })

  it("accepts custom className", () => {
    const { container } = render(<Avatar name="JD" className="size-12" />)
    expect(container.firstChild).toHaveClass("size-12")
  })
})