/**
 * Select — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import {
  Select,
  SelectContent,
  SelectTrigger,
  SelectValue,
} from "./select"

describe("Select", () => {
  it("renders a trigger button", () => {
    render(
      <Select items={[{ value: "a", label: "A" }, { value: "b", label: "B" }]} value="a" onValueChange={() => {}} aria-label="fruit">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>,
    )
    // RAC's select trigger is rendered as a button with aria-haspopup=listbox.
    const trigger = screen.getByRole("button", { name: /A/i })
    expect(trigger).toBeInTheDocument()
    expect(trigger.getAttribute("aria-haspopup")).toBe("listbox")
  })

  it("Trigger applies the input-like base classes (h-7, rounded-md)", () => {
    render(
      <Select items={[{ value: "a", label: "A" }]} value="a" onValueChange={() => {}} aria-label="fruit">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>,
    )
    const trigger = screen.getByRole("button", { name: /A/i })
    expect(trigger.className).toContain("h-7")
    expect(trigger.className).toContain("rounded-md")
  })

  it("opens the popover and shows items when clicked", async () => {
    const user = userEvent.setup()
    render(
      <Select items={[{ value: "a", label: "Apple" }, { value: "b", label: "Banana" }]} value="a" onValueChange={() => {}} aria-label="fruit">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>,
    )
    await user.click(screen.getByRole("button", { name: /A/i }))
    expect(await screen.findByRole("option", { name: "Apple" })).toBeInTheDocument()
    expect(await screen.findByRole("option", { name: "Banana" })).toBeInTheDocument()
  })

  it("calls onValueChange when an item is selected", async () => {
    const user = userEvent.setup()
    const onValueChange = vi.fn()
    render(
      <Select items={[{ value: "a", label: "Apple" }, { value: "b", label: "Banana" }]} value="a" onValueChange={onValueChange} aria-label="fruit">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>,
    )
    await user.click(screen.getByRole("button", { name: /A/i }))
    await user.click(await screen.findByRole("option", { name: "Banana" }))
    expect(onValueChange).toHaveBeenCalledWith("b")
  })

  it("respects disabled prop on the trigger", () => {
    render(
      <Select items={[{ value: "a", label: "A" }]} value="a" onValueChange={() => {}} disabled aria-label="fruit">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>,
    )
    const trigger = screen.getByRole("button", { name: /A/i })
    expect(trigger).toBeDisabled()
  })
})
