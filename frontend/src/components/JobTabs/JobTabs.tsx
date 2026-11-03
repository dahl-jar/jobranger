import { useSnapshot } from '@/hooks/useSnapshot'
import { ListTabs } from '@/components/JobTabs/ListTabs'
import { listCounts } from '@/helpers/adSelectors'

export function JobTabs() {
  const ads = useSnapshot('ads')
  const applications = useSnapshot('applications')
  if (ads === undefined) return null
  return <ListTabs counts={listCounts(ads)} applications={applications?.applications_total} />
}
