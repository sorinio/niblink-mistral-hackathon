import { useI18n } from 'vue-i18n'
import type { ProductScanResultDto } from '@/types'
import { SafetyRating } from '@/types'

export function useNarrationBuilder() {
  const { locale } = useI18n()

  function buildProductNarration(result: ProductScanResultDto): string {
    if (!result.success) return ''
    const lang = locale.value
    const parts: string[] = []

    parts.push(buildProductIntro(result, lang))
    parts.push(buildVerdict(result, lang))

    const flagged = buildFlaggedIngredients(result, lang)
    if (flagged) parts.push(flagged)

    const triggers = buildPersonalTriggers(result, lang)
    if (triggers) parts.push(triggers)

    const warnings = buildWarnings(result, lang)
    if (warnings) parts.push(warnings)

    parts.push(buildClosing(result, lang))

    return parts.join(' <break time="0.8s"/> ')
  }

  function buildInsightNarration(aiSummary: string): string {
    const lang = locale.value

    // Strip markdown formatting
    let clean = aiSummary
      .replace(/#{1,6}\s*/g, '')
      .replace(/\*\*([^*]+)\*\*/g, '$1')
      .replace(/\*([^*]+)\*/g, '$1')
      .replace(/^[-*]\s+/gm, '')
      .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
      .replace(/`([^`]+)`/g, '$1')
      .trim()

    const intro = lang === 'de'
      ? 'Hier ist eine Zusammenfassung deiner Muster und Erkenntnisse.'
      : "Here's a summary of your patterns and insights."

    // Cap at ~1800 chars total to stay within ElevenLabs free tier per-request budget
    const maxBody = 1800 - intro.length - 30 // 30 for break tag + spaces
    if (clean.length > maxBody) {
      // Truncate at last sentence boundary
      const truncated = clean.slice(0, maxBody)
      const lastPeriod = truncated.lastIndexOf('.')
      clean = lastPeriod > maxBody * 0.5 ? truncated.slice(0, lastPeriod + 1) : truncated
    }

    return `${intro} <break time="0.8s"/> ${clean}`
  }

  return { buildProductNarration, buildInsightNarration }
}

function buildProductIntro(result: ProductScanResultDto, lang: string): string {
  if (lang === 'de') {
    return result.productName
      ? `Du hast "${result.productName}" gescannt.`
      : 'Ich habe das Produkt analysiert.'
  }
  return result.productName
    ? `You scanned "${result.productName}".`
    : "I've analyzed the product."
}

function buildVerdict(result: ProductScanResultDto, lang: string): string {
  const flaggedCount = result.ingredients.filter(i => i.rating !== SafetyRating.Green).length
  const totalCount = result.ingredients.length

  if (lang === 'de') {
    switch (result.overallRating) {
      case SafetyRating.Green:
        return `Gute Nachricht — dieses Produkt sieht sicher für dich aus. Alle ${totalCount} Zutaten sind unbedenklich.`
      case SafetyRating.Yellow:
        return `Bei diesem Produkt ist etwas Vorsicht geboten. ${flaggedCount} von ${totalCount} Zutaten verdienen deine Aufmerksamkeit.`
      case SafetyRating.Red:
        return `Achtung — dieses Produkt ist problematisch für dich. ${flaggedCount} von ${totalCount} Zutaten sind bedenklich.`
      default:
        return result.headline
    }
  }

  switch (result.overallRating) {
    case SafetyRating.Green:
      return `Good news — this product looks safe for you. All ${totalCount} ingredients check out fine.`
    case SafetyRating.Yellow:
      return `For this product, some caution is advised. ${flaggedCount} of ${totalCount} ingredients deserve your attention.`
    case SafetyRating.Red:
      return `Heads up — this product is problematic for you. ${flaggedCount} of ${totalCount} ingredients are concerning.`
    default:
      return result.headline
  }
}

function buildFlaggedIngredients(result: ProductScanResultDto, lang: string): string | null {
  const flagged = result.ingredients
    .filter(i => i.rating !== SafetyRating.Green)
    .slice(0, 5) // Cap at 5 to avoid excessive length

  if (flagged.length === 0) return null

  const items = flagged.map(ingredient => {
    const ratingWord = ingredientRatingWord(ingredient.rating, lang)
    const reason = ingredient.reason ? ` — ${ingredient.reason}` : ''
    return `${ingredient.name}: ${ratingWord}${reason}.`
  })

  const preamble = lang === 'de'
    ? 'Hier die Details zu den markierten Zutaten:'
    : 'Here are the details on the flagged ingredients:'

  return preamble + ' <break time="0.5s"/> ' + items.join(' <break time="0.6s"/> ')
}

function ingredientRatingWord(rating: number, lang: string): string {
  if (lang === 'de') {
    return rating === SafetyRating.Yellow ? 'Vorsicht' : 'meiden'
  }
  return rating === SafetyRating.Yellow ? 'use caution' : 'best avoided'
}

function buildPersonalTriggers(result: ProductScanResultDto, lang: string): string | null {
  const triggers = result.ingredients.filter(i => i.isPersonalTrigger)
  if (triggers.length === 0) return null

  const names = triggers.map(t => t.name).join(lang === 'de' ? ' und ' : ' and ')

  if (lang === 'de') {
    return triggers.length === 1
      ? `Wichtig: ${names} ist einer deiner persönlichen Trigger basierend auf deinen bisherigen Daten.`
      : `Wichtig: ${names} sind persönliche Trigger, die wir in deinen Daten gefunden haben.`
  }

  return triggers.length === 1
    ? `Important: ${names} is one of your personal triggers based on your tracking data.`
    : `Important: ${names} are personal triggers we've identified from your tracking data.`
}

function buildWarnings(result: ProductScanResultDto, lang: string): string | null {
  if (result.warnings.length === 0) return null

  const preamble = lang === 'de'
    ? (result.warnings.length === 1 ? 'Noch eine Warnung:' : `Es gibt ${result.warnings.length} Warnungen:`)
    : (result.warnings.length === 1 ? 'One more thing to note:' : `There are ${result.warnings.length} warnings:`)

  const items = result.warnings.join('. <break time="0.4s"/> ')
  return `${preamble} <break time="0.4s"/> ${items}.`
}

function buildClosing(result: ProductScanResultDto, lang: string): string {
  if (lang === 'de') {
    switch (result.overallRating) {
      case SafetyRating.Green:
        return 'Du kannst dieses Produkt bedenkenlos genießen.'
      case SafetyRating.Yellow:
        return 'Am besten entscheidest du selbst, ob du es verwenden möchtest.'
      case SafetyRating.Red:
        return 'Ich würde dir empfehlen, nach einer Alternative zu suchen.'
      default:
        return ''
    }
  }

  switch (result.overallRating) {
    case SafetyRating.Green:
      return 'You can enjoy this product without concern.'
    case SafetyRating.Yellow:
      return "It's your call whether to go with it."
    case SafetyRating.Red:
      return "I'd suggest looking for an alternative."
    default:
      return ''
  }
}
