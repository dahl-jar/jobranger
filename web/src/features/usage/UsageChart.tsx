import styles from '@/features/usage/usage.module.css'
import type { UsageBar } from '@/features/usage/usageSummary'

interface UsageChartProps {
  bars: readonly UsageBar[]
  label: string
  peakTokens: string
  firstDay: string
}

export function UsageChart({ bars, label, peakTokens, firstDay }: UsageChartProps) {
  return (
    <>
      <div className={styles.chart}>
        <div className={styles.peakLine} aria-hidden="true" />
        <span className={styles.peak} aria-hidden="true">
          {peakTokens}
        </span>
        <div role="group" aria-label={label} className={styles.bars}>
          {bars.map((bar) => (
            <div key={bar.key} role="img" tabIndex={0} aria-label={bar.tip} className={styles.bar}>
              <div className={bar.isToday ? styles.todayFill : styles.barFill} style={{ height: bar.height }} />
              <span className={styles.tip} aria-hidden="true">
                <span className={styles.tipDay}>{bar.day}</span>
                <span className={styles.tipRow}>
                  <span className={styles.tipLabel}>Tokens</span>
                  <span>{bar.tokens}</span>
                </span>
                <span className={styles.tipRow}>
                  <span className={styles.tipLabel}>Cost</span>
                  <span className={styles.tipCost}>{bar.usd}</span>
                </span>
              </span>
            </div>
          ))}
        </div>
      </div>
      <div className={styles.axis} aria-hidden="true">
        <span>{firstDay}</span>
        <span>Today</span>
      </div>
    </>
  )
}
