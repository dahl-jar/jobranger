import type { AdsSnapshot, Day, UsageSnapshot } from '@/contract/payload.gen'

export function machineToday(ads: AdsSnapshot, usage: UsageSnapshot): Day {
  return usage.days.reduce<Day>((newest, day) => (day.day > newest ? day.day : newest), ads.as_of)
}
