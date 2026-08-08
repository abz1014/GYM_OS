import { Search } from 'lucide-react'

import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

/**
 * The console's list-search box: tall, pill-shaped, card-coloured, with the glyph inside it.
 *
 * The height is not cosmetic — this is the control staff hit most often on every list screen, and
 * the redesign gives it a full 48px target rather than the default input height.
 */
export function SearchField({
  value,
  onChange,
  placeholder,
  className,
  'aria-label': ariaLabel,
}: {
  value: string
  onChange: (value: string) => void
  /** Say what can be typed, not "Search…" — "Name, code, phone or email" tells staff what will match. */
  placeholder: string
  className?: string
  'aria-label'?: string
}) {
  return (
    <div className={cn('relative', className)}>
      <Search className="absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        aria-label={ariaLabel ?? placeholder}
        placeholder={placeholder}
        className="h-12 rounded-2xl bg-card pl-10"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  )
}
