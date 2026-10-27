import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import type { ContextSnapshot, ErrorMessage } from '@/features/relay/contract/payload.gen'
import { ContextDocuments } from '@/features/settings/ContextDocuments'
import { decodeBase64, TODAY } from '@/features/settings/settings.samples'

const CONTEXT = payloadFixtures['context.snapshot']
const CONTEXT_UPLOAD = payloadFixtures['context.upload']
const DOC = CONTEXT.docs[0] ?? { id: '', name: '', chars: 0, added_at: '' }

interface Options {
  context?: ContextSnapshot
  progress?: number
  errorCode?: ErrorMessage['code']
}

function renderDocuments({ context = CONTEXT, progress, errorCode }: Options = {}) {
  const onUpload = vi.fn()
  const onRemove = vi.fn()
  render(
    <ContextDocuments context={context} today={TODAY} progress={progress} errorCode={errorCode} onUpload={onUpload} onRemove={onRemove} />,
  )
  return { onUpload, onRemove, user: userEvent.setup({ applyAccept: false }) }
}

describe('ContextDocuments', () => {
  it('should remove the clicked document', async () => {
    const { onRemove, user } = renderDocuments()

    await user.click(screen.getByRole('button', { name: `Remove ${DOC.name}` }))

    expect(onRemove).toHaveBeenCalledExactlyOnceWith(DOC.id)
  })

  it('should show the size and day of each document', () => {
    renderDocuments()

    expect(screen.getByText(DOC.name)).toBeInTheDocument()
    expect(screen.getByText('2,400 characters · added 10 Oct')).toBeInTheDocument()
  })

  it('should disable Add document at the document limit', () => {
    renderDocuments({ context: { ...CONTEXT, docs_limit: CONTEXT.docs.length } })

    expect(screen.getByLabelText('Add document')).toBeDisabled()
    expect(screen.getByText('At most 1 documents.')).toBeInTheDocument()
  })

  it('should allow Add document under the limit', () => {
    renderDocuments()

    expect(screen.getByLabelText('Add document')).toBeEnabled()
    expect(screen.getByText('PDF, TXT or MD, up to 10 documents. Only the text is kept.')).toBeInTheDocument()
  })

  it('should refuse a document over 5 MB', async () => {
    const { onUpload, user } = renderDocuments()

    await user.upload(screen.getByLabelText('Add document'), new File([new Uint8Array(CV_MAX_BYTES + 1)], 'notes.md'))

    expect(await screen.findByRole('alert')).toHaveTextContent('The file must be under 5 MB.')
    expect(onUpload).not.toHaveBeenCalled()
  })

  it('should send a picked document as name and base64', async () => {
    const { onUpload, user } = renderDocuments()
    const bytes = decodeBase64(CONTEXT_UPLOAD.content_b64)

    await user.upload(screen.getByLabelText('Add document'), new File([bytes], CONTEXT_UPLOAD.name))

    await vi.waitFor(() => {
      expect(onUpload).toHaveBeenCalledExactlyOnceWith({
        name: CONTEXT_UPLOAD.name,
        sizeBytes: bytes.length,
        contentB64: CONTEXT_UPLOAD.content_b64,
      })
    })
  })

  it('should show the file error over the machine error', async () => {
    const { user } = renderDocuments({ errorCode: 'context_full' })

    await user.upload(screen.getByLabelText('Add document'), new File(['notes'], 'notes.docx'))

    await vi.waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Use a PDF, text or Markdown file.')
    })
  })

  it('should say when there are no documents', () => {
    renderDocuments({ context: { ...CONTEXT, docs: [] } })

    expect(screen.getByText('No documents yet. Add notes or a project write-up the CV leaves out.')).toBeInTheDocument()
  })

  it('should show the sending progress', () => {
    renderDocuments({ progress: 0.5 })

    expect(screen.getByRole('status')).toHaveTextContent('Sending 50%')
  })

  it.each<{ code: ErrorMessage['code']; text: string }>([
    { code: 'context_unreadable', text: "jobranger couldn't read this file. Try a PDF or text file." },
    { code: 'context_full', text: 'Your notes and documents are full. Remove a document first.' },
  ])('should explain $code from the machine', ({ code, text }) => {
    renderDocuments({ errorCode: code })

    expect(screen.getByRole('alert')).toHaveTextContent(text)
  })
})
