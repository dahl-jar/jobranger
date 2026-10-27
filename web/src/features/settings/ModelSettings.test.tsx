import { fireEvent, render, screen, within } from '@testing-library/react'
import { useState } from 'react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ConfigPatch, ConfigSnapshot, ModelsSnapshot } from '@/features/relay/contract/payload.gen'
import { applyConfigPatch } from '@/features/settings/configPatch'
import { ModelSettings } from '@/features/settings/ModelSettings'

const CONFIG = payloadFixtures['config.snapshot']
const MODELS = payloadFixtures['models.snapshot']
const USAGE = payloadFixtures['usage.snapshot']
const KEY = payloadFixtures['key.replace'].key

function renderModel({ config = CONFIG, models = MODELS }: { config?: ConfigSnapshot; models?: ModelsSnapshot } = {}) {
  const handlers = { onSelectModel: vi.fn(), onReplaceKey: vi.fn(), onPatch: vi.fn() }
  const view = (shown: ConfigSnapshot) => <ModelSettings config={shown} models={models} usage={USAGE} usageTable={<p>Usage table</p>} {...handlers} />
  const { rerender } = render(view(config))
  return { ...handlers, user: userEvent.setup(), rerenderConfig: (shown: ConfigSnapshot) => rerender(view(shown)) }
}

function modelOptions() {
  return within(screen.getByRole('combobox', { name: 'Model' }))
    .getAllByRole('option')
    .map((option) => option.textContent)
}

function OptimisticModel({ onPatch }: { onPatch: (patch: ConfigPatch) => void }) {
  const [config, setConfig] = useState<ConfigSnapshot>(CONFIG)
  return (
    <ModelSettings
      config={config}
      models={MODELS}
      usage={USAGE}
      usageTable={null}
      onSelectModel={vi.fn()}
      onReplaceKey={vi.fn()}
      onPatch={(patch) => {
        onPatch(patch)
        setConfig((current) => applyConfigPatch(current, patch))
      }}
    />
  )
}

describe('ModelSettings', () => {
  it('should move focus to Output after a saved Input price', async () => {
    const onPatch = vi.fn()
    const user = userEvent.setup()
    render(<OptimisticModel onPatch={onPatch} />)
    await user.click(screen.getByLabelText('Input'))
    fireEvent.change(screen.getByLabelText('Input'), { target: { value: '1.2' } })

    await user.tab()

    expect(onPatch).toHaveBeenCalledOnce()
    expect(screen.getByLabelText('Output')).toHaveFocus()
  })

  it('should title the key and usage parts', () => {
    renderModel()

    expect(screen.getAllByRole('heading').map((heading) => heading.textContent)).toEqual(['API key', 'Usage'])
  })

  it('should say which providers have no key yet', () => {
    renderModel()

    expect(screen.getByRole('radio', { name: 'OpenRouter' })).toHaveAccessibleDescription('No key yet')
    expect(screen.getByRole('radio', { name: 'Anthropic' })).not.toHaveAttribute('aria-describedby')
  })

  it('should ask to pick a model when the set provider has none picked', () => {
    renderModel({ config: { ...CONFIG, model: { ...CONFIG.model, model: null } } })

    expect(modelOptions()).toEqual(['Pick a model', 'Model One · about $0.21 a normal day'])
  })

  it('should list no models for a provider whose key failed', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'OpenAI' }))

    expect(screen.queryByRole('combobox', { name: 'Model' })).not.toBeInTheDocument()
  })

  it('should show no key error for a working provider', () => {
    renderModel()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should hide the key ending for a provider that is not set', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'OpenAI' }))

    expect(screen.queryByText(/^Key ending/)).not.toBeInTheDocument()
  })

  it('should close the key form when another provider is picked', async () => {
    const { user } = renderModel()
    await user.click(screen.getByRole('button', { name: 'Replace key' }))

    await user.click(screen.getByRole('radio', { name: 'OpenAI' }))

    expect(screen.queryByLabelText('New API key')).not.toBeInTheDocument()
  })

  it('should stay on a listed provider when replacing its key is cancelled', async () => {
    const { user } = renderModel()
    await user.click(screen.getByRole('radio', { name: 'OpenAI' }))
    await user.click(screen.getByRole('button', { name: 'Replace key' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByRole('radio', { name: 'OpenAI' })).toBeChecked()
  })

  it('should hide the key ending when the machine sent none', () => {
    renderModel({ config: { ...CONFIG, model: { ...CONFIG.model, key_hint: null } } })

    expect(screen.queryByText(/^Key ending/)).not.toBeInTheDocument()
  })

  it('should hide the key part before a provider is picked', () => {
    renderModel({ config: payloadFixtures['config.snapshot.empty'] })

    expect(screen.queryByRole('heading', { name: 'API key' })).not.toBeInTheDocument()
  })

  it('should show new prices from the machine', () => {
    const { rerenderConfig } = renderModel()

    rerenderConfig({ ...CONFIG, model: { ...CONFIG.model, price: { input_per_mtok: 2, output_per_mtok: 9 } } })

    expect(screen.getByLabelText('Input')).toHaveValue(2)
  })

  it("should list the picked provider's models with cost per normal day", () => {
    renderModel()

    expect(modelOptions()).toEqual(['Model One · about $0.21 a normal day'])
    expect(screen.getByRole('combobox', { name: 'Model' })).toHaveValue('model-one')
  })

  it('should select a model of another provider', async () => {
    const { onSelectModel, user } = renderModel()
    await user.click(screen.getByRole('radio', { name: 'Ollama' }))
    expect(modelOptions()).toEqual(['Pick a model', 'local-model · price unknown'])

    await user.selectOptions(screen.getByRole('combobox', { name: 'Model' }), 'local-model')

    expect(onSelectModel).toHaveBeenCalledExactlyOnceWith('ollama', 'local-model')
  })

  it('should show a set model missing from the list as selected', () => {
    renderModel({ config: { ...CONFIG, model: { ...CONFIG.model, model: 'model-two' } } })

    expect(modelOptions()).toEqual(['model-two (not listed)', 'Model One · about $0.21 a normal day'])
    expect(screen.getByRole('combobox', { name: 'Model' })).toHaveValue('model-two')
  })

  it('should select a listed model that is not first', () => {
    const second = { id: 'model-two', name: 'Model Two', input_per_mtok: 1, output_per_mtok: 5, usd_per_day: 0.3 }
    const anthropic = MODELS.providers[0] ?? { provider: 'anthropic' as const, error: null, models: [] }
    const models = { ...MODELS, providers: [{ ...anthropic, models: [...anthropic.models, second] }] }
    renderModel({ config: { ...CONFIG, model: { ...CONFIG.model, model: 'model-two' } }, models })

    expect(modelOptions()).toEqual(['Model One · about $0.21 a normal day', 'Model Two · about $0.30 a normal day'])
    expect(screen.getByRole('combobox', { name: 'Model' })).toHaveValue('model-two')
  })

  it('should show no key part for Ollama', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'Ollama' }))

    expect(screen.queryByRole('heading', { name: 'API key' })).not.toBeInTheDocument()
  })

  it('should not ask for a key when Ollama is not found', async () => {
    const { user } = renderModel({ models: { ...MODELS, providers: [] } })

    await user.click(screen.getByRole('radio', { name: 'Ollama' }))

    expect(screen.getByRole('radio', { name: 'Ollama' })).toHaveAccessibleDescription('Not found on this machine')
    expect(screen.queryByLabelText('New API key')).not.toBeInTheDocument()
  })

  it('should send a first key for a provider without one', async () => {
    const { onReplaceKey, user } = renderModel()
    await user.click(screen.getByRole('radio', { name: 'OpenRouter' }))
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(onReplaceKey).toHaveBeenCalledExactlyOnceWith('openrouter', KEY)
  })

  it('should ask for a key when the picked provider has none', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'OpenRouter' }))

    expect(screen.getByLabelText('New API key')).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Model' })).not.toBeInTheDocument()
  })

  it('should go back to the set provider when adding a key is cancelled', async () => {
    const { user } = renderModel()
    await user.click(screen.getByRole('radio', { name: 'OpenRouter' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByRole('radio', { name: 'Anthropic' })).toBeChecked()
  })

  it('should keep the saved key when replacing it is cancelled', async () => {
    const { user } = renderModel()
    await user.click(screen.getByRole('button', { name: 'Replace key' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByLabelText('New API key')).not.toBeInTheDocument()
  })

  it("should show the provider's key error", async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'OpenAI' }))

    expect(screen.getByRole('alert')).toHaveTextContent('The provider refused the key')
  })

  it('should show the end of the saved key', () => {
    renderModel()

    expect(screen.getByText('Key ending a1b2')).toBeInTheDocument()
  })

  it('should move focus to the key field when Replace key is picked', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('button', { name: 'Replace key' }))

    expect(screen.getByLabelText('New API key')).toHaveFocus()
  })

  it.each(['Save key', 'Cancel'])('should return focus to Replace key after %s', async (button) => {
    const { user } = renderModel()
    await user.click(screen.getByRole('button', { name: 'Replace key' }))
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: button }))

    expect(screen.getByRole('button', { name: 'Replace key' })).toHaveFocus()
  })

  it('should keep focus on the provider when adding a key starts', async () => {
    const { user } = renderModel()

    await user.click(screen.getByRole('radio', { name: 'OpenRouter' }))

    expect(screen.getByRole('radio', { name: 'OpenRouter' })).toHaveFocus()
  })

  it('should return focus to the set provider when adding a key is cancelled', async () => {
    const { user } = renderModel()
    await user.click(screen.getByRole('radio', { name: 'OpenRouter' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByRole('radio', { name: 'Anthropic' })).toHaveFocus()
  })

  it('should send a replaced key for the picked provider', async () => {
    const { onReplaceKey, user } = renderModel()
    await user.click(screen.getByRole('button', { name: 'Replace key' }))
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(onReplaceKey).toHaveBeenCalledExactlyOnceWith('anthropic', KEY)
  })

  it('should pick no provider before one is set', () => {
    renderModel({ config: payloadFixtures['config.snapshot.empty'] })

    expect(screen.getAllByRole('radio', { name: /Anthropic|OpenAI|OpenRouter|Ollama/ }).filter((radio) => radio.matches(':checked'))).toEqual([])
  })

  it('should send the monthly limit as a config patch', async () => {
    const { onPatch, user } = renderModel()

    await user.click(screen.getByRole('radio', { name: '$5.00' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ model: { monthly_limit_usd: 5 } })
  })

  it('should send prices as a config patch', async () => {
    const { onPatch, user } = renderModel()
    await user.click(screen.getByLabelText('Input'))
    fireEvent.change(screen.getByLabelText('Input'), { target: { value: '1' } })

    await user.tab()

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ model: { price: { input_per_mtok: 1, output_per_mtok: CONFIG.model.price.output_per_mtok } } })
  })

  it('should show the usage table it is given', () => {
    renderModel()

    expect(screen.getByText('Usage table')).toBeInTheDocument()
  })
})
