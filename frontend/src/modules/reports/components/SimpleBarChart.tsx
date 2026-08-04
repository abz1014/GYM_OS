interface SimpleBarChartProps {
  data: { label: string; value: number }[]
  valueFormatter?: (value: number) => string
}

export function SimpleBarChart({ data, valueFormatter = (v) => String(v) }: SimpleBarChartProps) {
  const max = Math.max(1, ...data.map((d) => d.value))
  // With many bars, a label under every single one just clips to unreadable fragments (e.g.
  // "07-..." for 30 daily buckets) — show at most ~8 evenly-spaced labels instead, same as a
  // real charting library would thin out axis ticks, rather than truncating all of them.
  const labelStride = Math.max(1, Math.ceil(data.length / 8))

  return (
    <div className="flex h-48 items-stretch gap-1.5 overflow-x-auto overflow-y-hidden pb-1">
      {data.map((d, i) => (
        <div key={i} className="group grid min-w-[1.5rem] flex-1 grid-rows-[1fr_auto] justify-items-center" title={`${d.label}: ${valueFormatter(d.value)}`}>
          <div className="flex w-full items-end">
            <div
              className="w-full rounded-t bg-primary transition-all group-hover:bg-primary/80"
              style={{ height: `${Math.max(2, (d.value / max) * 100)}%` }}
            />
          </div>
          <span className="relative mt-1 h-3 w-full">
            {i % labelStride === 0 && (
              <span className="absolute top-0 left-1/2 -translate-x-1/2 text-center text-[10px] whitespace-nowrap text-muted-foreground">
                {d.label}
              </span>
            )}
          </span>
        </div>
      ))}
    </div>
  )
}
