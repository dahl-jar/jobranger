export const ROW_ATTRIBUTE = 'data-application-id'
export const ADD_BUTTON_ATTRIBUTE = 'data-add-application'

function addButton(): HTMLElement | null {
  return document.querySelector<HTMLElement>(`[${ADD_BUTTON_ATTRIBUTE}]`)
}

export function rowOrAddButton(applicationId: number): HTMLElement | null {
  return document.querySelector<HTMLElement>(`[${ROW_ATTRIBUTE}="${applicationId}"]`) ?? addButton()
}

export function neighborButton(applicationId: number): HTMLElement | null {
  const buttons = [...document.querySelectorAll<HTMLElement>(`[${ROW_ATTRIBUTE}]`)]
  const index = buttons.findIndex((button) => button.getAttribute(ROW_ATTRIBUTE) === String(applicationId))
  return buttons[index + 1] ?? buttons[index - 1] ?? addButton()
}
