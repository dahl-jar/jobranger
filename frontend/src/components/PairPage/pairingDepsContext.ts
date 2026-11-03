import { createContext } from 'react'
import type { PairingDeps } from '@/components/PairPage/pairingFlow'

export const PairingDepsContext = createContext<PairingDeps | undefined>(undefined)
