import type { SnapshotData } from '@/app/useSnapshot'
import type { ActiveAd, Application, Language, RoleId, RoleOption, UsageDay } from '@/features/relay/contract/payload.gen'
import { demoAds } from '@/shared/demo/demoData'
import { en, type Messages } from '@/shared/i18n/en'

export const DEMO_TODAY = '2026-10-08'

const FOUND_AT = '2026-10-08T03:02:00Z'
const EARLIER_FOUND_AT = '2026-10-06T03:02:00Z'
const LAST_RUN_AT = '2026-10-08T03:04:00Z'
const NEXT_RUN_AT = '2026-10-08T15:00:00Z'
const PAIRED_AT = '2026-10-07T18:20:00Z'
const APPLIED_AT = '2026-10-02T09:15:00Z'
const INTERVIEW_AT = '2026-10-06T11:40:00Z'
const HAVBRIS_DEADLINE = '2026-10-10'
const KYSTLINJE_DEADLINE = '2026-10-18'
const NAV_URL = 'https://arbeidsplassen.nav.no/stillinger'
const TEAMTAILOR_URL = 'https://www.teamtailor.com/'
const PICKED_ROLES: readonly RoleId[] = ['backend', 'fullstack', 'data_engineering']
const ROLE_IDS: readonly (keyof Messages['app']['demo']['data']['roles'])[] = [
  'backend',
  'frontend',
  'fullstack',
  'data_engineering',
  'devops_platform',
  'mobile',
  'ml_ai',
]
const USAGE_DAYS: readonly Pick<UsageDay, 'day' | 'ads_read' | 'ads_checked' | 'input_tokens' | 'output_tokens' | 'cost_usd'>[] = [
  { day: '2026-10-04', ads_read: 790, ads_checked: 8, input_tokens: 26000, output_tokens: 2400, cost_usd: 0.09 },
  { day: '2026-10-05', ads_read: 802, ads_checked: 11, input_tokens: 33000, output_tokens: 3100, cost_usd: 0.12 },
  { day: '2026-10-06', ads_read: 806, ads_checked: 9, input_tokens: 28000, output_tokens: 2600, cost_usd: 0.1 },
  { day: '2026-10-07', ads_read: 809, ads_checked: 51, input_tokens: 160000, output_tokens: 11000, cost_usd: 0.55 },
  { day: '2026-10-08', ads_read: 812, ads_checked: 9, input_tokens: 27000, output_tokens: 2500, cost_usd: 0.1 },
]
const MONTH_TOKENS = 293_600
const MONTH_USD = 0.96
const TODAY_TOKENS = 29_500
const TODAY_USD = 0.1
const CALLS_PER_CHECK = 1

function activeAd(ad: Pick<ActiveAd, 'id' | 'company' | 'title' | 'place' | 'role_type' | 'source' | 'url' | 'deadline' | 'fit' | 'reason'> & Partial<ActiveAd>): ActiveAd {
  return {
    published_at: null,
    found_at: FOUND_AT,
    is_new: true,
    today: true,
    summary: '',
    gaps: [],
    checks: [],
    mark: null,
    marked_at: null,
    shown_anyway: false,
    ...ad,
  }
}

function roles(text: Messages): RoleOption[] {
  return ROLE_IDS.map((id) => ({ id, label: text.app.demo.data.roles[id] }))
}

function application(fields: Pick<Application, 'id' | 'company' | 'title' | 'status' | 'note' | 'applied_at' | 'history'> & Partial<Application>): Application {
  return {
    ad_id: null,
    place: '',
    role_type: null,
    source: null,
    url: null,
    deadline: null,
    fit: null,
    found_at: null,
    cv_name: 'cv.pdf',
    checks: [],
    removed: false,
    ...fields,
  }
}

export function demoSnapshots(language: Language, text: Messages = en): SnapshotData {
  const data = text.app.demo.data
  const examples = demoAds(text)
  const active = [
    activeAd({
      id: 'nav-havbris',
      company: examples.havbris.company,
      title: examples.havbris.title,
      place: examples.havbris.place,
      role_type: 'data_engineering',
      source: 'nav',
      url: NAV_URL,
      deadline: HAVBRIS_DEADLINE,
      fit: 'strong',
      summary: examples.havbris.summary ?? '',
      checks: [...examples.havbris.checks],
      reason: data.havbrisReason,
    }),
    activeAd({
      id: 'teamtailor-kystlinje',
      company: examples.kystlinje.company,
      title: examples.kystlinje.title,
      place: examples.kystlinje.place,
      role_type: 'devops_platform',
      source: 'teamtailor',
      url: TEAMTAILOR_URL,
      deadline: KYSTLINJE_DEADLINE,
      fit: 'partial',
      reason: data.kystlinjeReason,
      gaps: [data.kystlinjeGap],
    }),
    activeAd({
      id: 'nav-fjellnett',
      company: examples.fjellnett.company,
      title: examples.fjellnett.title,
      place: examples.fjellnett.place,
      role_type: 'backend',
      source: 'nav',
      url: NAV_URL,
      deadline: null,
      fit: 'strong',
      reason: data.fjellnettReason,
      found_at: EARLIER_FOUND_AT,
      is_new: false,
      today: false,
    }),
  ]
  const hidden = [
    { id: 'nav-lysvik', company: 'Lysvik Energi AS', title: 'Senior Backend Developer', kind: 'senior' as const, reason: data.hiddenSenior, role_type: 'backend' as const },
    { id: 'nav-fjordbyte', company: 'Fjordbyte AS', title: 'Platform Engineer', kind: 'years' as const, reason: data.hiddenYears, role_type: 'devops_platform' as const },
    { id: 'nav-skarvik', company: 'Skarvik Analyse AS', title: 'Data Scientist', kind: 'masters' as const, reason: data.hiddenMasters, role_type: 'ml_ai' as const },
  ].map((ad) => ({ ...ad, place: 'Oslo', source: 'nav' as const, url: NAV_URL, found_at: FOUND_AT, today: true }))
  const applications = [
    application({ id: 2, company: 'Sjøbris Systemer AS', title: 'Backend Developer', status: 'interview', note: data.note, applied_at: APPLIED_AT, history: [{ at: APPLIED_AT, text: data.historyApplied }, { at: INTERVIEW_AT, text: data.historyInterview }] }),
    application({ id: 1, company: 'Tindvik Digital AS', title: 'Fullstack Developer', status: 'waiting', note: '', applied_at: APPLIED_AT, history: [{ at: APPLIED_AT, text: data.historyApplied }] }),
  ]

  return {
    status: {
      type: 'status',
      v: 1,
      cli_version: '0.1.0',
      contract_version: 1,
      machine: 'laptop',
      paired_at: PAIRED_AT,
      last_run: { run_id: 1, finished_at: LAST_RUN_AT, ads_read: 812, matches: active.length, cost_usd: TODAY_USD },
      next_run_at: NEXT_RUN_AT,
      running: null,
      budget_paused: false,
    },
    ads: {
      type: 'ads.snapshot',
      v: 1,
      generated_at: LAST_RUN_AT,
      as_of: DEMO_TODAY,
      active,
      active_total: active.length,
      hidden,
      hidden_total: hidden.length,
      hidden_counts: { senior: 1, years: 1, masters: 1, role: 0, place: 0, other: 0 },
      closed: [{ id: 'nav-kaia', closed_on: '2026-10-05', company: 'Kaia Tech AS', title: 'Junior Developer', source: 'nav', reason: 'deadline', mark: 'applied' }],
      closed_total: 1,
    },
    applications: { type: 'applications.snapshot', v: 1, generated_at: LAST_RUN_AT, applications, applications_total: applications.length },
    config: {
      type: 'config.snapshot',
      v: 1,
      roles: roles(text),
      targets: { subfields: [...PICKED_ROLES], levels: ['junior', 'mid', 'none'], years: ['0-1', '1-3'], hide_masters: true, places: [], relocate: true },
      sources: { nav: true, employers: true, finn: false, linkedin: false },
      schedule: { times: ['03:00', '15:00'], min_interval_hours: 2 },
      notify: { website: true, email: { on: true, address: 'you@example.com' } },
      model: { provider: 'anthropic', model: 'claude-sonnet', key_hint: 'x7Qa', price: { input_per_mtok: 3, output_per_mtok: 15 }, monthly_limit_usd: 3 },
      language,
    },
    profile: {
      type: 'profile.snapshot',
      v: 1,
      cv_name: 'cv.pdf',
      cv_read_at: PAIRED_AT,
      summary: data.summary,
      education: data.education,
      languages: ['Norsk', 'English'],
      stack: ['Python', 'C#', 'SQL', 'TypeScript'],
      projects: [{ id: 'project-1', name: 'Booking API' }],
    },
    context: { type: 'context.snapshot', v: 1, about: '', about_limit: 4000, docs: [], docs_limit: 10 },
    guidance: { type: 'guidance.snapshot', v: 1, text: '', is_default: true, limit: 12000 },
    usage: {
      type: 'usage.snapshot',
      v: 1,
      month: DEMO_TODAY.slice(0, 7),
      month_tokens: MONTH_TOKENS,
      today_tokens: TODAY_TOKENS,
      month_usd: MONTH_USD,
      today_usd: TODAY_USD,
      resets_on: '2026-11-01',
      days: USAGE_DAYS.map((day) => ({ ...day, calls: day.ads_checked * CALLS_PER_CHECK })),
    },
    models: {
      type: 'models.snapshot',
      v: 1,
      providers: [{ provider: 'anthropic', error: null, models: [{ id: 'claude-sonnet', name: 'Claude Sonnet', input_per_mtok: 3, output_per_mtok: 15, usd_per_day: 0.1 }] }],
    },
  }
}
