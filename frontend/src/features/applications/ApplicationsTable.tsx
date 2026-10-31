import styles from '@/features/applications/ApplicationsTable.module.css'
import { ROW_ATTRIBUTE } from '@/features/applications/rowFocus'
import type { SortKey, SortState } from '@/features/applications/sortApplications'
import { StatusMenu } from '@/features/applications/StatusMenu'
import type { Application, ApplicationStatus } from '@/features/relay/contract/payload.gen'
import { formatDay } from '@/shared/format/dates'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

const SORT_ICON_SIZE = 12

const COLUMNS: readonly SortKey[] = ['company', 'role', 'applied', 'status', 'note']

const ARIA_SORT = { asc: 'ascending', desc: 'descending' } as const
const SORT_ICONS = { asc: 'up', desc: 'down' } as const

interface ApplicationsTableProps {
  applications: readonly Application[]
  sort: SortState
  today: string
  editsDisabled: boolean
  onSort: (key: SortKey) => void
  onOpen: (applicationId: number) => void
  onStatusChange: (applicationId: number, status: ApplicationStatus) => void
}

export function ApplicationsTable({ applications, sort, today, editsDisabled, onSort, onOpen, onStatusChange }: ApplicationsTableProps) {
  const text = useText()
  const words = text.applications

  return (
    <div className={styles.wrap}>
      <div role="table" aria-label={words.heading} className={styles.table}>
        <div role="rowgroup">
          <div role="row" className={`${styles.row} ${styles.head}`}>
            {COLUMNS.map((column) => {
              const sorted = sort.key === column
              return (
                <span key={column} role="columnheader" aria-sort={sorted ? ARIA_SORT[sort.dir] : 'none'}>
                  <button
                    type="button"
                    className={[styles.sortButton, sorted && styles.sorted].filter(Boolean).join(' ')}
                    onClick={() => onSort(column)}
                  >
                    {words.columns[column]}
                    <Icon name={sorted ? SORT_ICONS[sort.dir] : 'upDown'} size={SORT_ICON_SIZE} />
                  </button>
                </span>
              )
            })}
          </div>
        </div>
        <div role="rowgroup">
          {applications.map((application) => (
            <div key={application.id} role="row" className={`${styles.row} ${styles.body}`} onClick={() => onOpen(application.id)}>
              <span role="cell" className={styles.cell}>
                <button type="button" {...{ [ROW_ATTRIBUTE]: application.id }} className={styles.company}>
                  {application.company}
                </button>
              </span>
              <span role="cell" className={`${styles.cell} ${styles.role}`}>
                {application.title || words.roleNotSet}
              </span>
              <span role="cell" className={styles.date}>
                {formatDay(application.applied_at, today, text)}
              </span>
              <span role="cell" className={styles.cell}>
                <StatusMenu
                  status={application.status}
                  disabled={editsDisabled}
                  onChange={(status) => onStatusChange(application.id, status)}
                />
              </span>
              <span role="cell" className={styles.cell}>
                <button
                  type="button"
                  aria-label={words.openNote(application.company)}
                  className={[styles.note, !application.note && styles.noNote].filter(Boolean).join(' ')}
                >
                  {application.note || words.addNote}
                </button>
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
