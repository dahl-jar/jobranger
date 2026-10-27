import { Link } from 'react-router'
import { settingsPath, useAppBase } from '@/app/paths'
import { UsageChart } from '@/features/usage/UsageChart'
import styles from '@/features/usage/usage.module.css'
import { CHART_DAYS, summarizeUsage } from '@/features/usage/usageSummary'
import type { Day, Money, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { ProgressBar } from '@/shared/ui/ProgressBar'
import { SectionLabel } from '@/shared/ui/SectionLabel'

interface UsageSidebarProps {
  usage: UsageSnapshot
  today: Day
  monthlyLimitUsd: Money | null
}

export function UsageSidebar({ usage, today, monthlyLimitUsd }: UsageSidebarProps) {
  const text = useText()
  const base = useAppBase()
  const words = text.usage
  const summary = summarizeUsage(usage, today, monthlyLimitUsd, text)
  const { budget } = summary
  return (
    <section>
      <SectionLabel aside={<span className={styles.aside}>{words.lastDays(CHART_DAYS)}</span>}>{words.heading}</SectionLabel>
      <p className={styles.month}>
        <span className={styles.monthCost}>{summary.monthUsd}</span>
        {words.spent}
      </p>
      <div className={styles.budget}>
        <div className={styles.budgetLine}>
          <span>{budget.line}</span>
          <span className={styles.resets}>{summary.resets}</span>
        </div>
        <ProgressBar
          label={words.limitUsed}
          percent={budget.percent}
          tone={budget.nearLimit ? 'near' : 'ok'}
          className={styles.track}
        />
        <Link to={settingsPath(base, 'model')} className={styles.limitLink}>
          {budget.action}
        </Link>
      </div>
      <dl className={styles.figures}>
        <div>
          <dt>{words.today}</dt>
          <dd>{summary.todayUsd}</dd>
        </div>
        <div>
          <dt>{words.average}</dt>
          <dd>{summary.averageUsd}</dd>
        </div>
        <div>
          <dt>{words.peak}</dt>
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
