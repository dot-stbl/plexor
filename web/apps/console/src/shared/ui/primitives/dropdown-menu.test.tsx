/**
 * DropdownMenu — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "./dropdown-menu"

describe("DropdownMenu", () => {
  it("renders the trigger element", () => {
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    expect(screen.getByRole("button", { name: "Open" })).toBeInTheDocument()
  })

  it("opens the menu on trigger click", async () => {
    const user = userEvent.setup()
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>First</DropdownMenuItem>
          <DropdownMenuItem>Second</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    await user.click(screen.getByRole("button", { name: "Open" }))
    expect(await screen.findByText("First")).toBeInTheDocument()
    expect(await screen.findByText("Second")).toBeInTheDocument()
  })

  it("calls onClick when an item is activated", async () => {
    const user = userEvent.setup()
    const onClick = vi.fn()
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem onClick={onClick}>First</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    await user.click(screen.getByRole("button", { name: "Open" }))
    await user.click(await screen.findByText("First"))
    expect(onClick).toHaveBeenCalled()
  })

  it("DropdownMenuSeparator applies h-px bg-border/50", async () => {
    const user = userEvent.setup()
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuSeparator data-testid="sep" />
          <DropdownMenuItem>Below</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    await user.click(screen.getByRole("button", { name: "Open" }))
    const sep = await screen.findByTestId("sep")
    expect(sep.className).toContain("h-px")
    expect(sep.className).toContain("bg-border/50")
  })

  it("DropdownMenuLabel applies px-2 py-1.5", async () => {
    const user = userEvent.setup()
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuLabel data-testid="lbl">Section</DropdownMenuLabel>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    await user.click(screen.getByRole("button", { name: "Open" }))
    const lbl = await screen.findByTestId("lbl")
    expect(lbl.className).toContain("px-2")
    expect(lbl.className).toContain("py-1.5")
  })

  it("DropdownMenuItem applies min-h-7 base classes", async () => {
    const user = userEvent.setup()
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>
          <button type="button">Open</button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem data-testid="it">X</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    )
    await user.click(screen.getByRole("button", { name: "Open" }))
    const it = await screen.findByTestId("it")
    expect(it.className).toContain("min-h-7")
    expect(it.className).toContain("rounded-md")
  })
})
