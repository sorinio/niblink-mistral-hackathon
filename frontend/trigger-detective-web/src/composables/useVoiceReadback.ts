import { ref } from 'vue'

const ELEVENLABS_API_KEY = import.meta.env.VITE_ELEVENLABS_API_KEY || ''
const VOICE_ID = import.meta.env.VITE_ELEVENLABS_VOICE_ID || 'EXAVITQu4vr4xnSDxMaL' // "Sarah" - warm, clear

export function useVoiceReadback() {
  const isPlaying = ref(false)
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  let currentAudio: HTMLAudioElement | null = null

  const isAvailable = !!ELEVENLABS_API_KEY

  async function speak(text: string) {
    if (!ELEVENLABS_API_KEY) {
      error.value = 'ElevenLabs API key not configured'
      return
    }

    // Stop any currently playing audio
    stop()

    isLoading.value = true
    error.value = null

    try {
      const response = await fetch(
        `https://api.elevenlabs.io/v1/text-to-speech/${VOICE_ID}`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'xi-api-key': ELEVENLABS_API_KEY
          },
          body: JSON.stringify({
            text,
            model_id: 'eleven_multilingual_v2',
            voice_settings: {
              stability: 0.6,
              similarity_boost: 0.75,
              style: 0.2
            }
          })
        }
      )

      if (!response.ok) {
        const errText = await response.text()
        throw new Error(`ElevenLabs API error: ${response.status} ${errText}`)
      }

      const audioBlob = await response.blob()
      const audioUrl = URL.createObjectURL(audioBlob)
      currentAudio = new Audio(audioUrl)

      currentAudio.onplay = () => {
        isPlaying.value = true
        isLoading.value = false
      }

      currentAudio.onended = () => {
        isPlaying.value = false
        URL.revokeObjectURL(audioUrl)
        currentAudio = null
      }

      currentAudio.onerror = () => {
        isPlaying.value = false
        isLoading.value = false
        error.value = 'Audio playback failed'
        URL.revokeObjectURL(audioUrl)
        currentAudio = null
      }

      await currentAudio.play()
    } catch (err: any) {
      isLoading.value = false
      isPlaying.value = false
      error.value = err.message || 'Failed to generate speech'
    }
  }

  function stop() {
    if (currentAudio) {
      currentAudio.pause()
      currentAudio.currentTime = 0
      currentAudio = null
    }
    isPlaying.value = false
    isLoading.value = false
  }

  return {
    speak,
    stop,
    isPlaying,
    isLoading,
    isAvailable,
    error
  }
}
