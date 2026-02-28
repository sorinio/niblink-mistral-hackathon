<script setup lang="ts">
import { watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVoiceInput } from '@/composables/useVoiceInput'

const emit = defineEmits<{
  (e: 'transcript', text: string): void
}>()

const { t, locale } = useI18n()
const { startListening, stopListening, isListening, transcript, isAvailable, error } = useVoiceInput()

function toggleMic() {
  if (isListening.value) {
    stopListening()
  } else {
    startListening(locale.value)
  }
}

// Emit final transcript when listening stops
watch(isListening, (listening) => {
  if (!listening && transcript.value.trim()) {
    emit('transcript', transcript.value.trim())
  }
})
</script>

<template>
  <div v-if="isAvailable" class="voice-input">
    <button
      @click="toggleMic"
      :title="isListening ? t('voice.stopListening') : t('voice.startListening')"
      :class="[
        'p-3 rounded-full transition-all duration-200',
        isListening
          ? 'bg-alert text-white shadow-lg shadow-alert/30 animate-pulse'
          : 'bg-primary/10 text-primary hover:bg-primary/20'
      ]"
    >
      <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
      </svg>
    </button>

    <!-- Live transcript -->
    <p v-if="isListening && transcript" class="text-sm text-text-muted mt-2 italic">
      "{{ transcript }}"
    </p>
    <p v-if="isListening && !transcript" class="text-sm text-text-muted mt-2">
      {{ t('voice.listening') }}
    </p>

    <p v-if="error" class="text-sm text-alert mt-1">{{ error }}</p>
  </div>
</template>
