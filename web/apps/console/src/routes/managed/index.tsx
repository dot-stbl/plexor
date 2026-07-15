import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/managed/')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/managed/"!</div>
}
