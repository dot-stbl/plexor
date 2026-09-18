/**
 * Dialog — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from "./dialog"

describe("Dialog", () => {
  it("renders content when open", () => {
    render(
      <Dialog open onOpenChange={() => {}}>
        <DialogContent>
          <DialogTitle>Hello</DialogTitle>
          <DialogDescription>World</DialogDescription>
        </DialogContent>
      </Dialog>,
    )
    expect(screen.getByText("Hello")).toBeInTheDocument()
    expect(screen.getByText("World")).toBeInTheDocument()
  })

  it("DialogContent applies bg-popover + rounded-xl classes", () => {
    render(
      <Dialog open onOpenChange={() => {}}>
        <DialogContent data-testid="dlg-content">
          <DialogTitle>X</DialogTitle>
        </DialogContent>
      </Dialog>,
    )
    const content = screen.getByTestId("dlg-content")
    expect(content.className).toContain("bg-popover")
    expect(content.className).toContain("rounded-xl")
  })

  it("renders close button when showCloseButton=true", () => {
    render(
      <Dialog open onOpenChange={() => {}}>
        <DialogContent showCloseButton>
          <DialogTitle>T</DialogTitle>
          <DialogDescription>D</DialogDescription>
        </DialogContent>
      </Dialog>,
    )
    // The close button uses sr-only text "Close"
    const closeBtn = document.querySelector("[data-slot='button']") as HTMLElement | null
    expect(closeBtn).not.toBeNull()
    expect(closeBtn?.getAttribute("slot")).toBe("close")
  })

  it("DialogTrigger renders the trigger element", () => {
    render(
      <DialogTrigger>
        <button type="button">Open</button>
        <DialogContent>
          <DialogTitle>T</DialogTitle>
        </DialogContent>
      </DialogTrigger>,
    )
    expect(screen.getByRole("button", { name: "Open" })).toBeInTheDocument()
  })

  it("DialogTitle applies font-heading class", () => {
    render(
      <Dialog open onOpenChange={() => {}}>
        <DialogContent>
          <DialogTitle data-testid="dlg-title">Title</DialogTitle>
        </DialogContent>
      </Dialog>,
    )
    expect(screen.getByTestId("dlg-title").className).toContain("font-heading")
  })
})
