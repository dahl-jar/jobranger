import { describe, expect, it } from 'vitest'
import { en } from '@/shared/i18n/en'
import { nb } from '@/shared/i18n/nb'

const COUNTS = [0, 1, 2] as const

type CountText = [name: string, text: (count: number) => string, shown: [string, string, string]]

const ENGLISH: CountText[] = [
  ['dates.dueIn', en.dates.dueIn, ['in 0 days', 'in 1 day', 'in 2 days']],
  ['landing.example.heading', en.landing.example.heading, ['Today, 0 matches', 'Today, 1 match', 'Today, 2 matches']],
  ['jobs.hidden.counted.role', en.jobs.hidden.counted.role, ['0 other roles', '1 other role', '2 other roles']],
  ['jobs.hidden.counted.place', en.jobs.hidden.counted.place, ['0 other places', '1 other place', '2 other places']],
  ['jobs.hidden.counted.other', en.jobs.hidden.counted.other, ['0 other reasons', '1 other reason', '2 other reasons']],
  ['applications.form.stopsAt', (count) => en.applications.form.stopsAt('Note', count), ['Note stops at 0 characters.', 'Note stops at 1 character.', 'Note stops at 2 characters.']],
  ['applications.panel.noteStops', en.applications.panel.noteStops, ['Note stops at 0 characters.', 'Note stops at 1 character.', 'Note stops at 2 characters.']],
  ['applications.summary.bar', (count) => en.applications.summary.bar(1, 1, count), ['1 waiting, 1 rejected, 0 interviews', '1 waiting, 1 rejected, 1 interview', '1 waiting, 1 rejected, 2 interviews']],
  ['settings.recheck', en.settings.recheck, ['Rechecks 0 ads on the next run', 'Rechecks 1 ad on the next run', 'Rechecks 2 ads on the next run']],
  ['settings.chips.place.atMost', en.settings.chips.place.atMost, ['At most 0 places.', 'At most 1 place.', 'At most 2 places.']],
  ['settings.chips.term.atMost', en.settings.chips.term.atMost, ['At most 0 items.', 'At most 1 item.', 'At most 2 items.']],
  ['settings.chips.project.atMost', en.settings.chips.project.atMost, ['At most 0 projects.', 'At most 1 project.', 'At most 2 projects.']],
  ['settings.chips.tooLong', (count) => en.settings.chips.tooLong('A place', count), ['A place has at most 0 characters.', 'A place has at most 1 character.', 'A place has at most 2 characters.']],
  ['settings.delivery.tooClose', (count) => en.settings.delivery.tooClose('03:00', '04:00', count), ['03:00 and 04:00 are less than 0 hours apart.', '03:00 and 04:00 are less than 1 hour apart.', '03:00 and 04:00 are less than 2 hours apart.']],
  ['settings.delivery.gap', en.settings.delivery.gap, ['At least 0 hours apart.', 'At least 1 hour apart.', 'At least 2 hours apart.']],
  ['settings.context.facts', (count) => en.settings.context.facts(count, String(count), 'today'), ['0 characters · added today', '1 character · added today', '2 characters · added today']],
  ['settings.context.full', en.settings.context.full, ['At most 0 documents.', 'At most 1 document.', 'At most 2 documents.']],
  ['settings.context.hint', en.settings.context.hint, ['PDF, TXT or MD, up to 0 documents. Only the text is kept.', 'PDF, TXT or MD, up to 1 document. Only the text is kept.', 'PDF, TXT or MD, up to 2 documents. Only the text is kept.']],
  ['settings.upload.rename', en.settings.upload.rename, ['Rename the file to under 0 characters, without special characters.', 'Rename the file to under 1 character, without special characters.', 'Rename the file to under 2 characters, without special characters.']],
  ['run.newAds', en.run.newAds, ['0 new ads', '1 new ad', '2 new ads']],
  ['run.matches', en.run.matches, ['0 matches', '1 match', '2 matches']],
  ['run.lastRun', (count) => en.run.lastRun(count, '03:04', 'next run 15:00'), ['Read 0 ads at 03:04, next run 15:00', 'Read 1 ad at 03:04, next run 15:00', 'Read 2 ads at 03:04, next run 15:00']],
  ['run.steps.modelDone', en.run.steps.modelDone, ['Checked 0 ads', 'Checked 1 ad', 'Checked 2 ads']],
  ['run.titles.checking', en.run.titles.checking, ['Checking 0 new ads against your CV', 'Checking 1 new ad against your CV', 'Checking 2 new ads against your CV']],
  ['usage.lastDays', en.usage.lastDays, ['Last 0 days', 'Last 1 day', 'Last 2 days']],
  ['usage.noRuns', en.usage.noRuns, ['No runs in the last 0 days.', 'No runs in the last 1 day.', 'No runs in the last 2 days.']],
  ['usage.chartLabel', (count) => en.usage.chartLabel(count, '0', '9k'), ['Tokens per day for the last 0 days, from 0 to 9k', 'Tokens per day for the last 1 day, from 0 to 9k', 'Tokens per day for the last 2 days, from 0 to 9k']],
  ['usage.tip', (count) => en.usage.tip('Today', count, String(count), '$0.10'), ['Today, 0 tokens, $0.10', 'Today, 1 token, $0.10', 'Today, 2 tokens, $0.10']],
  ['usage.month', (count) => en.usage.month(count, String(count)), ['This month · 0 tokens', 'This month · 1 token', 'This month · 2 tokens']],
  ['usage.todayTokens', (count) => en.usage.todayTokens(count, String(count)), ['Today · 0 tokens', 'Today · 1 token', 'Today · 2 tokens']],
]

const NORWEGIAN: CountText[] = [
  ['dates.dueIn', nb.dates.dueIn, ['om 0 dager', 'om 1 dag', 'om 2 dager']],
  ['landing.example.heading', nb.landing.example.heading, ['I dag, 0 treff', 'I dag, 1 treff', 'I dag, 2 treff']],
  ['jobs.hidden.count', nb.jobs.hidden.count, ['0 skjulte', '1 skjult', '2 skjulte']],
  ['jobs.hidden.withParts', (count) => nb.jobs.hidden.withParts(count, '1 senior'), ['0 skjulte: 1 senior', '1 skjult: 1 senior', '2 skjulte: 1 senior']],
  ['jobs.hidden.counted.role', nb.jobs.hidden.counted.role, ['0 andre roller', '1 annen rolle', '2 andre roller']],
  ['jobs.hidden.counted.place', nb.jobs.hidden.counted.place, ['0 andre steder', '1 annet sted', '2 andre steder']],
  ['jobs.hidden.counted.other', nb.jobs.hidden.counted.other, ['0 andre grunner', '1 annen grunn', '2 andre grunner']],
  ['applications.summary.bar', (count) => nb.applications.summary.bar(1, 1, count), ['1 venter, 1 avslag, 0 intervjuer', '1 venter, 1 avslag, 1 intervju', '1 venter, 1 avslag, 2 intervjuer']],
  ['settings.recheck', nb.settings.recheck, ['Sjekker 0 annonser på nytt ved neste kjøring', 'Sjekker 1 annonse på nytt ved neste kjøring', 'Sjekker 2 annonser på nytt ved neste kjøring']],
  ['settings.chips.place.atMost', nb.settings.chips.place.atMost, ['Maks 0 steder.', 'Maks 1 sted.', 'Maks 2 steder.']],
  ['settings.chips.term.atMost', nb.settings.chips.term.atMost, ['Maks 0 oppføringer.', 'Maks 1 oppføring.', 'Maks 2 oppføringer.']],
  ['settings.chips.project.atMost', nb.settings.chips.project.atMost, ['Maks 0 prosjekter.', 'Maks 1 prosjekt.', 'Maks 2 prosjekter.']],
  ['settings.delivery.tooClose', (count) => nb.settings.delivery.tooClose('03:00', '04:00', count), ['03:00 og 04:00 ligger mindre enn 0 timer fra hverandre.', '03:00 og 04:00 ligger mindre enn 1 time fra hverandre.', '03:00 og 04:00 ligger mindre enn 2 timer fra hverandre.']],
  ['settings.delivery.gap', nb.settings.delivery.gap, ['Minst 0 timer mellom hver kjøring.', 'Minst 1 time mellom hver kjøring.', 'Minst 2 timer mellom hver kjøring.']],
  ['settings.context.full', nb.settings.context.full, ['Maks 0 dokumenter.', 'Maks 1 dokument.', 'Maks 2 dokumenter.']],
  ['settings.context.hint', nb.settings.context.hint, ['PDF, TXT eller MD, opptil 0 dokumenter. Bare teksten blir tatt vare på.', 'PDF, TXT eller MD, opptil 1 dokument. Bare teksten blir tatt vare på.', 'PDF, TXT eller MD, opptil 2 dokumenter. Bare teksten blir tatt vare på.']],
  ['run.newAds', nb.run.newAds, ['0 nye annonser', '1 ny annonse', '2 nye annonser']],
  ['run.hidden', nb.run.hidden, ['0 skjulte', '1 skjult', '2 skjulte']],
  ['run.lastRun', (count) => nb.run.lastRun(count, '03:04', 'neste kjøring kl. 15:00'), ['Leste 0 annonser kl. 03:04, neste kjøring kl. 15:00', 'Leste 1 annonse kl. 03:04, neste kjøring kl. 15:00', 'Leste 2 annonser kl. 03:04, neste kjøring kl. 15:00']],
  ['run.steps.foundNew', (count) => nb.run.steps.foundNew('NAV', count), ['NAV, 0 nye', 'NAV, 1 ny', 'NAV, 2 nye']],
  ['run.steps.modelDone', nb.run.steps.modelDone, ['Sjekket 0 annonser', 'Sjekket 1 annonse', 'Sjekket 2 annonser']],
  ['run.titles.checking', nb.run.titles.checking, ['Sjekker 0 nye annonser mot CV-en din', 'Sjekker 1 ny annonse mot CV-en din', 'Sjekker 2 nye annonser mot CV-en din']],
  ['usage.lastDays', nb.usage.lastDays, ['Siste 0 dager', 'Siste 1 dag', 'Siste 2 dager']],
  ['usage.noRuns', nb.usage.noRuns, ['Ingen kjøringer de siste 0 dagene.', 'Ingen kjøringer den siste dagen.', 'Ingen kjøringer de siste 2 dagene.']],
  ['usage.chartLabel', (count) => nb.usage.chartLabel(count, '0', '9k'), ['Tokens per dag de siste 0 dagene, fra 0 til 9k', 'Tokens per dag den siste dagen, fra 0 til 9k', 'Tokens per dag de siste 2 dagene, fra 0 til 9k']],
  ['usage.tip', (count) => nb.usage.tip('I dag', count, String(count), '$0,10'), ['I dag, 0 tokens, $0,10', 'I dag, 1 token, $0,10', 'I dag, 2 tokens, $0,10']],
  ['usage.month', (count) => nb.usage.month(count, String(count)), ['Denne måneden · 0 tokens', 'Denne måneden · 1 token', 'Denne måneden · 2 tokens']],
  ['usage.todayTokens', (count) => nb.usage.todayTokens(count, String(count)), ['I dag · 0 tokens', 'I dag · 1 token', 'I dag · 2 tokens']],
]

describe('count texts', () => {
  it.each(ENGLISH)('should count %s in English for 0, 1 and 2', (_, text, shown) => {
    expect(COUNTS.map((count) => text(count))).toEqual(shown)
  })

  it.each(NORWEGIAN)('should count %s in bokmål for 0, 1 and 2', (_, text, shown) => {
    expect(COUNTS.map((count) => text(count))).toEqual(shown)
  })
})
