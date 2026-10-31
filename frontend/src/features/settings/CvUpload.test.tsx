import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import type { ErrorMessage } from '@/features/relay/contract/payload.gen'
import { CvUpload } from '@/features/settings/CvUpload'
import { CV_BYTES, CV_UPLOAD, TODAY } from '@/features/settings/settings.samples'

const PROFILE = payloadFixtures['profile.snapshot']

interface Options {
  cvName?: string | null
  cvReadAt?: string | null
  progress?: number
  errorCode?: ErrorMessage['code']
}

function renderCv({ cvName = PROFILE.cv_name, cvReadAt = PROFILE.cv_read_at, progress, errorCode }: Options = {}) {
  const onUpload = vi.fn()
  render(
    <CvUpload cvName={cvName} cvReadAt={cvReadAt} today={TODAY} progress={progress} errorCode={errorCode} onUpload={onUpload} />,
  )
  return { onUpload, user: userEvent.setup({ applyAccept: false }) }
}

describe('CvUpload', () => {
  it('should send a picked CV as name, size and base64', async () => {
    const { onUpload, user } = renderCv()

    await user.upload(screen.getByLabelText('Replace CV'), new File([CV_BYTES], CV_UPLOAD.name))

    await vi.waitFor(() => {
      expect(onUpload).toHaveBeenCalledExactlyOnceWith({ name: CV_UPLOAD.name, sizeBytes: CV_UPLOAD.size_bytes, contentB64: CV_UPLOAD.content_b64 })
    })
  })

  it('should refuse a .docx file before sending', async () => {
    const { onUpload, user } = renderCv()

    await user.upload(screen.getByLabelText('Replace CV'), new File([CV_BYTES], 'cv.docx'))

    expect(await screen.findByRole('alert')).toHaveTextContent('Use a PDF, text or Markdown file.')
    expect(onUpload).not.toHaveBeenCalled()
  })

  it('should refuse a CV over 5 MB before sending', async () => {
    const { onUpload, user } = renderCv()

    await user.upload(screen.getByLabelText('Replace CV'), new File([new Uint8Array(CV_MAX_BYTES + 1)], 'cv.pdf'))

    expect(await screen.findByRole('alert')).toHaveTextContent('The CV must be under 5 MB.')
    expect(onUpload).not.toHaveBeenCalled()
  })

  it.each([
    { file: new File([], 'cv.pdf'), text: 'This file is empty.' },
    { file: new File([CV_BYTES], `${'a'.repeat(117)}.pdf`), text: 'Rename the file to under 120 characters, without special characters.' },
  ])('should refuse $file.name before sending', async ({ file, text }) => {
    const { onUpload, user } = renderCv()

    await user.upload(screen.getByLabelText('Replace CV'), file)

    expect(await screen.findByRole('alert')).toHaveTextContent(text)
    expect(onUpload).not.toHaveBeenCalled()
  })

  it('should say when the browser cannot read the CV', async () => {
    const { onUpload, user } = renderCv()
    const file = new File([CV_BYTES], CV_UPLOAD.name)
    vi.spyOn(file, 'arrayBuffer').mockRejectedValue(new DOMException('gone', 'NotReadableError'))

    await user.upload(screen.getByLabelText('Replace CV'), file)

    expect(await screen.findByRole('alert')).toHaveTextContent("jobranger couldn't read this file. Try a PDF or text file.")
    expect(onUpload).not.toHaveBeenCalled()
  })

  it('should clear the file error after a good file', async () => {
    const { onUpload, user } = renderCv()
    await user.upload(screen.getByLabelText('Replace CV'), new File([CV_BYTES], 'cv.docx'))
    await screen.findByRole('alert')

    await user.upload(screen.getByLabelText('Replace CV'), new File([CV_BYTES], CV_UPLOAD.name))

    await vi.waitFor(() => {
      expect(onUpload).toHaveBeenCalledOnce()
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should show no progress while nothing is sending', () => {
    renderCv()

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('should show the sending progress', () => {
    renderCv({ progress: 0.42 })

    expect(screen.getByRole('status')).toHaveTextContent('Sending 42%')
  })

  it.each<{ code: ErrorMessage['code']; text: string }>([
    { code: 'cv_type', text: 'Use a PDF, text or Markdown file.' },
    { code: 'cv_too_large', text: 'The CV must be under 5 MB.' },
    { code: 'cv_unreadable', text: "jobranger couldn't read this file. Try a PDF or text file." },
    { code: 'cv_no_text', text: 'This PDF has no text. Try another file or paste a summary in the terminal.' },
    { code: 'cv_parse_failed', text: "jobranger couldn't read your CV. Try again or edit the rows by hand." },
  ])('should explain $code from the machine', ({ code, text }) => {
    renderCv({ errorCode: code })

    expect(screen.getByRole('alert')).toHaveTextContent(text)
  })

  it('should show the file error over the machine error', async () => {
    const { user } = renderCv({ errorCode: 'cv_no_text' })

    await user.upload(screen.getByLabelText('Replace CV'), new File([CV_BYTES], 'cv.docx'))

    await vi.waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Use a PDF, text or Markdown file.')
    })
  })

  it('should not explain errors that belong to other settings', () => {
    renderCv({ errorCode: 'context_full' })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should show the CV name and the day it was read', () => {
    renderCv()

    expect(screen.getByText(PROFILE.cv_name)).toBeInTheDocument()
    expect(screen.getByText('read 10 Oct')).toBeInTheDocument()
  })

  it('should offer to add a CV when there is none', () => {
    renderCv({ cvName: null, cvReadAt: null })

    expect(screen.getByText('No CV yet')).toBeInTheDocument()
    expect(screen.queryByText(/^read /)).not.toBeInTheDocument()
    expect(screen.getByLabelText('Add CV')).toHaveAttribute('accept', '.pdf,.txt,.md')
  })
})
