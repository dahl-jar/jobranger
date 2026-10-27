import { useSnapshot } from '@/app/useSnapshot'
import { ListTabs } from '@/features/jobs/ListTabs'
import { listCounts } from '@/features/jobs/selectors'

export function JobTabs() {
  const ads = useSnapshot('ads')
  const applications = useSnapshot('applications')
  if (ads === undefined) return null
  return <ListTabs counts={listCounts(ads)} applications={applications?.applications_total} />
}
