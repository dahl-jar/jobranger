import { createContext } from 'react'
import type { PairingDeps } from '@/features/pairing/pairingFlow'

export const PairingDepsContext = createContext<PairingDeps | undefined>(undefined)
