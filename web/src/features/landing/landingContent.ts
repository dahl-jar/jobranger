import { en } from '@/shared/i18n/en'

export const INSTALL_MANAGERS = [
  { id: 'pip', command: 'pip install jobranger' },
  { id: 'uv', command: 'uv tool install jobranger' },
] as const

export const INSTALL_STEPS = [
  { id: 'setUp', number: '2', command: 'jobranger init', walkthroughStep: 2 },
  { id: 'pair', number: '3', command: 'jobranger pair', walkthroughStep: 7 },
  { id: 'schedule', number: '4', command: 'jobranger service install', walkthroughStep: 8 },
] as const

export const NAV_SECTIONS = [
  { href: '#how', id: 'how' },
  { href: '#install', id: 'install' },
  { href: '#free', id: 'free' },
  { href: '#faq', id: 'faq' },
] as const

export const CLAIMS = en.landing.claims
export const HOW_STEPS = en.landing.howSteps
export const PRIVACY_LINES = en.landing.privacy
