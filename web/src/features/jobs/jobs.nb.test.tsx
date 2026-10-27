import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { AdDetail } from '@/features/jobs/AdDetail'
import { ConfigSummary } from '@/features/jobs/ConfigSummary'
import { ADS_SNAPSHOT, APPLIED_AD, CONFIG_SNAPSHOT, DATED_AD, YEARS_HIDDEN_AD } from '@/features/jobs/goldenPayloads'
import { JobsView } from '@/features/jobs/JobsView'
import { RolesFilter } from '@/features/jobs/RolesFilter'
import { ListTabs } from '@/features/jobs/ListTabs'
import { type JobList, listCounts } from '@/features/jobs/selectors'
import type { HiddenKind } from '@/features/relay/contract/payload.gen'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian } from '@/test/render'

const MARK_ERROR = 'offline'
const APPLICATIONS_TOTAL = 3
const AD_DATA = [
  ...ADS_SNAPSHOT.active.flatMap((ad) => [ad.title, ad.company, ad.place, ad.summary, ad.reason, ...ad.gaps, ...ad.checks.map((check) => check.text)]),
  ...ADS_SNAPSHOT.hidden.flatMap((ad) => [ad.title, ad.company, ad.reason]),
  ...ADS_SNAPSHOT.closed.flatMap((ad) => [ad.title, ad.company]),
  ...CONFIG_SNAPSHOT.roles.map((role) => role.label),
]

function renderList(list: JobList, reason: HiddenKind | undefined = undefined) {
  renderNorwegian(
    <>
      <ListTabs counts={listCounts(ADS_SNAPSHOT)} applications={APPLICATIONS_TOTAL} />
      <JobsView
        list={list}
        snapshot={ADS_SNAPSHOT}
        config={CONFIG_SNAPSHOT}
        reason={reason}
        movedIds={new Set([YEARS_HIDDEN_AD.id])}
        errors={{ [DATED_AD.id]: MARK_ERROR }}
        disabled={false}
        onMark={vi.fn()}
        onUnmark={vi.fn()}
        onShowAnyway={vi.fn()}
        onReasonChange={vi.fn()}
      />
    </>,
  )
}

function renderDetail(ad: typeof DATED_AD | undefined) {
  renderNorwegian(
    <AdDetail ad={ad} today={ADS_SNAPSHOT.as_of} roles={CONFIG_SNAPSHOT.roles} from="today" disabled={false} error={MARK_ERROR} onMark={vi.fn()} onUnmark={vi.fn()} />,
  )
}

describe('jobs in bokmål', () => {
  it.each(['today', 'active', 'hidden', 'closed', 'all'] as const)('should show the %s list without English text', async (list) => {
    renderList(list)

    expect(await screen.findByRole('navigation', { name: 'Lister' })).toBeInTheDocument()
    expectNoEnglish(AD_DATA)
  })

  it('should show the hidden list before the config arrives', async () => {
    renderNorwegian(
      <JobsView
        list="hidden"
        snapshot={ADS_SNAPSHOT}
        config={undefined}
        reason={undefined}
        movedIds={new Set()}
        errors={{}}
        disabled={false}
        onMark={vi.fn()}
        onUnmark={vi.fn()}
        onShowAnyway={vi.fn()}
        onReasonChange={vi.fn()}
      />,
    )

    expect(await screen.findByRole('table', { name: 'Skjulte annonser' })).toBeInTheDocument()
  })

  it('should count one hidden ad of a kind in the singular', async () => {
    renderList('today')

    expect(await screen.findByText(/· 1 annen grunn ·/)).toBeInTheDocument()
  })

  it('should show hidden ads picked by reason without English text', async () => {
    renderList('hidden', YEARS_HIDDEN_AD.kind)

    expect(await screen.findByRole('table', { name: 'Skjulte annonser' })).toBeInTheDocument()
    expectNoEnglish(AD_DATA)
  })

  it.each([
    { name: 'an open ad', ad: DATED_AD },
    { name: 'an applied ad', ad: { ...APPLIED_AD, summary: 'Bygg API-er.' } },
  ])('should show $name in detail without English text', async ({ ad }) => {
    renderDetail(ad)

    expect(await screen.findByRole('link', { name: 'Tilbake til «I dag»' })).toBeInTheDocument()
    expectNoEnglish(AD_DATA)
  })

  it('should say the ad is gone in bokmål', async () => {
    renderDetail(undefined)

    expect(await screen.findByText('Denne annonsen er ikke lenger i listen din.')).toBeInTheDocument()
    expectNoEnglish(AD_DATA)
  })

  it('should show the search summary in bokmål', async () => {
    renderNorwegian(<ConfigSummary targets={CONFIG_SNAPSHOT.targets} />)

    expect(await screen.findByRole('link', { name: 'Endre søket' })).toBeInTheDocument()
    expectNoEnglish(AD_DATA)
  })

  it('should show the roles heading in bokmål', async () => {
    renderNorwegian(<RolesFilter roles={CONFIG_SNAPSHOT.roles} picked={[]} disabled={false} onChange={vi.fn()} />)

    expect(await screen.findByRole('heading', { name: 'Roller' })).toBeInTheDocument()
  })
})
