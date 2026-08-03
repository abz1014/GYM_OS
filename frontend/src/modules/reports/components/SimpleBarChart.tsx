interface SimpleBarChartProps {
  data: { label: string; value: number }[]
  valueFormatter?: (value: number) => string
}

export function SimpleBarChart({ data, valueFormatter = (v) => String(v) }: SimpleBarChartProps) {
  const max = Math.max(1, ...data.map((d) => d.value))

  return (
    <div className="flex h-48 items-end gap-1.5 overflow-x-auto pb-1">
      {data.map((d, i) => (
        <div key={i} className="group flex min-w-[1.5rem] flex-1 flex-col items-center gap-1" title={`${d.label}: ${valueFormatter(d.value)}`}>
          <div className="flex h-40 w-full items-end">
            <div
              className="w-full rounded-t bg-primary transition-all group-hover:bg-primary/80"
              style={{ height: `${Math.max(2, (d.value / max) * 100)}%` }}
            />
          </div>
          <span className="w-full truncate text-center text-[10px] text-muted-foreground">{d.label}</span>
        </div>
      ))}
    </div>
  )
}
