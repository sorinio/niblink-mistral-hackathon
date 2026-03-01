<script setup lang="ts">
import { computed } from 'vue'
import { marked } from 'marked'

marked.setOptions({ breaks: true, gfm: true })

const props = defineProps<{
  role: 'user' | 'assistant'
  content: string
  isStreaming?: boolean
}>()

const renderedContent = computed(() => {
  if (props.role === 'user') return ''
  return marked.parse(props.content) as string
})
</script>

<template>
  <div :class="['flex', role === 'user' ? 'justify-end' : 'justify-start']">
    <div
      v-if="role === 'user'"
      class="max-w-[85%] px-4 py-3 rounded-2xl text-sm leading-relaxed whitespace-pre-wrap bg-primary text-white rounded-br-md"
    >
      {{ content }}
    </div>
    <div
      v-else
      class="max-w-[85%] px-4 py-3 rounded-2xl text-sm leading-relaxed bg-surface border border-border text-text rounded-bl-md"
    >
      <div class="chat-md" v-html="renderedContent"></div>
      <span
        v-if="isStreaming"
        class="inline-block w-1.5 h-4 bg-current opacity-75 animate-pulse ml-0.5 align-middle"
      />
    </div>
  </div>
</template>

<style scoped>
.chat-md :deep(p) {
  margin-bottom: 0.5rem;
}
.chat-md :deep(p:last-child) {
  margin-bottom: 0;
}
.chat-md :deep(strong) {
  font-weight: 600;
  color: var(--color-text);
}
.chat-md :deep(em) {
  font-style: italic;
  color: var(--color-text-muted);
}
.chat-md :deep(ul),
.chat-md :deep(ol) {
  margin-left: 1.25rem;
  margin-bottom: 0.5rem;
}
.chat-md :deep(ul) {
  list-style-type: disc;
}
.chat-md :deep(ol) {
  list-style-type: decimal;
}
.chat-md :deep(li) {
  margin-bottom: 0.25rem;
}
.chat-md :deep(h2),
.chat-md :deep(h3) {
  font-weight: 600;
  margin-top: 0.75rem;
  margin-bottom: 0.5rem;
}
.chat-md :deep(h2) {
  font-size: 1rem;
}
.chat-md :deep(h3) {
  font-size: 0.9375rem;
}
.chat-md :deep(code) {
  background-color: var(--color-border);
  padding: 0.125rem 0.25rem;
  border-radius: 0.25rem;
  font-size: 0.8125rem;
}
</style>
