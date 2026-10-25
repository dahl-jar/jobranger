import type { TerminalLine } from '@/features/setup/Terminal'
import {
  DEMO_DRY_RUN_LINE,
  DEMO_FJELLNETT,
  DEMO_HAVBRIS,
  DEMO_HIDDEN_LINE,
  DEMO_KYSTLINJE,
  DEMO_NORMAL_DAY_LINE,
  DEMO_PAIR_CODE,
  DEMO_RUN_LINE,
  type DemoAd,
} from '@/shared/demo/demoData'
import { FIT_LABELS } from '@/shared/ui/fit'

export interface SetupStep {
  id: 'install' | 'model' | 'cv' | 'limits' | 'delivery' | 'dryRun' | 'pair' | 'schedule'
  title: string
  command: string
  body: string
  lines: readonly TerminalLine[]
}

const FIT_WIDTH = 13
const TITLE_WIDTH = 20
const COMPANY_WIDTH = 17
const PLACE_WIDTH = 11

function dryRunLine(ad: DemoAd): TerminalLine {
  const text = `  ${FIT_LABELS[ad.fit].padEnd(FIT_WIDTH)}${ad.title.padEnd(TITLE_WIDTH)}${ad.company.padEnd(COMPANY_WIDTH)}${ad.place.padEnd(PLACE_WIDTH)}${ad.due}`
  return [ad.fit === 'strong' ? 'ok' : 'part', text]
}

export function setupSteps(pairUrl: string): readonly [SetupStep, ...SetupStep[]] {
  return [
    {
      id: 'install',
      title: 'Install',
      command: 'pip install jobranger',
      body: 'pip, uv or pipx.',
      lines: [
        ['cmd', 'pip install jobranger'],
        ['dim', 'Collecting jobranger'],
        ['dim', 'Installing collected packages: jobranger'],
        ['ok', '✓ Successfully installed jobranger-0.4.1'],
        ['blank', ''],
        ['cmd', 'jobranger --version'],
        ['out', 'jobranger 0.4.1'],
      ],
    },
    {
      id: 'model',
      title: 'Model and key',
      command: 'jobranger init',
      body: 'Your provider bills you for use. The key stays on this machine.',
      lines: [
        ['cmd', 'jobranger init'],
        ['blank', ''],
        ['head', 'jobranger setup · 1 of 5 · model'],
        ['blank', ''],
        ['q', 'Which AI provider should read the ads?'],
        ['sel', '  › Anthropic'],
        ['dim', '    (OpenAI, OpenRouter, Ollama)'],
        ['blank', ''],
        ['q', 'API key   ••••••••••••••••3f9a'],
        ['ok', '✓ Key works. Stored in ~/.config/jobranger on this machine only.'],
      ],
    },
    {
      id: 'cv',
      title: 'Your CV and roles',
      command: 'jobranger init',
      body: 'jobranger reads your CV once. Fix anything wrong, then pick roles.',
      lines: [
        ['head', 'jobranger setup · 2 of 5 · you'],
        ['blank', ''],
        ['q', 'Path to your CV   ~/Documents/CV.pdf'],
        ['dim', '  Reading your CV...'],
        ['out', '  Experience   2 years, backend developer'],
        ['out', '  Education    Bachelor in computer science'],
        ['out', '  Languages    Norwegian, English'],
        ['out', '  Stack        Python, SQL, C#, .NET, React, Docker'],
        ['q', 'Is this right? (Y/n)   y'],
        ['blank', ''],
        ['q', 'Which roles? (space to pick, enter to go on)'],
        ['sel', '  [x] Backend               [ ] Frontend'],
        ['sel', '  [x] Fullstack             [ ] Mobile'],
        ['sel', '  [x] Data engineering      [x] DevOps and platform'],
        ['out', '  [ ] ML and AI'],
        ['dim', '  Suggested from your CV: Backend, Fullstack, Data engineering'],
      ],
    },
    {
      id: 'limits',
      title: 'Limits and sources',
      command: 'jobranger init',
      body: 'Limits decide what gets hidden. FINN and LinkedIn are off unless you turn them on.',
      lines: [
        ['head', 'jobranger setup · 3 of 5 · limits and sources'],
        ['blank', ''],
        ['q', 'Level   Junior, Mid-level, No level stated'],
        ['q', 'Experience the ad asks for   0–1 years, 1–3 years'],
        ['q', "Hide ads that require a master's?   yes"],
        ['q', 'Places (empty = anywhere)   '],
        ['dim', '  Anywhere in Norway, open to moving'],
        ['blank', ''],
        ['out', 'Sources'],
        ['ok', '  ✓ NAV              public job feed'],
        ['ok', '  ✓ Employer feeds   Teamtailor and Ashby career pages'],
        ['part', '  – FINN             off'],
        ['part', '  – LinkedIn         off'],
        ['dim', '    FINN and LinkedIn do not allow automated collection in their terms.'],
        ['dim', '    Turn it on only for your own, personal use.'],
        ['q', 'Turn on FINN or LinkedIn for personal use? (y/N)   n'],
      ],
    },
    {
      id: 'delivery',
      title: 'Delivery',
      command: 'jobranger init',
      body: 'Website, email or both, and when to run.',
      lines: [
        ['head', 'jobranger setup · 4 of 5 · delivery'],
        ['blank', ''],
        ['q', 'How do you want your matches?'],
        ['sel', '  [x] Website   see and change everything in your browser'],
        ['sel', '  [x] Email     a short list every morning'],
        ['blank', ''],
        ['q', 'Email address   you@example.com'],
        ['q', 'Send through   your mail account (SMTP)'],
        ['ok', '✓ Test email sent to you@example.com'],
        ['blank', ''],
        ['q', 'Run times (24h, comma separated)   03:00, 15:00'],
      ],
    },
    {
      id: 'dryRun',
      title: 'Dry run',
      command: 'jobranger init',
      body: 'Checks the last 7 days: real matches and expected model use.',
      lines: [
        ['head', 'jobranger setup · 5 of 5 · dry run on the last 7 days'],
        ['blank', ''],
        ['out', '  NAV              1 204 ads    38 software'],
        ['out', '  Teamtailor          86 ads     9 software'],
        ['out', '  Ashby               41 ads     4 software'],
        ['dim', `  ${DEMO_DRY_RUN_LINE}`],
        ['blank', ''],
        dryRunLine(DEMO_HAVBRIS),
        dryRunLine(DEMO_FJELLNETT),
        dryRunLine(DEMO_KYSTLINJE),
        ['out', '  ... 3 more'],
        ['dim', `  ${DEMO_HIDDEN_LINE}`],
        ['blank', ''],
        ['q', 'Any of these wrong? (enter to skip)'],
        ['dim', `  ${DEMO_NORMAL_DAY_LINE}`],
        ['ok', '✓ Saved ~/.config/jobranger/config.yaml'],
      ],
    },
    {
      id: 'pair',
      title: 'Pair the website',
      command: 'jobranger pair',
      body: 'One-time code, no account. Skip for email only.',
      lines: [
        ['cmd', 'jobranger pair'],
        ['blank', ''],
        ['out', 'Open this page in your browser:'],
        ['head', `  ${pairUrl}`],
        ['blank', ''],
        ['out', 'and enter this code:'],
        ['big', `  ${DEMO_PAIR_CODE}`],
        ['blank', ''],
        ['dim', 'The code expires in 10 minutes. Waiting for the website...'],
        ['ok', '✓ Paired with Firefox on macOS.'],
      ],
    },
    {
      id: 'schedule',
      title: 'Run every night',
      command: 'jobranger service install',
      body: 'Runs at your times. Run by hand with jobranger run.',
      lines: [
        ['cmd', 'jobranger service install'],
        ['ok', '✓ Scheduled at 03:00 and 15:00 every day.'],
        ['dim', '  Next run in 6 h 12 min. See logs with: jobranger logs'],
        ['blank', ''],
        ['cmd', 'jobranger run'],
        ['dim', 'Reading NAV, Teamtailor, Ashby...'],
        ['out', DEMO_RUN_LINE],
        ['ok', '✓ Sent to the website and to you@example.com'],
      ],
    },
  ]
}
