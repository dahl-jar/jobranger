import type { TerminalLine } from '@/features/setup/Terminal'
import { demoAds, DEMO_PAIR_CODE, type DemoAd } from '@/shared/demo/demoData'
import { en, type Messages } from '@/shared/i18n/en'

export type SetupStepId = 'install' | 'model' | 'cv' | 'limits' | 'delivery' | 'dryRun' | 'pair' | 'schedule'

export interface SetupStep {
  id: SetupStepId
  title: string
  command: string
  body: string
  lines: readonly TerminalLine[]
}

const FIT_WIDTH = 13
const FIT_GAP = 2
const TITLE_WIDTH = 20
const COMPANY_WIDTH = 17
const PLACE_WIDTH = 11
const VERSION = '0.4.1'
const INSTALL_COMMAND = 'pip install jobranger'
const INIT_COMMAND = 'jobranger init'
const PAIR_COMMAND = 'jobranger pair'
const SERVICE_COMMAND = 'jobranger service install'

function fitWidth(text: Messages): number {
  const { strong, partial } = text.ui.fit
  return Math.max(FIT_WIDTH, strong.length + FIT_GAP, partial.length + FIT_GAP)
}

function dryRunLine(ad: DemoAd, text: Messages): TerminalLine {
  const fit = text.ui.fit[ad.fit].padEnd(fitWidth(text))
  const line = `  ${fit}${ad.title.padEnd(TITLE_WIDTH)}${ad.company.padEnd(COMPANY_WIDTH)}${ad.place.padEnd(PLACE_WIDTH)}${ad.due}`
  return [ad.fit === 'strong' ? 'ok' : 'part', line]
}

function stepText(text: Messages, id: SetupStepId): Pick<SetupStep, 'id' | 'title' | 'body'> {
  return { id, ...text.setup.steps[id] }
}

export function setupSteps(pairUrl: string, text: Messages = en): readonly [SetupStep, ...SetupStep[]] {
  const lines = text.setup.terminal
  const demo = demoAds(text)
  return [
    {
      ...stepText(text, 'install'),
      command: INSTALL_COMMAND,
      lines: [
        ['cmd', INSTALL_COMMAND],
        ['dim', 'Collecting jobranger'],
        ['dim', 'Installing collected packages: jobranger'],
        ['ok', `✓ Successfully installed jobranger-${VERSION}`],
        ['blank', ''],
        ['cmd', 'jobranger --version'],
        ['out', `jobranger ${VERSION}`],
      ],
    },
    {
      ...stepText(text, 'model'),
      command: INIT_COMMAND,
      lines: [
        ['cmd', INIT_COMMAND],
        ['blank', ''],
        ['head', lines.model.head],
        ['blank', ''],
        ['q', lines.model.provider],
        ['sel', '  › Anthropic'],
        ['dim', '    (OpenAI, OpenRouter, Ollama)'],
        ['blank', ''],
        ['q', lines.model.key],
        ['ok', lines.model.keyWorks],
      ],
    },
    {
      ...stepText(text, 'cv'),
      command: INIT_COMMAND,
      lines: [
        ['head', lines.cv.head],
        ['blank', ''],
        ['q', lines.cv.path],
        ['dim', lines.cv.reading],
        ['out', lines.cv.experience],
        ['out', lines.cv.education],
        ['out', lines.cv.languages],
        ['out', lines.cv.stack],
        ['q', lines.cv.confirm],
        ['blank', ''],
        ['q', lines.cv.roles],
        ['sel', lines.cv.rolesFirst],
        ['sel', lines.cv.rolesSecond],
        ['sel', lines.cv.rolesThird],
        ['out', lines.cv.rolesFourth],
        ['dim', lines.cv.suggested],
      ],
    },
    {
      ...stepText(text, 'limits'),
      command: INIT_COMMAND,
      lines: [
        ['head', lines.limits.head],
        ['blank', ''],
        ['q', lines.limits.level],
        ['q', lines.limits.years],
        ['q', lines.limits.masters],
        ['q', lines.limits.places],
        ['dim', lines.limits.anywhere],
        ['blank', ''],
        ['out', lines.limits.sources],
        ['ok', lines.limits.nav],
        ['ok', lines.limits.employers],
        ['part', lines.limits.finn],
        ['part', lines.limits.linkedin],
        ['dim', lines.limits.terms],
        ['dim', lines.limits.personal],
        ['q', lines.limits.optIn],
      ],
    },
    {
      ...stepText(text, 'delivery'),
      command: INIT_COMMAND,
      lines: [
        ['head', lines.delivery.head],
        ['blank', ''],
        ['q', lines.delivery.how],
        ['sel', lines.delivery.website],
        ['sel', lines.delivery.email],
        ['blank', ''],
        ['q', lines.delivery.address],
        ['q', lines.delivery.sendThrough],
        ['ok', lines.delivery.testSent],
        ['blank', ''],
        ['q', lines.delivery.runTimes],
      ],
    },
    {
      ...stepText(text, 'dryRun'),
      command: INIT_COMMAND,
      lines: [
        ['head', lines.dryRun.head],
        ['blank', ''],
        ['out', lines.dryRun.nav],
        ['out', lines.dryRun.teamtailor],
        ['out', lines.dryRun.ashby],
        ['dim', `  ${text.demo.dryRunLine}`],
        ['blank', ''],
        dryRunLine(demo.havbris, text),
        dryRunLine(demo.fjellnett, text),
        dryRunLine(demo.kystlinje, text),
        ['out', lines.dryRun.more],
        ['dim', `  ${text.demo.hiddenLine}`],
        ['blank', ''],
        ['q', lines.dryRun.wrong],
        ['dim', `  ${text.demo.normalDayLine}`],
        ['ok', lines.dryRun.saved],
      ],
    },
    {
      ...stepText(text, 'pair'),
      command: PAIR_COMMAND,
      lines: [
        ['cmd', PAIR_COMMAND],
        ['blank', ''],
        ['out', lines.pair.open],
        ['head', `  ${pairUrl}`],
        ['blank', ''],
        ['out', lines.pair.enter],
        ['big', `  ${DEMO_PAIR_CODE}`],
        ['blank', ''],
        ['dim', lines.pair.expires],
        ['ok', lines.pair.paired],
      ],
    },
    {
      ...stepText(text, 'schedule'),
      command: SERVICE_COMMAND,
      lines: [
        ['cmd', SERVICE_COMMAND],
        ['ok', lines.schedule.scheduled],
        ['dim', lines.schedule.next],
        ['blank', ''],
        ['cmd', 'jobranger run'],
        ['dim', lines.schedule.reading],
        ['out', text.demo.runLine],
        ['ok', lines.schedule.sent],
      ],
    },
  ]
}
