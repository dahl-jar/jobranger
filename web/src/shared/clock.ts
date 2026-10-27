import { createContext, useContext } from 'react'

export type Clock = () => Date

const systemClock: Clock = () => new Date()

export const ClockContext = createContext<Clock>(systemClock)

export function useClock(): Clock {
  return useContext(ClockContext)
}
