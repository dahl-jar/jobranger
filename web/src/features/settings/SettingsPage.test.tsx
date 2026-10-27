import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { QUERY_KEYS } from '@/app/queryKeys'
import type { ConfigSnapshot } from '@/features/relay/contract/payload.gen'
import { heldSend, REFUSED, refusal, SAVED, type SendMock, sentDrafts, SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'
import { drainTurns } from '@/test/flush'

const NEW_KEY = 'sk-test-0123456789abcdef'
const DOC = new File(['notes about a project'], 'notes.md', { type: 'text/markdown' })
const { status, ads, applications, profile, context, guidance, usage, models } = SNAPSHOTS
const BEFORE_CONFIG = { status, ads, applications, profile, context, guidance, usage, models }
const CV = new File(['cv text'], 'cv.txt', { type: 'text/plain' })

function cachedConfig(queryClient: ReturnType<typeof renderApp>['queryClient']): ConfigSnapshot | undefined {
  return queryClient.getQueryData<ConfigSnapshot>(QUERY_KEYS.config)
}

describe('SettingsPage', () => {
  it('should merge a search change into the config before the machine saves it', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/settings/search', { send: held.send })

    await user.click(await screen.findByRole('checkbox', { name: 'Senior' }))

    expect([sentDrafts(held.send), cachedConfig(queryClient)?.targets.levels]).toEqual([
      [{ type: 'config.update', v: 1, patch: { targets: { levels: ['junior', 'mid', 'senior'] } } }],
      ['junior', 'mid', 'senior'],
    ])
  })

  it('should restore the config when the machine refuses a change', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/settings/search', { send: held.send })
    await user.click(await screen.findByRole('checkbox', { name: 'Senior' }))

    act(() => held.reject())

    expect(await screen.findByText('Not saved: Run times must be at least 2 hours apart')).toBeInTheDocument()
    expect(cachedConfig(queryClient)?.targets.levels).toEqual(SNAPSHOTS.config.targets.levels)
  })

  it('should say Saved with the recheck line after the machine saves', async () => {
    const { user } = renderApp('/app/settings/search')

    await user.click(await screen.findByRole('checkbox', { name: 'Senior' }))

    expect(await screen.findByText('Saved')).toHaveTextContent('SavedRechecks 37 ads on the next run, up to about $0.12')
  })

  it('should show a context_full reply only under the documents that caused it', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/settings/profile', { send: held.send })
    const documents = (await screen.findByText('Context documents')).closest('section') ?? document.body

    await user.upload(within(documents).getByLabelText(/Add document/), DOC)
    act(() => held.reject(refusal('context_full', 'full')))

    await waitFor(() => expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual(['Your notes and documents are full. Remove a document first.']))
  })

  it('should send cv.upload with the file name, size and content', async () => {
    const { user, send } = renderApp('/app/settings/profile')

    await user.upload(await screen.findByLabelText(/Replace CV/), CV)

    await waitFor(() => expect(sentDrafts(send)).toEqual([{ type: 'cv.upload', v: 1, name: 'cv.txt', size_bytes: CV.size, content_b64: btoa('cv text') }]))
  })

  it('should show the machine reply under the CV when an upload is refused', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/settings/profile', { send: held.send })
    await user.upload(await screen.findByLabelText(/Replace CV/), CV)
    await waitFor(() => expect(held.send).toHaveBeenCalledOnce())

    act(() => held.reject(refusal('cv_no_text', 'empty')))

    expect(await screen.findByText('This PDF has no text. Try another file or paste a summary in the terminal.')).toBeInTheDocument()
  })

  it('should send context.upload with the file name and content', async () => {
    const { user, send } = renderApp('/app/settings/profile')
    const documents = (await screen.findByText('Context documents')).closest('section') ?? document.body

    await user.upload(within(documents).getByLabelText(/Add document/), DOC)

    await waitFor(() => expect(sentDrafts(send)).toEqual([{ type: 'context.upload', v: 1, name: 'notes.md', content_b64: btoa('notes about a project') }]))
  })

  it('should send guidance.reset on Reset to default', async () => {
    const { user, send } = renderApp('/app/settings/matching')
    await user.click(await screen.findByRole('button', { name: 'Edit matching guidance' }))

    await user.click(screen.getByRole('button', { name: 'Reset to default' }))

    expect(sentDrafts(send)).toEqual([{ type: 'guidance.reset', v: 1 }])
  })

  it('should send model.select for a picked model', async () => {
    const { user, send } = renderApp('/app/settings/model')
    await user.click(await screen.findByRole('radio', { name: /^Ollama/ }))

    await user.selectOptions(screen.getByRole('combobox', { name: 'Model' }), 'local-model')

    expect(sentDrafts(send)).toEqual([{ type: 'model.select', v: 1, provider: 'ollama', model: 'local-model' }])
  })

  it('should unpair through the relay from Connection', async () => {
    const { user, unpair } = renderApp('/app/settings/connection')
    await user.click(await screen.findByRole('button', { name: 'Unpair' }))

    await user.click(screen.getByRole('button', { name: 'Yes, unpair' }))

    expect(unpair).toHaveBeenCalledOnce()
  })

  it('should send the picked language to the machine', async () => {
    const { user, send } = renderApp('/app/settings/appearance')

    await user.click(await screen.findByRole('radio', { name: 'Norsk bokmål' }))

    expect(sentDrafts(send)).toEqual([{ type: 'config.update', v: 1, patch: { language: 'nb' } }])
  })

  it('should send a language pick before the config arrives', async () => {
    const { user, send } = renderApp('/app/settings/appearance', { data: BEFORE_CONFIG })

    await user.click(await screen.findByRole('radio', { name: 'Norsk bokmål' }))

    expect(sentDrafts(send)).toEqual([{ type: 'config.update', v: 1, patch: { language: 'nb' } }])
  })

  it('should say why a language pick before the config arrives was not saved', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/settings/appearance', { data: BEFORE_CONFIG, send: held.send })
    await user.click(await screen.findByRole('radio', { name: 'Norsk bokmål' }))

    act(() => held.reject())

    expect(await screen.findByText(`Ikke lagret: ${REFUSED.message}`)).toBeInTheDocument()
  })

  it('should keep the saved line to the section it belongs to', async () => {
    const { user } = renderApp('/app/settings/search')
    await user.click(await screen.findByRole('checkbox', { name: 'Senior' }))
    await screen.findByText('Saved')

    await user.click(screen.getByRole('link', { name: 'Delivery' }))

    await screen.findByRole('heading', { name: 'Delivery' })
    expect(screen.queryByText('Saved')).toBeNull()
  })

  it('should send the about text as context.update', async () => {
    const { user, send } = renderApp('/app/settings/profile')
    const about = await screen.findByRole('textbox', { name: 'About you' })

    await user.type(about, ' Soon.')
    await user.tab()

    expect(sentDrafts(send)).toEqual([{ type: 'context.update', v: 1, about: `${SNAPSHOTS.context.about} Soon.` }])
  })

  it('should show the Usage heading once under Model', async () => {
    renderApp('/app/settings/model')

    await screen.findByRole('heading', { name: 'Model' })

    expect(screen.getAllByText('Usage')).toHaveLength(1)
  })

  it('should send a changed Experience row as profile.update', async () => {
    const { user, send } = renderApp('/app/settings/profile')
    const experience = await screen.findByRole('textbox', { name: 'Experience' })

    await user.type(experience, ' More.')
    await user.tab()

    expect(sentDrafts(send)).toEqual([{ type: 'profile.update', v: 1, summary: `${SNAPSHOTS.profile.summary} More.` }])
  })

  it('should send context.remove for the removed document', async () => {
    const { user, send } = renderApp('/app/settings/profile')

    await user.click(await screen.findByRole('button', { name: `Remove ${SNAPSHOTS.context.docs[0]?.name ?? ''}` }))

    expect(sentDrafts(send)).toEqual([{ type: 'context.remove', v: 1, doc_id: SNAPSHOTS.context.docs[0]?.id }])
  })

  it('should send key.replace with the typed key', async () => {
    const { user, send } = renderApp('/app/settings/model')
    await user.click(await screen.findByRole('button', { name: 'Replace key' }))

    await user.type(screen.getByLabelText('New API key'), NEW_KEY)
    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(sentDrafts(send)).toEqual([{ type: 'key.replace', v: 1, provider: 'anthropic', key: NEW_KEY }])
  })

  it('should send guidance.update with the edited text', async () => {
    const { user, send } = renderApp('/app/settings/matching')
    await user.click(await screen.findByRole('button', { name: 'Edit matching guidance' }))

    await user.type(screen.getByRole('textbox'), ' Prefer Bergen.')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(sentDrafts(send)).toEqual([{ type: 'guidance.update', v: 1, text: `${SNAPSHOTS.guidance.text} Prefer Bergen.` }])
  })

  it('should keep Reset to default off while a reset is going', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/settings/matching', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Edit matching guidance' }))

    await user.click(screen.getByRole('button', { name: 'Reset to default' }))

    expect(screen.getByRole('button', { name: 'Reset to default' })).toBeDisabled()
  })

  it('should warn that this browser may clear the pairing', async () => {
    Object.defineProperty(navigator, 'storage', { configurable: true, value: { persisted: async () => false } })
    onTestFinished(() => {
      Reflect.deleteProperty(navigator, 'storage')
    })

    renderApp('/app/settings/connection')

    expect(await screen.findByText('This browser may clear the pairing after 7 days without a visit.')).toBeInTheDocument()
  })

  it('should say the machine is offline under Connection', async () => {
    renderApp('/app/settings/connection', { state: { kind: 'machineOffline', lastSeen: undefined } })

    expect(await screen.findByText('Offline')).toBeInTheDocument()
  })

  it('should show the delivery settings', async () => {
    renderApp('/app/settings/delivery')

    expect(await screen.findByRole('checkbox', { name: 'Show matches on this website' })).toBeChecked()
  })

  it('should start a second upload at 0 percent', async () => {
    let call = 0
    const held = heldSend()
    const send: SendMock = vi.fn(async (draft, onProgress) => {
      call += 1
      if (call > 1) return held.send(draft, onProgress)
      onProgress?.(1, 1)
      return SAVED
    })
    const { user } = renderApp('/app/settings/profile', { send })
    const documents = (await screen.findByText('Context documents')).closest('section') ?? document.body
    await user.upload(within(documents).getByLabelText(/Add document/), DOC)
    await waitFor(() => expect(send).toHaveBeenCalledOnce())

    await user.upload(within(documents).getByLabelText(/Add document/), DOC)

    expect(await within(documents).findByText('Sending 0%')).toBeInTheDocument()
  })

  it('should keep a newer config from the machine when an earlier change fails', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/settings/search', { send: held.send })
    await user.click(await screen.findByRole('checkbox', { name: 'Senior' }))
    const machineConfig: ConfigSnapshot = { ...SNAPSHOTS.config, targets: { ...SNAPSHOTS.config.targets, levels: ['lead'] } }
    act(() => queryClient.setQueryData(QUERY_KEYS.config, machineConfig))

    act(() => held.reject())

    await screen.findByText(/^Not saved/)
    expect(cachedConfig(queryClient)?.targets.levels).toEqual(['lead'])
  })

  it('should not warn when the browser keeps the pairing', async () => {
    Object.defineProperty(navigator, 'storage', { configurable: true, value: { persisted: async () => true } })
    onTestFinished(() => {
      Reflect.deleteProperty(navigator, 'storage')
    })

    renderApp('/app/settings/connection')

    await screen.findByRole('button', { name: 'Unpair' })
    await act(() => drainTurns())

    expect(screen.queryByText(/may clear the pairing/)).toBeNull()
  })
})
