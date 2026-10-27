const ICON_SHAPES = {
  sun: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4" />
    </>
  ),
  monitor: (
    <>
      <rect x="3" y="4.5" width="18" height="12" rx="1.5" />
      <path d="M8.5 20h7M12 16.5V20" />
    </>
  ),
  moon: <path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" />,
  sliders: (
    <>
      <path d="M4 7h10M18 7h2M4 17h4M12 17h8" />
      <circle cx="16" cy="7" r="2" />
      <circle cx="10" cy="17" r="2" />
    </>
  ),
  back: <path d="M19 12H5M11 6l-6 6 6 6" />,
  forward: <path d="M5 12h14M13 6l6 6-6 6" />,
  down: <path d="M12 5v14M6 13l6 6 6-6" />,
  check: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  dash: <path d="M6 12h12" />,
  close: <path d="M7 7l10 10M17 7L7 17" />,
  strong: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M8 12.5l2.8 2.8L16.5 9.5" />
    </>
  ),
  partial: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 3a9 9 0 0 1 0 18z" fill="currentColor" stroke="none" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 2" />
    </>
  ),
  copy: (
    <>
      <rect x="8" y="8" width="12" height="12" rx="1.5" />
      <path d="M16 8V5.5A1.5 1.5 0 0 0 14.5 4h-9A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16H8" />
    </>
  ),
  plus: <path d="M12 5v14M5 12h14" />,
  play: <path d="M7 4.5v15l12-7.5z" fill="currentColor" stroke="none" />,
  pencil: <path d="M4 20l4-1 11-11-3-3L5 16z" />,
  eye: (
    <>
      <path d="M2.5 12s3.5-6.5 9.5-6.5 9.5 6.5 9.5 6.5-3.5 6.5-9.5 6.5S2.5 12 2.5 12z" />
      <circle cx="12" cy="12" r="2.5" />
    </>
  ),
  doc: (
    <>
      <path d="M6 3h8l4 4v14H6z" />
      <path d="M14 3v4h4M9 12h6M9 16h6" />
    </>
  ),
  page: (
    <>
      <path d="M6 3h8l4 4v14H6z" />
      <path d="M14 3v4h4" />
    </>
  ),
  chevron: <path d="M6 9l6 6 6-6" />,
  up: <path d="M12 19V5M6 11l6-6 6 6" />,
  upDown: <path d="M8 9l4-4 4 4M8 15l4 4 4-4" />,
  external: <path d="M7 17L17 7M9 7h8v8" />,
  place: (
    <>
      <path d="M12 21s-6.5-5.6-6.5-10.5a6.5 6.5 0 0 1 13 0C18.5 15.4 12 21 12 21z" />
      <circle cx="12" cy="10.5" r="2.2" />
    </>
  ),
  area: (
    <>
      <rect x="3.5" y="7" width="17" height="12.5" rx="1.5" />
      <path d="M8.5 7V5.5a1.5 1.5 0 0 1 1.5-1.5h4a1.5 1.5 0 0 1 1.5 1.5V7" />
    </>
  ),
  source: (
    <>
      <path d="M5 4.5h11.5v15H7a2 2 0 0 1-2-2z" />
      <path d="M16.5 8.5H19v9a2 2 0 0 1-2 2" />
      <path d="M8.5 8.5h4.5M8.5 12h4.5M8.5 15.5h3" />
    </>
  ),
  applied: (
    <>
      <path d="M4 12.5l3.5 3.5L14 9.5" />
      <path d="M11 16l1.5 1.5L20 10" />
    </>
  ),
  skip: (
    <>
      <path d="M5 12h11M12 7l5 5-5 5" />
      <path d="M20 6v12" />
    </>
  ),
  notForMe: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M6 6l12 12" />
    </>
  ),
  level: <path d="M4 20V14M10 20V9M16 20V4" />,
  cap: (
    <>
      <path d="M2.5 9.5L12 5l9.5 4.5L12 14z" />
      <path d="M6.5 11.5V16c0 1.4 2.5 3 5.5 3s5.5-1.6 5.5-3v-4.5" />
    </>
  ),
  waiting: <path d="M12 7.5V12l3 2M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z" />,
  interview: <path d="M4 5h16v10H9l-5 4z" />,
  offer: <path d="M4 10h16v10H4zM12 10v10M3 6h18v4H3zM12 6c-1.5-3-5-3-5-1s3 1 5 1c2 0 5 1 5-1s-3.5-2-5 1" />,
  accepted: <path d="M8 12.5l2.8 2.8L16.5 9.5M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z" />,
  running: <circle cx="12" cy="12" r="8" strokeDasharray="30 20" />,
  pending: <circle cx="12" cy="12" r="7" />,
}

export type IconName = keyof typeof ICON_SHAPES

const DEFAULT_SIZE = 14
const LIGHT_STROKE_WIDTH = 1.8
const DEFAULT_STROKE_WIDTH = 1.9
const MEDIUM_STROKE_WIDTH = 2
const CHECK_STROKE_WIDTH = 2.4
const BOLD_STROKE_WIDTH = 2.2

const STROKE_WIDTHS: Partial<Record<IconName, number>> = {
  forward: MEDIUM_STROKE_WIDTH,
  down: MEDIUM_STROKE_WIDTH,
  clock: MEDIUM_STROKE_WIDTH,
  strong: BOLD_STROKE_WIDTH,
  partial: BOLD_STROKE_WIDTH,
  check: CHECK_STROKE_WIDTH,
  dash: CHECK_STROKE_WIDTH,
  close: CHECK_STROKE_WIDTH,
  plus: BOLD_STROKE_WIDTH,
  pencil: BOLD_STROKE_WIDTH,
  doc: LIGHT_STROKE_WIDTH,
  sliders: LIGHT_STROKE_WIDTH,
  page: BOLD_STROKE_WIDTH,
  chevron: BOLD_STROKE_WIDTH,
  up: CHECK_STROKE_WIDTH,
  upDown: CHECK_STROKE_WIDTH,
  external: MEDIUM_STROKE_WIDTH,
  place: LIGHT_STROKE_WIDTH,
  area: LIGHT_STROKE_WIDTH,
  source: LIGHT_STROKE_WIDTH,
  level: LIGHT_STROKE_WIDTH,
  cap: LIGHT_STROKE_WIDTH,
  waiting: MEDIUM_STROKE_WIDTH,
  interview: MEDIUM_STROKE_WIDTH,
  offer: MEDIUM_STROKE_WIDTH,
  accepted: MEDIUM_STROKE_WIDTH,
  running: CHECK_STROKE_WIDTH,
  pending: MEDIUM_STROKE_WIDTH,
}

export function Icon({ name, size = DEFAULT_SIZE }: { name: IconName; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={STROKE_WIDTHS[name] ?? DEFAULT_STROKE_WIDTH}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {ICON_SHAPES[name]}
    </svg>
  )
}
