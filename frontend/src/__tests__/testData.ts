import { payloadFixtures } from '@/contract/fixtures.gen'
import type { Application, ApplicationsSnapshot } from '@/contract/payload.gen'

export const applicationsSnapshot: ApplicationsSnapshot = payloadFixtures['applications.snapshot']

const [found, byHand] = applicationsSnapshot.applications
if (!found || !byHand) {
  throw new Error('applications.snapshot fixture must hold a found and a hand-added application')
}

export const foundApplication: Application = found
export const removedApplication: Application = byHand
export const handApplication: Application = { ...byHand, removed: false }
