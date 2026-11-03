import { Outlet } from 'react-router'
import { JobsSidebar } from '@/components/JobsSidebar/JobsSidebar'
import styles from '@/components/JobsLayout/JobsLayout.module.css'

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
