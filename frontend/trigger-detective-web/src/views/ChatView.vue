<script setup lang="ts">
import { ref, nextTick, watch, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useChatStore } from '@/stores/chat'
import { useSettingsStore } from '@/stores/settings'
import ChatMessage from '@/components/chat/ChatMessage.vue'

const { t } = useI18n()
const chatStore = useChatStore()
const settingsStore = useSettingsStore()
const inputText = ref('')
const messagesContainer = ref<HTMLElement | null>(null)

const suggestedQuestions = computed(() => [
  t('chat.suggestions.dinner'),
  t('chat.suggestions.iron'),
  t('chat.suggestions.pasta'),
  t('chat.suggestions.avoid')
])

function send() {
  const msg = inputText.value.trim()
  if (!msg || chatStore.isStreaming) return
  inputText.value = ''
  chatStore.sendMessage(msg)
}

function askSuggested(question: string) {
  inputText.value = question
  send()
}

// Auto-scroll on new content
watch(
  [() => chatStore.messages.length, () => chatStore.currentStreamContent],
  () => {
    nextTick(() => {
      messagesContainer.value?.scrollTo({
        top: messagesContainer.value.scrollHeight,
        behavior: 'smooth'
      })
    })
  }
)
</script>

<template>
  <div class="flex flex-col h-[calc(100vh-12rem)] md:h-[calc(100vh-7rem)]">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div>
        <h1 class="text-2xl font-semibold text-text">{{ t('chat.title') }}</h1>
        <p v-if="settingsStore.useLocalAi" class="text-xs text-accent mt-0.5">
          {{ t('chat.localMode') }}
        </p>
      </div>
      <button
        v-if="chatStore.hasMessages"
        @click="chatStore.clearChat"
        class="px-3 py-1.5 text-sm rounded-lg border border-border text-text-muted
          hover:bg-alert/10 hover:text-alert hover:border-alert/30 transition-colors"
      >
        {{ t('chat.clearChat') }}
      </button>
    </div>

    <!-- Messages area -->
    <div ref="messagesContainer" class="flex-1 overflow-y-auto space-y-4 pb-4">
      <!-- Empty state -->
      <div
        v-if="!chatStore.hasMessages && !chatStore.isStreaming"
        class="flex flex-col items-center justify-center h-full text-center px-4"
      >
        <div class="p-4 bg-primary/10 rounded-full mb-4">
          <svg class="w-10 h-10 text-primary" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
        </div>
        <h2 class="text-lg font-medium text-text mb-2">{{ t('chat.emptyTitle') }}</h2>
        <p class="text-text-muted mb-6 max-w-md text-sm">{{ t('chat.emptyDescription') }}</p>
        <div class="flex flex-wrap gap-2 justify-center max-w-lg">
          <button
            v-for="q in suggestedQuestions"
            :key="q"
            @click="askSuggested(q)"
            class="px-4 py-2 rounded-full border border-border text-sm text-text
              hover:bg-primary/10 hover:border-primary/30 transition-colors"
          >
            {{ q }}
          </button>
        </div>
      </div>

      <!-- Messages -->
      <template v-else>
        <ChatMessage
          v-for="(msg, i) in chatStore.messages"
          :key="i"
          :role="msg.role"
          :content="msg.content"
        />

        <!-- Thinking indicator (before first token arrives) -->
        <div
          v-if="chatStore.isStreaming && !chatStore.currentStreamContent"
          class="flex justify-start"
        >
          <div class="max-w-[85%] px-4 py-3 rounded-2xl rounded-bl-md bg-surface border border-border">
            <div class="flex items-center gap-2 text-sm text-text-muted">
              <span class="thinking-dots flex gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-primary/60" />
                <span class="w-1.5 h-1.5 rounded-full bg-primary/60" />
                <span class="w-1.5 h-1.5 rounded-full bg-primary/60" />
              </span>
              {{ t('chat.thinking') }}
            </div>
          </div>
        </div>

        <!-- Streaming message (once tokens arrive) -->
        <ChatMessage
          v-if="chatStore.isStreaming && chatStore.currentStreamContent"
          role="assistant"
          :content="chatStore.currentStreamContent"
          :is-streaming="true"
        />
      </template>
    </div>

    <!-- Error -->
    <div
      v-if="chatStore.error"
      class="text-sm text-alert bg-alert/10 px-4 py-2 rounded-lg mb-2"
    >
      {{ chatStore.error }}
    </div>

    <!-- Input area -->
    <div class="flex gap-2 pt-3 border-t border-border">
      <input
        v-model="inputText"
        @keydown.enter="send"
        :placeholder="t('chat.inputPlaceholder')"
        :disabled="chatStore.isStreaming"
        class="flex-1 px-4 py-3 rounded-xl border border-border bg-surface
          text-text placeholder-text-muted focus:outline-none focus:ring-2
          focus:ring-primary/30 disabled:opacity-50"
      />
      <button
        @click="chatStore.isStreaming ? chatStore.stopStreaming() : send()"
        :class="[
          'px-4 py-3 rounded-xl font-medium transition-colors',
          chatStore.isStreaming
            ? 'bg-alert/10 text-alert hover:bg-alert/20'
            : 'bg-primary text-white hover:bg-primary/90 disabled:opacity-50'
        ]"
        :disabled="!chatStore.isStreaming && !inputText.trim()"
      >
        {{ chatStore.isStreaming ? t('chat.stop') : t('chat.send') }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.thinking-dots span {
  animation: bounce 1.4s infinite ease-in-out;
}
.thinking-dots span:nth-child(1) { animation-delay: 0s; }
.thinking-dots span:nth-child(2) { animation-delay: 0.2s; }
.thinking-dots span:nth-child(3) { animation-delay: 0.4s; }

@keyframes bounce {
  0%, 80%, 100% { transform: scale(0.6); opacity: 0.4; }
  40% { transform: scale(1); opacity: 1; }
}
</style>
