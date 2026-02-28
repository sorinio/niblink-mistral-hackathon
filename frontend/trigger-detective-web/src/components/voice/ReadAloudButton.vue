<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useVoiceReadback } from '@/composables/useVoiceReadback'

defineProps<{
  text: string
  size?: 'sm' | 'md'
}>()

const { t } = useI18n()
const { speak, stop, isPlaying, isLoading, isAvailable } = useVoiceReadback()

function toggle(text: string) {
  if (isPlaying.value) {
    stop()
  } else {
    speak(text)
  }
}
</script>

<template>
  <button
    v-if="isAvailable"
    @click="toggle(text)"
    :disabled="isLoading"
    :title="isPlaying ? t('voice.stop') : t('voice.readAloud')"
    :class="[
      'inline-flex items-center gap-1.5 rounded-lg transition-all',
      size === 'sm'
        ? 'px-2 py-1 text-xs'
        : 'px-3 py-1.5 text-sm',
      isPlaying
        ? 'bg-primary/15 text-primary'
        : 'bg-bg hover:bg-primary/10 text-text-muted hover:text-primary'
    ]"
  >
    <!-- Loading spinner -->
    <svg v-if="isLoading" class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
    </svg>
    <!-- Speaker icon -->
    <svg v-else-if="isPlaying" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
      <path stroke-linecap="round" stroke-linejoin="round" d="M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
      <path stroke-linecap="round" stroke-linejoin="round" d="M17 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2" />
    </svg>
    <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
      <path stroke-linecap="round" stroke-linejoin="round" d="M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
    </svg>
    <span>{{ isPlaying ? t('voice.stop') : t('voice.readAloud') }}</span>
  </button>
</template>
