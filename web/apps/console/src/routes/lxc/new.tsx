import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/lxc/new')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/lxc/new"!</div>
}
