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

export const DEMO_HAVBRIS: DemoAd = {
  company: 'Havbris Data AS',
  title: 'Data Engineer',
  place: 'Trondheim',
  source: 'NAV',
  due: '10 Oct',
  dueNote: 'in 2 days',
  fit: 'strong',
  summary: 'Build pipelines and data models for a new data platform.',
  checks: [
    { text: 'Requires 1–3 years of experience', state: 'met' },
    { text: 'Bachelor in IT', state: 'met' },
    { text: 'Azure preferred, not in your CV', state: 'partly' },
  ],
}

export const DEMO_KYSTLINJE: DemoAd = {
  company: 'Kystlinje AS',
  title: 'Platform Engineer',
  place: 'Oslo',
  source: 'Teamtailor',
  due: '18 Oct',
  fit: 'partial',
  checks: [],
}

export const DEMO_FJELLNETT: DemoAd = {
  company: 'Fjellnett AS',
  title: 'Graduate Developer',
  place: 'Oslo',
  source: 'NAV',
  due: 'No date',
  fit: 'strong',
  checks: [],
}

export const DEMO_MATCH_COUNT = 3
export const DEMO_HIDDEN_LINE = "41 hidden: 18 senior · 12 need 3+ yrs · 6 master's · 5 other areas"
export const DEMO_PAIR_CODE = 'KQFB-MXTW'
export const DEMO_RUN_LINE = '812 ads read, 64 new, 3 matches.'
export const DEMO_DRY_RUN_LINE = 'Checked 51 ads with your AI provider, about 170k tokens.'
export const DEMO_NORMAL_DAY_LINE = 'A normal day: about 9 ads for the model, around 30k tokens.'
