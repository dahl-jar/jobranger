import { payloadSchema } from '@/features/relay/contract/schemas.gen'

export interface ChipLimit {
  maxLength: number
  maxItems: number
  item: string
  items: string
}

const SCHEMA = payloadSchema.$defs

export const PLACE_LIMIT: ChipLimit = {
  maxLength: SCHEMA.Places.items.maxLength,
  maxItems: SCHEMA.Places.maxItems,
  item: 'A place',
  items: 'places',
}

export const TERM_LIMIT: ChipLimit = {
  maxLength: SCHEMA.Terms.items.maxLength,
  maxItems: SCHEMA.Terms.maxItems,
  item: 'An item',
  items: 'items',
}

export const PROJECT_LIMIT: ChipLimit = {
  maxLength: SCHEMA.ProjectEdit.properties.name.maxLength,
  maxItems: SCHEMA.ProfileUpdate.properties.projects.maxItems,
  item: 'A project name',
  items: 'projects',
}
