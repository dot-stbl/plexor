/**
 * Input — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Input } from "./input"

describe("Input", () => {
  it("renders as a real <input> with data-slot=input", () => {
    render(<Input placeholder="name" />)
    const input = screen.getByPlaceholderText("name")
    expect(input).toBeInstanceOf(HTMLInputElement)
    expect(input.dataset["slot"]).toBe("input")
  })

  it("applies the input base classes (h-7, rounded-md)", () => {
    render(<Input placeholder="x" />)
    const input = screen.getByPlaceholderText("x")
    expect(input.className).toContain("h-7")
    expect(input.className).toContain("rounded-md")
  })

  it("forwards type prop", () => {
    render(<Input type="password" placeholder="secret" />)
    const input = screen.getByPlaceholderText("secret") as HTMLInputElement
    expect(input.type).toBe("password")
  })

  it("calls onChange when typed into", async () => {
    const user = userEvent.setup()
    let value = ""
    render(<Input placeholder="x" onChange={(e) => { value = e.target.value }} />)
    await user.type(screen.getByPlaceholderText("x"), "hello")
    expect(value).toBe("hello")
  })

  it("respects disabled", () => {
    render(<Input disabled placeholder="x" />)
    expect(screen.getByPlaceholderText("x")).toBeDisabled()
  })
})
