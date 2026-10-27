import { Link } from 'react-router'
import { SETTINGS_PATH } from '@/app/paths'
import { UsageChart } from '@/features/usage/UsageChart'
import styles from '@/features/usage/usage.module.css'
import { CHART_DAYS, summarizeUsage } from '@/features/usage/usageSummary'
import type { Day, Money, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { ProgressBar } from '@/shared/ui/ProgressBar'
import { SectionLabel } from '@/shared/ui/SectionLabel'

interface UsageSidebarProps {
  usage: UsageSnapshot
  today: Day
  monthlyLimitUsd: Money | null
}

export function UsageSidebar({ usage, today, monthlyLimitUsd }: UsageSidebarProps) {
  const summary = summarizeUsage(usage, today, monthlyLimitUsd)
  const { budget } = summary
  return (
    <section>
      <SectionLabel aside={<span className={styles.aside}>Last {CHART_DAYS} days</span>}>AI usage</SectionLabel>
      <p className={styles.month}>
        <span className={styles.monthCost}>{summary.monthUsd}</span>
        spent this month
      </p>
      <div className={styles.budget}>
        <div className={styles.budgetLine}>
          <span>{budget.line}</span>
          <span className={styles.resets}>{summary.resets}</span>
        </div>
        <ProgressBar
          label="Monthly limit used"
          percent={budget.percent}
          tone={budget.nearLimit ? 'near' : 'ok'}
          className={styles.track}
        />
        <Link to={`${SETTINGS_PATH}/model`} className={styles.limitLink}>
          {budget.action}
        </Link>
      </div>
      <dl className={styles.figures}>
        <div>
          <dt>Today</dt>
          <dd>{summary.todayUsd}</dd>
        </div>
        <div>
          <dt>Daily avg</dt>
          <dd>{summary.averageUsd}</dd>
        </div>
        <div>
          <dt>Peak</dt>
          <dd>{summary.peakUsd}</dd>
        </div>
      </dl>
      <UsageChart
        bars={summary.bars}
        label={summary.chartLabel}
        peakTokens={summary.peakTokens}
        firstDay={summary.firstDay}
      />
    </section>
  )
}
