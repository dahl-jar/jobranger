import styles from '@/features/landing/LandingPage.module.css'
import { demoAds, DEMO_MATCH_COUNT, type DemoAd } from '@/shared/demo/demoData'
import { useText } from '@/shared/i18n/languageContext'
import { CheckList } from '@/shared/ui/CheckList'
import { FitMark } from '@/shared/ui/FitMark'
import { SectionLabel } from '@/shared/ui/SectionLabel'

function ExampleAd({ ad }: { ad: DemoAd }) {
  return (
    <article className={styles.ad}>
      <div className={styles.due}>
        <div className={styles.dueDate}>{ad.due}</div>
        {ad.dueNote !== undefined && <div className={styles.dueNote}>{ad.dueNote}</div>}
        <div className={styles.adFit}>
          <FitMark fit={ad.fit} />
        </div>
      </div>
      <div className={styles.adBody}>
        <div className={styles.adMeta}>{[ad.company, ad.place, ad.source].join(' · ')}</div>
        <h3 className={styles.adTitle}>{ad.title}</h3>
        {ad.summary !== undefined && <p className={styles.adSummary}>{ad.summary}</p>}
        {ad.checks.length > 0 && <CheckList checks={ad.checks} />}
      </div>
    </article>
  )
}

export function ExampleMatch() {
  const text = useText()
  const { havbris, kystlinje } = demoAds(text)
  const example = text.landing.example

  return (
    <section aria-label={example.label} className={styles.example}>
      <SectionLabel aside={<span className={styles.exampleTag}>{example.tag}</span>}>{example.heading(DEMO_MATCH_COUNT)}</SectionLabel>
      {[havbris, kystlinje].map((ad) => (
        <ExampleAd key={ad.title} ad={ad} />
      ))}
      <p className={styles.hiddenLine}>{text.demo.hiddenLine}</p>
    </section>
  )
}
