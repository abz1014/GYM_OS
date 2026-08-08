import { useEffect, useState } from 'react'

/**
 * Owns the ⌘K / Ctrl-K binding in one place so no screen has to know the palette exists.
 *
 * The guard skips the shortcut while the person is typing into a field, because this app has a
 * front-desk search box and several dialogs where an intercepted keystroke would be worse than no
 * shortcut at all.
 */
export function useCommandPalette() {
  const [open, setOpen] = useState(false)

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() !== 'k' || !(event.metaKey || event.ctrlKey)) {
        return
      }
      const target = event.target as HTMLElement | null
      if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) {
        return
      }
      event.preventDefault()
      setOpen((o) => !o)
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return { open, setOpen }
}
