import styles from '@/features/usage/usage.module.css'
import { recentUsage, TABLE_DAYS, usageTotals } from '@/features/usage/usageSummary'
import type { Day, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'

interface UsageTableProps {
  usage: UsageSnapshot
  today: Day
}

export function UsageTable({ usage, today }: UsageTableProps) {
  const text = useText()
  const words = text.usage
  const totals = usageTotals(usage, text)
  const recent = recentUsage(usage, today, text)
  return (
    <section>
      <dl className={styles.totals}>
        <div className={styles.total}>
          <dt>{words.month(usage.month_tokens, totals.monthTokens)}</dt>
          <dd>{totals.monthUsd}</dd>
        </div>
        <div className={styles.total}>
          <dt>{words.todayTokens(usage.today_tokens, totals.todayTokens)}</dt>
          <dd>{totals.todayUsd}</dd>
        </div>
      </dl>
      {recent.length === 0 ? (
        <p className={styles.empty}>{words.noRuns(TABLE_DAYS)}</p>
      ) : (
        <div role="region" aria-label={words.perDay} tabIndex={0} className={styles.tableScroll}>
          <table role="table" className={styles.table}>
            <thead role="rowgroup">
              <tr role="row">
                <th role="columnheader" scope="col" className={styles.day}>
                  {words.columns.day}
                </th>
                <th role="columnheader" scope="col" className={styles.read}>
                  {words.columns.read}
                </th>
                <th role="columnheader" scope="col" className={styles.checked}>
                  {words.columns.checked}
                </th>
                <th role="columnheader" scope="col" className={styles.tokens}>
                  {words.columns.tokens}
                </th>
                <th role="columnheader" scope="col" className={styles.cost}>
                  {words.columns.cost}
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
