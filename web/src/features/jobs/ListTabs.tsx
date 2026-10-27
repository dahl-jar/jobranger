import { NavLink } from 'react-router'
import styles from '@/features/jobs/JobsView.module.css'
import { listPath } from '@/features/jobs/paths'
import type { JobList } from '@/features/jobs/selectors'
import { useText } from '@/shared/i18n/languageContext'

const JOB_LISTS: readonly JobList[] = ['today', 'active', 'hidden', 'closed', 'all']
const APPLICATIONS_PATH = '/app/applications'

function ListLink({ to, label, count }: { to: string; label: string; count: number | undefined }) {
  return (
    <NavLink to={to} className={({ isActive }) => `${styles.tab} ${isActive ? styles.current : ''}`}>
      <span>
        {label}
        {count !== undefined && (
          <>
            {' '}
            <span className={styles.count}>{count}</span>
          </>
        )}
      </span>
    </NavLink>
  )
}

interface ListTabsProps {
  counts: Record<JobList, number>
  applications: number | undefined
}

export function ListTabs({ counts, applications }: ListTabsProps) {
  const words = useText().jobs

  return (
    <nav aria-label={words.listsLabel} className={styles.tabs}>
      {JOB_LISTS.map((list) => (
        <ListLink key={list} to={listPath(list)} label={words.lists[list]} count={counts[list]} />
      ))}
      <span aria-hidden="true" className={styles.divider} />
      <ListLink to={APPLICATIONS_PATH} label={words.applications} count={applications} />
    </nav>
  )
}
