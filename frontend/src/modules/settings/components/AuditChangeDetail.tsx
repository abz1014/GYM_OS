/**
 * What actually changed, from the audit row's own payload.
 *
 * The audit log answered "who did what, and when" but never "what" in any useful sense: a refund
 * showed as `IssueRefundCommand / Billing / Jane / 14:02` while the amount and the member sat
 * unread in `dataAfter`, which the DTO has always carried. "Who authorised that refund, and for
 * how much" was unanswerable from the one screen built to answer it.
 *
 * Deliberately a summary plus a disclosure rather than a raw JSON dump: the payload is a command
 * object, most of whose fields are ids nobody reads. The summary lifts the handful a human scans
 * for — money, names, reasons, dates — and the disclosure keeps the rest available rather than
 * hidden, because an audit trail that quietly drops fields is worse than one that shows them all.
 */

/** Ids and plumbing — real data, but not what a person is reading the audit log to find. */
const NOISE = /(^|[a-z])(id|ids|guid|tenantid|token|hash|correlationid)$/i

/** Fields worth putting in the one-line summary, in the order a human asks about them. */
const HEADLINE_ORDER = ['amount', 'total', 'totalamount', 'price', 'pricepaid', 'reason',
  'cancellationreason', 'note', 'status', 'name', 'email', 'quantity', 'enabled']

function label(key: string): string {
  return key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/^./, (c) => c.toUpperCase())
}

function readable(value: unknown): string | null {
  if (value === null || value === undefined) return null
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (typeof value === 'number') return String(value)
  if (typeof value === 'string') {
    if (value.trim() === '') return null
    // ISO timestamps are unreadable in a table; a date is what the reader wants.
    const asDate = /^\d{4}-\d{2}-\d{2}(T|$)/.test(value) ? new Date(value) : null
    if (asDate && !Number.isNaN(asDate.valueOf())) return asDate.toLocaleDateString()
    return value.length > 80 ? `${value.slice(0, 80)}…` : value
  }
  // Arrays and nested objects: say how much there is rather than pretending to render it.
  if (Array.isArray(value)) return value.length === 0 ? null : `${value.length} item(s)`
  return null
}

interface Field {
  key: string
  label: string
  value: string
}

function parseFields(dataAfter: string | null): Field[] {
  if (!dataAfter) return []
  let parsed: unknown
  try {
    parsed = JSON.parse(dataAfter)
  } catch {
    // A payload we cannot parse is still evidence — show it verbatim rather than dropping it.
    return [{ key: 'raw', label: 'Payload', value: dataAfter.slice(0, 200) }]
  }
  if (typeof parsed !== 'object' || parsed === null) return []

  return Object.entries(parsed as Record<string, unknown>)
    .filter(([k]) => !NOISE.test(k))
    .map(([k, v]) => ({ key: k, label: label(k), value: readable(v) }))
    .filter((f): f is Field => f.value !== null)
}

/** The one line shown inline in the table — the fields a person scans for, or nothing. */
export function AuditChangeSummary({ dataAfter }: { dataAfter: string | null }) {
  const fields = parseFields(dataAfter)
  if (fields.length === 0) {
    // Honest blank: some commands genuinely carry no payload worth showing.
    return <span className="text-muted-foreground">—</span>
  }

  const rank = (f: Field) => {
    const i = HEADLINE_ORDER.indexOf(f.key.toLowerCase())
    return i === -1 ? HEADLINE_ORDER.length : i
  }
  const headline = [...fields].sort((a, b) => rank(a) - rank(b)).slice(0, 2)

  return (
    <details className="group">
      <summary className="cursor-pointer list-none text-sm marker:content-['']">
        <span className="text-foreground">
          {headline.map((f) => `${f.label}: ${f.value}`).join(' · ')}
        </span>
        {fields.length > headline.length && (
          <span className="ml-1 text-xs text-muted-foreground group-open:hidden">
            +{fields.length - headline.length} more
          </span>
        )}
      </summary>
      <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 border-l-2 border-border pl-3 text-xs">
        {fields.map((f) => (
          <div key={f.key} className="contents">
            <dt className="text-muted-foreground">{f.label}</dt>
            <dd className="break-words">{f.value}</dd>
          </div>
        ))}
      </dl>
    </details>
  )
}
