import { en, type Messages } from '@/shared/i18n/en'
import type { Check } from '@/shared/ui/CheckList'
import type { Fit } from '@/shared/ui/fit'

export interface DemoAd {
  company: string
  title: string
  place: string
  source: string
  due: string
  dueNote?: string
  fit: Fit
  summary?: string
  checks: readonly Check[]
}

const HAVBRIS_CHECK_STATES: readonly Check['state'][] = ['met', 'met', 'partly']

export function demoAds(text: Messages = en): { havbris: DemoAd; kystlinje: DemoAd; fjellnett: DemoAd } {
  const { demo } = text
  return {
    havbris: {
      company: 'Havbris Data AS',
      title: 'Data Engineer',
      place: 'Trondheim',
      source: 'NAV',
      due: demo.havbris.due,
      dueNote: demo.havbris.dueNote,
      fit: 'strong',
      summary: demo.havbris.summary,
      checks: demo.havbris.checks.map((check, index) => ({ text: check, state: HAVBRIS_CHECK_STATES[index] ?? 'met' })),
    },
    kystlinje: {
      company: 'Kystlinje AS',
      title: 'Platform Engineer',
      place: 'Oslo',
      source: 'Teamtailor',
      due: demo.kystlinjeDue,
      fit: 'partial',
      checks: [],
    },
    fjellnett: {
      company: 'Fjellnett AS',
      title: 'Graduate Developer',
      place: 'Oslo',
      source: 'NAV',
      due: demo.fjellnettDue,
      fit: 'strong',
      checks: [],
    },
  }
}

export const DEMO_MATCH_COUNT = 3
export const DEMO_PAIR_CODE = 'KQFB-MXTW'
