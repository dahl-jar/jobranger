import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { AboutYou } from '@/features/settings/AboutYou'
import { AppearanceSettings } from '@/features/settings/AppearanceSettings'
import { ConnectionSettings } from '@/features/settings/ConnectionSettings'
import { ContextDocuments } from '@/features/settings/ContextDocuments'
import { CvUpload } from '@/features/settings/CvUpload'
import { DeliverySettings } from '@/features/settings/DeliverySettings'
import { MatchingSettings } from '@/features/settings/MatchingSettings'
import { ModelSettings } from '@/features/settings/ModelSettings'
import { PriceFields } from '@/features/settings/PriceFields'
import { ProfileSettings } from '@/features/settings/ProfileSettings'
import { ReplaceKeyForm } from '@/features/settings/ReplaceKeyForm'
import { SavedLine } from '@/features/settings/SavedLine'
import { SearchSettings } from '@/features/settings/SearchSettings'
import { TODAY } from '@/features/settings/settings.samples'
import { SettingsLayout } from '@/features/settings/SettingsLayout'
import { SETTINGS_SECTIONS } from '@/features/settings/settingsSections'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian, renderNorwegianRoutes } from '@/test/render'

const CONFIG = payloadFixtures['config.snapshot']
const MODELS = payloadFixtures['models.snapshot']
const USAGE = payloadFixtures['usage.snapshot']
const PROFILE = payloadFixtures['profile.snapshot']
const CONTEXT = payloadFixtures['context.snapshot']
const GUIDANCE = payloadFixtures['guidance.snapshot']
const STATUS = payloadFixtures['status']
const NO_KEY_PROVIDER = 'openrouter'

const SETTINGS_DATA = [
  ...CONFIG.roles.map((role) => role.label),
  ...CONFIG.targets.places,
  ...MODELS.providers.flatMap((provider) => [provider.error ?? '', ...provider.models.map((model) => model.name)]),
  PROFILE.summary,
  PROFILE.education,
  ...PROFILE.languages,
  ...PROFILE.stack,
  ...PROFILE.projects.map((project) => project.name),
  CONTEXT.about,
  ...CONTEXT.docs.map((doc) => doc.name),
  GUIDANCE.text,
  STATUS.machine,
]

describe('settings in bokmål', () => {
  it.each(SETTINGS_SECTIONS.map((section) => section.id))('should frame the %s section without English text', async (section) => {
    renderNorwegianRoutes(
      [{ path: '/app/settings/:section', element: <SettingsLayout editsDisabled={false} saveResult={undefined} renderSection={() => null} /> }],
      `/app/settings/${section}`,
    )

    expect(await screen.findByRole('navigation', { name: 'Innstillinger' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it.each([
    { name: 'a saved change with a recheck', result: { kind: 'saved' as const, recheck: { ads: 3, cost_usd: 0.12 } } },
    { name: 'a failed change', result: { kind: 'failed' as const, message: 'tidsavbrudd' } },
  ])('should tell $name in bokmål', async ({ result }) => {
    renderNorwegian(<SavedLine result={result} />)

    expect(await screen.findByRole('status')).toBeInTheDocument()
    expectNoEnglish()
  })

  it.each([
    { name: 'with places', targets: CONFIG.targets },
    { name: 'without places', targets: { ...CONFIG.targets, places: [] } },
  ])('should show search $name without English text', async ({ targets }) => {
    renderNorwegian(<SearchSettings config={{ ...CONFIG, targets }} onPatch={vi.fn()} />)

    expect(await screen.findByRole('heading', { name: 'Roller' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it('should show delivery and its warnings without English text', async () => {
    const user = userEvent.setup()
    renderNorwegian(<DeliverySettings config={{ ...CONFIG, schedule: { ...CONFIG.schedule, times: ['03:00', '04:00'] } }} onPatch={vi.fn()} />)

    await user.click(await screen.findByRole('checkbox', { name: 'Vis treff på denne nettsiden' }))

    expect(screen.getByRole('group', { name: 'Denne siden slutter å få oppdateringer. Slå av?' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it.each([
    { name: 'a configured provider', provider: CONFIG.model.provider },
    { name: 'a provider without a key', provider: NO_KEY_PROVIDER },
  ])('should show the model section for $name without English text', async ({ provider }) => {
    const user = userEvent.setup()
    renderNorwegian(
      <ModelSettings
        config={{ ...CONFIG, model: { ...CONFIG.model, monthly_limit_usd: 3 } }}
        models={MODELS}
        usage={USAGE}
        usageTable={null}
        onSelectModel={vi.fn()}
        onReplaceKey={vi.fn()}
        onPatch={vi.fn()}
      />,
    )

    await user.click(await screen.findByRole('radio', { name: provider === NO_KEY_PROVIDER ? 'OpenRouter' : 'Anthropic' }))

    expect(screen.getByRole('group', { name: 'Leverandør' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it('should reject a bad price in bokmål', async () => {
    const user = userEvent.setup()
    renderNorwegian(<PriceFields price={CONFIG.model.price} onChange={vi.fn()} />)

    await user.clear(await screen.findByRole('spinbutton', { name: 'Inndata' }))
    await user.tab()

    expect(screen.getByRole('alert')).toHaveTextContent('Skriv inn en pris fra 0 til')
    expectNoEnglish()
  })

  it('should reject a bad key in bokmål', async () => {
    const user = userEvent.setup()
    renderNorwegian(<ReplaceKeyForm focusOnOpen={false} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.type(await screen.findByLabelText('Ny API-nøkkel'), 'kort')
    await user.click(screen.getByRole('button', { name: 'Lagre nøkkelen' }))

    expect(screen.getByRole('alert')).toHaveTextContent('En API-nøkkel har 12 tegn eller flere og ingen mellomrom.')
    expectNoEnglish()
  })

  it.each([
    { name: 'edited guidance', guidance: GUIDANCE },
    { name: 'the default guidance', guidance: { ...GUIDANCE, is_default: true } },
  ])('should show $name without English text', async ({ guidance }) => {
    const user = userEvent.setup()
    renderNorwegian(<MatchingSettings guidance={guidance} errorCode="guidance_empty" editsDisabled={false} onSave={vi.fn()} onReset={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: 'Endre instruksene' }))

    expect(screen.getByRole('button', { name: 'Lukk instruksene' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it.each([
    { name: 'a saved pairing', isOnline: true, isPersisted: true },
    { name: 'an offline machine and a pairing that may be cleared', isOnline: false, isPersisted: false },
  ])('should show the connection with $name without English text', async ({ isOnline, isPersisted }) => {
    const user = userEvent.setup()
    renderNorwegian(<ConnectionSettings status={STATUS} isOnline={isOnline} isPersisted={isPersisted} today={TODAY} onUnpair={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: 'Koble fra' }))

    expect(screen.getByRole('button', { name: 'Ja, koble fra' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it.each([
    { name: 'a CV and documents', cvName: PROFILE.cv_name, cvReadAt: PROFILE.cv_read_at, context: CONTEXT, cvError: 'cv_no_text' as const },
    { name: 'no CV and no documents', cvName: null, cvReadAt: null, context: { ...CONTEXT, docs: [] }, cvError: 'cv_parse_failed' as const },
  ])('should show the profile with $name without English text', async ({ cvName, cvReadAt, context, cvError }) => {
    renderNorwegian(
      <ProfileSettings
        profile={PROFILE}
        cvUpload={<CvUpload cvName={cvName} cvReadAt={cvReadAt} today={TODAY} progress={0.4} errorCode={cvError} onUpload={vi.fn()} />}
        onUpdate={vi.fn()}
      >
        <AboutYou context={context} errorCode="context_full" onSave={vi.fn()} />
        <ContextDocuments context={context} today={TODAY} progress={undefined} errorCode="context_unreadable" onUpload={vi.fn()} onRemove={vi.fn()} />
      </ProfileSettings>,
    )

    expect(await screen.findByRole('heading', { name: 'Profilen din' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })

  it('should show a full document list in bokmål', async () => {
    renderNorwegian(
      <ContextDocuments context={{ ...CONTEXT, docs_limit: CONTEXT.docs.length }} today={TODAY} progress={undefined} errorCode={undefined} onUpload={vi.fn()} onRemove={vi.fn()} />,
    )

    expect(await screen.findByText('Maks 1 dokument.')).toBeInTheDocument()
  })

  it('should show a document size with a space between thousands', async () => {
    renderNorwegian(
      <ContextDocuments context={{ ...CONTEXT, docs: CONTEXT.docs.map((doc) => ({ ...doc, chars: 12_345 })) }} today={TODAY} progress={undefined} errorCode={undefined} onUpload={vi.fn()} onRemove={vi.fn()} />,
    )

    expect(await screen.findByText(/^12 345 tegn/)).toBeInTheDocument()
  })

  it('should show appearance without English text', async () => {
    renderNorwegian(<AppearanceSettings />)

    expect(await screen.findByRole('group', { name: 'Språk' })).toBeInTheDocument()
    expectNoEnglish(SETTINGS_DATA)
  })
})
