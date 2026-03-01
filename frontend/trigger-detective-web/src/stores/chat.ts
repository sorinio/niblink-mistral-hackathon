import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { ChatMessageDto } from '@/types'
import { api } from '@/services/api'
import { useSettingsStore } from '@/stores/settings'

export const useChatStore = defineStore('chat', () => {
  const messages = ref<ChatMessageDto[]>([])
  const isStreaming = ref(false)
  const currentStreamContent = ref('')
  const error = ref<string | null>(null)
  let abortController: AbortController | null = null

  const hasMessages = computed(() => messages.value.length > 0)

  async function sendMessage(userMessage: string) {
    messages.value.push({ role: 'user', content: userMessage })

    isStreaming.value = true
    currentStreamContent.value = ''
    error.value = null

    const settingsStore = useSettingsStore()

    abortController = api.streamChatMessage(
      {
        message: userMessage,
        history: messages.value.slice(0, -1),
        useLocal: settingsStore.useLocalAi
      },
      (token) => {
        currentStreamContent.value += token
      },
      () => {
        if (currentStreamContent.value) {
          messages.value.push({
            role: 'assistant',
            content: currentStreamContent.value
          })
        }
        currentStreamContent.value = ''
        isStreaming.value = false
      },
      (err) => {
        error.value = err.message || 'Chat failed. Please try again.'
        if (currentStreamContent.value) {
          messages.value.push({
            role: 'assistant',
            content: currentStreamContent.value
          })
          currentStreamContent.value = ''
        }
        isStreaming.value = false
      }
    )
  }

  function stopStreaming() {
    abortController?.abort()
    if (currentStreamContent.value) {
      messages.value.push({
        role: 'assistant',
        content: currentStreamContent.value
      })
      currentStreamContent.value = ''
    }
    isStreaming.value = false
  }

  function clearChat() {
    messages.value = []
    currentStreamContent.value = ''
    error.value = null
    isStreaming.value = false
  }

  return {
    messages,
    isStreaming,
    currentStreamContent,
    error,
    hasMessages,
    sendMessage,
    stopStreaming,
    clearChat
  }
})
