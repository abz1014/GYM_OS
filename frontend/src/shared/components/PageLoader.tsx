import { Loader2 } from 'lucide-react'

export function PageLoader() {
  return (
    <div className="flex h-full min-h-40 w-full items-center justify-center">
      <Loader2 className="size-6 animate-spin text-muted-foreground" />
    </div>
  )
}
