import { Outlet } from 'react-router'
import { JobsSidebar } from '@/features/jobs/JobsSidebar'
import styles from '@/features/jobs/JobsLayout.module.css'

export function JobsLayout() {
  return (
    <div className={styles.columns}>
      <main className={styles.main}>
        <Outlet />
      </main>
      <JobsSidebar />
    </div>
  )
}
