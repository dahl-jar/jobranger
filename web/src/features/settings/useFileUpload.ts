import { useState, type ChangeEvent } from 'react'
import { readUploadFile, UPLOAD_ACCEPT, type UploadFile, type UploadReadError } from '@/features/settings/uploadFile'

export function useFileUpload(onFile: (file: UploadFile) => void) {
  const [readError, setReadError] = useState<UploadReadError>()

  async function pick(event: ChangeEvent<HTMLInputElement>) {
    const input = event.currentTarget
    const file = input.files?.[0]
    input.value = ''
    if (file === undefined) return
    const result = await readUploadFile(file)
    if (result.kind === 'error') {
      setReadError(result.error)
      return
    }
    setReadError(undefined)
    onFile(result.file)
  }

  return {
    inputProps: { type: 'file', accept: UPLOAD_ACCEPT, onChange: (event: ChangeEvent<HTMLInputElement>) => void pick(event) },
    readError,
  }
}
