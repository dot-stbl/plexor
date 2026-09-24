/**
 * Progress — react-aria-components migration tests.
 */
import { describe, expect, it } from "vitest"
import { render, screen } from "@testing-library/react"

import { Progress, ProgressIndicator, ProgressTrack } from "./progress"

describe("Progress", () => {
  it("renders with data-slot=progress", () => {
    render(<Progress data-testid="p" aria-label="Loading" />)
    expect(screen.getByTestId("p").dataset["slot"]).toBe("progress")
  })

  it("renders the track and indicator slots", () => {
    render(<Progress aria-label="Loading" />)
    expect(document.querySelector("[data-slot='progress-track']")).not.toBeNull()
    expect(document.querySelector("[data-slot='progress-indicator']")).not.toBeNull()
  })

  it("applies h-1 bg-muted to the track", () => {
    render(<Progress aria-label="Loading" />)
    const track = document.querySelector("[data-slot='progress-track']") as HTMLElement
    expect(track.className).toContain("h-1")
    expect(track.className).toContain("bg-muted")
  })

  it("applies h-full bg-primary to the indicator", () => {
    render(<Progress aria-label="Loading" />)
    const indicator = document.querySelector("[data-slot='progress-indicator']") as HTMLElement
    expect(indicator.className).toContain("h-full")
    expect(indicator.className).toContain("bg-primary")
  })

  it("accepts custom ProgressTrack / ProgressIndicator children", () => {
    render(
      <Progress aria-label="Loading">
        <ProgressTrack data-testid="custom-track" />
        <ProgressIndicator data-testid="custom-indicator" />
      </Progress>,
    )
    expect(screen.getByTestId("custom-track").dataset["slot"]).toBe("progress-track")
    expect(screen.getByTestId("custom-indicator").dataset["slot"]).toBe("progress-indicator")
  })
})
