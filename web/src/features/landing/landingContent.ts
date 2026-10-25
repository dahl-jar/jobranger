export const CLAIMS = ['Free and open source', 'Your own AI API key', 'Data stored on your machine'] as const

export const HOW_STEPS = [
  { number: '01', title: 'Reads job boards', body: 'NAV, Teamtailor and Ashby. FINN and LinkedIn are opt-in.' },
  { number: '02', title: 'Checks requirements', body: "Years, education, master's, level, language and place against your CV." },
  { number: '03', title: 'Lists the matches', body: 'Strong or Partial fit, a requirement checklist and your gaps.' },
  { number: '04', title: 'Tracks applications', body: 'Waiting, Interview, Offer, Accepted or Rejected, with a note.' },
] as const

export const INSTALL_MANAGERS = [
  { label: 'pip', command: 'pip install jobranger', note: 'Python 3.12 or newer.' },
  { label: 'uv', command: 'uv tool install jobranger', note: 'Python 3.12 or newer. pipx install jobranger works too.' },
] as const

export const INSTALL_STEPS = [
  {
    number: '2',
    title: 'Set up',
    command: 'jobranger init',
    body: 'Model access, CV, roles, limits and places. Ends with a dry run on the last 7 days.',
    walkthroughStep: 2,
  },
  { number: '3', title: 'Pair the website', command: 'jobranger pair', body: 'Optional. Skip it for email only.', walkthroughStep: 7 },
  {
    number: '4',
    title: 'Schedule',
    command: 'jobranger service install',
    body: 'Runs at the times you chose. Run by hand any time with jobranger run, or Run now on the website.',
    walkthroughStep: 8,
  },
] as const

export const PRIVACY_LINES = [
  'CV, ads and applications are stored on your machine.',
  'Your API key is only sent to your AI provider.',
  'The website connects through a relay that stores nothing.',
  'FINN and LinkedIn are off by default, and for personal use only.',
] as const

export const FAQ_ITEMS = [
  { question: 'Is it free?', answer: 'Yes. You only pay your AI provider for what you use.' },
  {
    question: 'Which jobs?',
    answer:
      'Software development and data engineering in Norway: backend, frontend, fullstack, mobile, data engineering, DevOps, ML and AI. More fields are in progress.',
  },
  {
    question: 'Does my computer need to be on?',
    answer: 'Only when it runs. Missed runs catch up on the next one. No schedule needed if you run it by hand. Docker works on an always-on machine.',
  },
  { question: 'Can I skip the website?', answer: 'Yes. Use email, or jobranger report in the terminal.' },
  { question: 'What does the website see?', answer: 'Only what your machine sends while the tab is open.' },
] as const
