import * as React from "react"
import { useLocale } from "react-aria-components"

type Direction = "ltr" | "rtl"

const DirectionContext = React.createContext<Direction | undefined>(undefined)

interface DirectionProviderProps {
  /** Layout direction for the subtree. */
  direction?: Direction
  children?: React.ReactNode
}

/**
 * DirectionProvider — base-ui compat.
 *
 * react-aria-components' I18nProvider derives direction from the LOCALE
 * (isRTL), not an explicit prop. To force a direction we set the `dir`
 * attribute on a wrapper and expose it through our own context; RAC
 * components read direction from their locale, so an RTL locale is the
 * way to flip RAC internals themselves.
 */
function DirectionProvider({
  direction = "ltr",
  children,
  ...props
}: DirectionProviderProps) {
  return (
    <DirectionContext.Provider value={direction}>
      <div dir={direction} data-slot="direction-provider" {...props}>
        {children}
      </div>
    </DirectionContext.Provider>
  )
}

/** Current layout direction: explicit DirectionProvider value, else the RAC locale's direction. */
function useDirection(): Direction {
  const context = React.useContext(DirectionContext)
  const { direction } = useLocale()
  return context ?? direction
}

export { DirectionProvider, useDirection }
