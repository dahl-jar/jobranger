import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { PriceFields } from '@/features/settings/PriceFields'

const PRICE = payloadFixtures['config.snapshot'].model.price

function renderPrices() {
  const onChange = vi.fn()
  render(<PriceFields price={PRICE} onChange={onChange} />)
  return { onChange, user: userEvent.setup() }
}

describe('PriceFields', () => {
  it('should prefill the input and output prices', () => {
    renderPrices()

    expect(screen.getByLabelText('Input')).toHaveValue(PRICE.input_per_mtok)
    expect(screen.getByLabelText('Output')).toHaveValue(PRICE.output_per_mtok)
  })

  it('should send both prices when one changes', async () => {
    const { onChange, user } = renderPrices()
    await user.clear(screen.getByLabelText('Input'))
    await user.type(screen.getByLabelText('Input'), '1.25')

    await user.tab()

    expect(onChange).toHaveBeenCalledExactlyOnceWith({ input_per_mtok: 1.25, output_per_mtok: PRICE.output_per_mtok })
  })

  it('should not send when the prices did not change', async () => {
    const { onChange, user } = renderPrices()
    await user.click(screen.getByLabelText('Output'))

    await user.tab()

    expect(onChange).not.toHaveBeenCalled()
  })

  it.each(['0', '1000000'])('should accept %s as a price', async (typed) => {
    const { onChange, user } = renderPrices()
    await user.click(screen.getByLabelText('Output'))
    fireEvent.change(screen.getByLabelText('Output'), { target: { value: typed } })

    await user.tab()

    expect(onChange).toHaveBeenCalledExactlyOnceWith({ input_per_mtok: PRICE.input_per_mtok, output_per_mtok: Number(typed) })
  })

  it('should refuse a bad input price', async () => {
    const { onChange, user } = renderPrices()
    await user.click(screen.getByLabelText('Input'))
    fireEvent.change(screen.getByLabelText('Input'), { target: { value: '-1' } })

    await user.tab()

    expect(onChange).not.toHaveBeenCalled()
  })

  it('should drop the price error after a good price', async () => {
    const { user } = renderPrices()
    await user.click(screen.getByLabelText('Input'))
    fireEvent.change(screen.getByLabelText('Input'), { target: { value: '-1' } })
    await user.tab()
    fireEvent.change(screen.getByLabelText('Input'), { target: { value: '2' } })

    await user.click(screen.getByLabelText('Input'))
    await user.tab()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it.each(['-1', '1000001', ''])('should refuse "%s" as a price', async (typed) => {
    const { onChange, user } = renderPrices()
    await user.click(screen.getByLabelText('Output'))
    fireEvent.change(screen.getByLabelText('Output'), { target: { value: typed } })

    await user.tab()

    expect(onChange).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Enter a price from 0 to 1000000.')
  })
})
