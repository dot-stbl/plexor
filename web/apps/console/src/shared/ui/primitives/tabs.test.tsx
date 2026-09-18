/**
 * Tabs — react-aria-components migration tests.
 */
import { describe, expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"

import { Tabs, TabsContent, TabsList, TabsTrigger } from "./tabs"

describe("Tabs", () => {
  it("renders tabs and content", () => {
    render(
      <Tabs defaultValue="a">
        <TabsList>
          <TabsTrigger value="a">Tab A</TabsTrigger>
          <TabsTrigger value="b">Tab B</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Content A</TabsContent>
        <TabsContent value="b">Content B</TabsContent>
      </Tabs>,
    )
    expect(screen.getByRole("tab", { name: "Tab A" })).toBeInTheDocument()
    expect(screen.getByText("Content A")).toBeInTheDocument()
  })

  it("applies orientation data attribute", () => {
    render(
      <Tabs defaultValue="a" orientation="vertical">
        <TabsList>
          <TabsTrigger value="a">A</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Content A</TabsContent>
      </Tabs>,
    )
    expect(screen.getByRole("tablist").getAttribute("data-orientation")).toBe("vertical")
  })

  it("switches tab on click and calls onValueChange", async () => {
    const user = userEvent.setup()
    const onValueChange = vi.fn()
    render(
      <Tabs value="a" onValueChange={onValueChange}>
        <TabsList>
          <TabsTrigger value="a">Tab A</TabsTrigger>
          <TabsTrigger value="b">Tab B</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Content A</TabsContent>
        <TabsContent value="b">Content B</TabsContent>
      </Tabs>,
    )
    await user.click(screen.getByRole("tab", { name: "Tab B" }))
    expect(onValueChange).toHaveBeenCalledWith("b")
  })

  it("TabsList applies bg-muted by default", () => {
    render(
      <Tabs defaultValue="a">
        <TabsList data-testid="tl">
          <TabsTrigger value="a">A</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Content A</TabsContent>
      </Tabs>,
    )
    expect(screen.getByTestId("tl").className).toContain("bg-muted")
  })

  it("TabsList data-variant=line omits bg-muted", () => {
    render(
      <Tabs defaultValue="a">
        <TabsList variant="line" data-testid="tl">
          <TabsTrigger value="a">A</TabsTrigger>
        </TabsList>
        <TabsContent value="a">Content A</TabsContent>
      </Tabs>,
    )
    const list = screen.getByTestId("tl")
    expect(list.className).not.toContain("bg-muted")
    expect(list.getAttribute("data-variant")).toBe("line")
  })
})
