import { payloadSchema } from '@/features/relay/contract/schemas.gen'

export interface ChipLimit {
  maxLength: number
  maxItems: number
  kind: 'place' | 'term' | 'project'
}

const SCHEMA = payloadSchema.$defs

export const PLACE_LIMIT: ChipLimit = {
  maxLength: SCHEMA.Places.items.maxLength,
  maxItems: SCHEMA.Places.maxItems,
  kind: 'place',
}

export const TERM_LIMIT: ChipLimit = {
  maxLength: SCHEMA.Terms.items.maxLength,
  maxItems: SCHEMA.Terms.maxItems,
  kind: 'term',
}

export const PROJECT_LIMIT: ChipLimit = {
  maxLength: SCHEMA.ProjectEdit.properties.name.maxLength,
  maxItems: SCHEMA.ProfileUpdate.properties.projects.maxItems,
  kind: 'project',
}
