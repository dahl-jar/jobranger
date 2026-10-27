import { NavLink } from 'react-router'
import styles from '@/features/jobs/JobsView.module.css'
import { useAppBase } from '@/app/paths'
import { applicationsPath } from '@/features/applications/applicationsPath'
import { listPath } from '@/features/jobs/paths'
import { JOB_LISTS, type JobList } from '@/features/jobs/selectors'
import { useText } from '@/shared/i18n/languageContext'


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
  const base = useAppBase()

  return (
    <nav aria-label={words.listsLabel} className={styles.tabs}>
      {JOB_LISTS.map((list) => (
        <ListLink key={list} to={listPath(list, base)} label={words.lists[list]} count={counts[list]} />
      ))}
      <span aria-hidden="true" className={styles.divider} />
      <ListLink to={applicationsPath(base)} label={words.applications} count={applications} />
    </nav>
  )
}
