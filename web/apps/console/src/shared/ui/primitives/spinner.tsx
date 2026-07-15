import { cn } from "@/lib/utils"
import { ProgressActivity } from '@nine-thirty-five/material-symbols-react/rounded/700';function Spinner({ className, ...props }: React.ComponentProps<"svg">) {
  return (
    <ProgressActivity strokeWidth={2} data-slot="spinner" role="status" aria-label="Loading" className={cn("size-4 animate-spin", className)} {...props}  />
  )
}

export { Spinner }
