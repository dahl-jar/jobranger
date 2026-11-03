import styles from '@/components/JobsView/JobsView.module.css'
import type { RoleId, RoleOption } from '@/contract/payload.gen'
import { useText } from '@/hooks/useLanguage'
import { SectionLabel } from '@/components/SectionLabel/SectionLabel'

interface RolesFilterProps {
  roles: readonly RoleOption[]
  picked: readonly RoleId[]
  disabled: boolean
  onChange: (picked: RoleId[]) => void
}

export function RolesFilter({ roles, picked, disabled, onChange }: RolesFilterProps) {
  const toggle = (id: RoleId) => {
    const isPicked = picked.includes(id)
    onChange(roles.map((role) => role.id).filter((roleId) => (roleId === id ? !isPicked : picked.includes(roleId))))
  }
  const heading = useText().jobs.roles
  return (
    <section className={styles.block}>
      <SectionLabel>{heading}</SectionLabel>
      <div className={styles.roles}>
        {roles.map((role) => (
          <label key={role.id} className={styles.roleOption}>
            <input type="checkbox" checked={picked.includes(role.id)} disabled={disabled} onChange={() => toggle(role.id)} />
            {role.label}
          </label>
        ))}
      </div>
    </section>
  )
}
