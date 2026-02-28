import { ref, computed } from 'vue'

export function useVoiceInput() {
  const isListening = ref(false)
  const transcript = ref('')
  const error = ref<string | null>(null)
  let recognition: any = null

  const isAvailable = computed(() => {
    return 'webkitSpeechRecognition' in window || 'SpeechRecognition' in window
  })

  function startListening(lang: string = 'en-US') {
    if (!isAvailable.value) {
      error.value = 'Speech recognition not supported in this browser'
      return
    }

    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition
    recognition = new SpeechRecognition()
    recognition.continuous = false
    recognition.interimResults = true
    recognition.lang = lang === 'de' ? 'de-DE' : 'en-US'

    transcript.value = ''
    error.value = null

    recognition.onstart = () => {
      isListening.value = true
    }

    recognition.onresult = (event: any) => {
      let finalTranscript = ''
      let interimTranscript = ''

      for (let i = event.resultIndex; i < event.results.length; i++) {
        const result = event.results[i]
        if (result.isFinal) {
          finalTranscript += result[0].transcript
        } else {
          interimTranscript += result[0].transcript
        }
      }

      transcript.value = finalTranscript || interimTranscript
    }

    recognition.onerror = (event: any) => {
      isListening.value = false
      if (event.error === 'no-speech') {
        error.value = 'No speech detected. Try again.'
      } else if (event.error === 'not-allowed') {
        error.value = 'Microphone access denied'
      } else {
        error.value = `Speech recognition error: ${event.error}`
      }
    }

    recognition.onend = () => {
      isListening.value = false
    }

    recognition.start()
  }

  function stopListening() {
    if (recognition) {
      recognition.stop()
      recognition = null
    }
    isListening.value = false
  }

  return {
    startListening,
    stopListening,
    isListening,
    transcript,
    isAvailable,
    error
  }
}
