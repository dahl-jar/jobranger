import { NavLink } from 'react-router'
import styles from '@/components/JobsView/JobsView.module.css'
import { useAppBase } from '@/hooks/useAppBase'
import { applicationsPath } from '@/helpers/applicationsPath'
import { listPath } from '@/helpers/adPaths'
import { JOB_LISTS, type JobList } from '@/helpers/adSelectors'
import { useText } from '@/hooks/useLanguage'


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
