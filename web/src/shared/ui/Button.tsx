import type { ComponentProps } from 'react'
import styles from '@/shared/ui/button.module.css'

type ButtonVariant = 'primary' | 'quiet' | 'link'
type ButtonSize = 36 | 40 | 44

interface ButtonProps extends ComponentProps<'button'> {
  variant?: ButtonVariant
  size?: ButtonSize
}

export function Button({ variant = 'primary', size, className, type = 'button', ...props }: ButtonProps) {
  const classes = [styles[variant], size === undefined ? undefined : styles[`size${size}`], className]
  return <button type={type} className={classes.filter(Boolean).join(' ')} {...props} />
}
