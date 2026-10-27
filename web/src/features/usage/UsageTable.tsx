import styles from '@/features/usage/usage.module.css'
import { recentUsage, TABLE_DAYS, usageTotals } from '@/features/usage/usageSummary'
import type { Day, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { SectionLabel } from '@/shared/ui/SectionLabel'

export function UsageTable({ usage, today }: { usage: UsageSnapshot; today: Day }) {
  const totals = usageTotals(usage)
  const recent = recentUsage(usage, today)
  return (
    <section>
      <SectionLabel>Usage</SectionLabel>
      <dl className={styles.totals}>
        <div className={styles.total}>
          <dt>This month · {totals.monthTokens} tokens</dt>
          <dd>{totals.monthUsd}</dd>
        </div>
        <div className={styles.total}>
          <dt>Today · {totals.todayTokens} tokens</dt>
          <dd>{totals.todayUsd}</dd>
        </div>
      </dl>
      {recent.length === 0 ? (
        <p className={styles.empty}>No runs in the last {TABLE_DAYS} days.</p>
      ) : (
        <div role="region" aria-label="Usage per day" tabIndex={0} className={styles.tableScroll}>
          <table role="table" className={styles.table}>
            <thead role="rowgroup">
              <tr role="row">
                <th role="columnheader" scope="col" className={styles.day}>
                  Day
                </th>
                <th role="columnheader" scope="col" className={styles.read}>
                  Ads read
                </th>
                <th role="columnheader" scope="col" className={styles.checked}>
                  Checked by AI
                </th>
                <th role="columnheader" scope="col" className={styles.tokens}>
                  Tokens
                </th>
                <th role="columnheader" scope="col" className={styles.cost}>
                  Cost
                </th>
              </tr>
            </thead>
            <tbody role="rowgroup">
              {recent.map((row) => (
                <tr role="row" key={row.key}>
                  <td role="cell" className={styles.day}>
                    {row.day}
                  </td>
                  <td role="cell" className={styles.read}>
                    {row.adsRead}
                  </td>
                  <td role="cell" className={styles.checked}>
                    {row.adsChecked}
                  </td>
                  <td role="cell" className={styles.tokens}>
                    {row.tokens}
                  </td>
                  <td role="cell" className={styles.cost}>
                    {row.usd}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
