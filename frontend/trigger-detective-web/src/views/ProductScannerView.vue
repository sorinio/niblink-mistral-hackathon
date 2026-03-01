<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { api } from '@/services/api'
import { useSettingsStore } from '@/stores/settings'
import { useToastStore } from '@/stores/toast'
import type { ProductScanResultDto, ScannedIngredientDto } from '@/types'
import { SafetyRating } from '@/types'
import Icons from '@/components/icons/Icons.vue'
import ReadAloudButton from '@/components/voice/ReadAloudButton.vue'
import { useNarrationBuilder } from '@/composables/useNarrationBuilder'
import { useCamera } from '@/composables/useCamera'

const { t } = useI18n()
const { buildProductNarration } = useNarrationBuilder()
const settingsStore = useSettingsStore()
const toastStore = useToastStore()
const camera = useCamera()

const fileInput = ref<HTMLInputElement | null>(null)
const selectedFile = ref<File | null>(null)
const previewUrl = ref<string | null>(null)
const isDragging = ref(false)
const scanning = ref(false)
const scanResult = ref<ProductScanResultDto | null>(null)

const hasFile = computed(() => selectedFile.value !== null)

function openFilePicker() {
  fileInput.value?.click()
}

function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) selectFile(file)
}

function handleDrop(event: DragEvent) {
  isDragging.value = false
  const file = event.dataTransfer?.files?.[0]
  if (file) selectFile(file)
}

function selectFile(file: File) {
  const allowedTypes = ['image/jpeg', 'image/png', 'image/webp']
  if (!allowedTypes.includes(file.type)) {
    toastStore.error(t('food.photoUpload.invalidType'))
    return
  }
  if (file.size > 10 * 1024 * 1024) {
    toastStore.error(t('food.photoUpload.tooLarge'))
    return
  }

  selectedFile.value = file
  previewUrl.value = URL.createObjectURL(file)
  scanResult.value = null
}

function clearFile() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  selectedFile.value = null
  previewUrl.value = null
  scanResult.value = null
  if (fileInput.value) fileInput.value.value = ''
}

async function scanProduct() {
  if (!selectedFile.value) return
  scanning.value = true
  scanResult.value = null

  try {
    const result = await api.scanProductLabel(selectedFile.value, settingsStore.useLocalAi)
    scanResult.value = result

    if (!result.success) {
      toastStore.error(result.errorMessage || t('scanner.scanFailed'))
    }
  } catch (err: any) {
    toastStore.error(t('scanner.scanFailed'))
  } finally {
    scanning.value = false
  }
}

function scanAnother() {
  clearFile()
}

function ratingColor(rating: SafetyRating): string {
  switch (rating) {
    case SafetyRating.Green: return 'text-success'
    case SafetyRating.Yellow: return 'text-yellow-500'
    case SafetyRating.Red: return 'text-alert'
    default: return 'text-text-muted'
  }
}

function ratingBg(rating: SafetyRating): string {
  switch (rating) {
    case SafetyRating.Green: return 'bg-success/10'
    case SafetyRating.Yellow: return 'bg-yellow-500/10'
    case SafetyRating.Red: return 'bg-alert/10'
    default: return 'bg-bg'
  }
}

function ratingLabel(rating: SafetyRating): string {
  switch (rating) {
    case SafetyRating.Green: return t('scanner.safe')
    case SafetyRating.Yellow: return t('scanner.caution')
    case SafetyRating.Red: return t('scanner.avoid')
    default: return ''
  }
}

const readbackText = computed(() => {
  if (!scanResult.value?.success) return ''
  return buildProductNarration(scanResult.value)
})

function ingredientIcon(ingredient: ScannedIngredientDto): string {
  switch (ingredient.rating) {
    case SafetyRating.Green: return '&#10003;'
    case SafetyRating.Yellow: return '!'
    case SafetyRating.Red: return '&#10007;'
    default: return '-'
  }
}

function openCamera() {
  camera.open()
}

function capturePhoto() {
  const file = camera.capture()
  if (file) {
    selectFile(file)
    camera.close()
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div>
      <h1 class="text-2xl font-semibold text-text flex items-center gap-3">
        <Icons name="scanner" :size="28" />
        {{ t('scanner.title') }}
      </h1>
      <p class="mt-1 text-text-muted">{{ t('scanner.subtitle') }}</p>
      <p v-if="settingsStore.useLocalAi" class="text-xs text-accent mt-0.5">
        {{ t('chat.localMode') }}
      </p>
    </div>

    <!-- Local AI quality warning -->
    <div v-if="settingsStore.useLocalAi" class="flex items-start gap-2 px-3 py-2 rounded-lg bg-yellow-500/10 border border-yellow-500/20 text-sm text-yellow-700 dark:text-yellow-400">
      <span class="shrink-0 mt-0.5">&#9888;</span>
      <span>{{ t('scanner.localWarning') }}</span>
    </div>

    <!-- Result View -->
    <template v-if="scanResult?.success">
      <!-- Traffic Light -->
      <div :class="['card border-2 text-center py-8', {
        'border-success/40 bg-success/5': scanResult.overallRating === SafetyRating.Green,
        'border-yellow-500/40 bg-yellow-500/5': scanResult.overallRating === SafetyRating.Yellow,
        'border-alert/40 bg-alert/5': scanResult.overallRating === SafetyRating.Red
      }]">
        <!-- Traffic Light Circles -->
        <div class="flex justify-center gap-4 mb-6">
          <div :class="['w-16 h-16 rounded-full border-4 transition-all duration-500',
            scanResult.overallRating === SafetyRating.Red
              ? 'bg-red-500 border-red-600 shadow-lg shadow-red-500/30 scale-110'
              : 'bg-red-500/15 border-red-500/20'
          ]"></div>
          <div :class="['w-16 h-16 rounded-full border-4 transition-all duration-500',
            scanResult.overallRating === SafetyRating.Yellow
              ? 'bg-yellow-400 border-yellow-500 shadow-lg shadow-yellow-400/30 scale-110'
              : 'bg-yellow-400/15 border-yellow-400/20'
          ]"></div>
          <div :class="['w-16 h-16 rounded-full border-4 transition-all duration-500',
            scanResult.overallRating === SafetyRating.Green
              ? 'bg-green-500 border-green-600 shadow-lg shadow-green-500/30 scale-110'
              : 'bg-green-500/15 border-green-500/20'
          ]"></div>
        </div>

        <h2 v-if="scanResult.productName" class="text-lg font-semibold text-text mb-1">
          {{ scanResult.productName }}
        </h2>
        <p :class="['text-xl font-bold', ratingColor(scanResult.overallRating)]">
          {{ scanResult.headline }}
        </p>
        <p class="text-text-muted mt-2 text-sm max-w-md mx-auto">
          {{ scanResult.explanation }}
        </p>
        <div class="mt-4">
          <ReadAloudButton :text="readbackText" />
        </div>
      </div>

      <!-- Warnings -->
      <div v-if="scanResult.warnings.length > 0" class="card border-alert/20 bg-alert/5">
        <h3 class="font-medium text-alert mb-3">{{ t('scanner.warnings') }}</h3>
        <ul class="space-y-2">
          <li
            v-for="(warning, idx) in scanResult.warnings"
            :key="idx"
            class="flex items-start gap-2 text-sm text-text"
          >
            <span class="text-alert mt-0.5 shrink-0">&#9888;</span>
            <span>{{ warning }}</span>
          </li>
        </ul>
      </div>

      <!-- Ingredients List -->
      <div class="card">
        <h3 class="font-medium text-text mb-3">
          {{ t('scanner.ingredientAnalysis') }} ({{ scanResult.ingredients.length }})
        </h3>
        <div class="space-y-2">
          <div
            v-for="ingredient in scanResult.ingredients"
            :key="ingredient.name"
            :class="['flex items-center gap-3 p-3 rounded-lg', ratingBg(ingredient.rating)]"
          >
            <!-- Rating indicator -->
            <div :class="['w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold shrink-0', {
              'bg-success/20 text-success': ingredient.rating === SafetyRating.Green,
              'bg-yellow-500/20 text-yellow-600': ingredient.rating === SafetyRating.Yellow,
              'bg-alert/20 text-alert': ingredient.rating === SafetyRating.Red
            }]" v-html="ingredientIcon(ingredient)">
            </div>

            <!-- Info -->
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-medium text-text capitalize">{{ ingredient.name }}</span>
                <span
                  v-if="ingredient.isPersonalTrigger"
                  class="text-xs px-1.5 py-0.5 bg-alert/15 text-alert rounded-full"
                >
                  {{ t('scanner.personalTrigger') }}
                </span>
              </div>
              <p v-if="ingredient.reason" class="text-sm text-text-muted mt-0.5">
                {{ ingredient.reason }}
              </p>
            </div>

            <!-- Rating label -->
            <span :class="['text-xs font-medium px-2 py-1 rounded-full shrink-0', {
              'bg-success/20 text-success': ingredient.rating === SafetyRating.Green,
              'bg-yellow-500/20 text-yellow-600': ingredient.rating === SafetyRating.Yellow,
              'bg-alert/20 text-alert': ingredient.rating === SafetyRating.Red
            }]">
              {{ ratingLabel(ingredient.rating) }}
            </span>
          </div>
        </div>
      </div>

      <!-- Actions -->
      <div class="flex gap-3">
        <button
          @click="scanAnother"
          class="flex-1 py-3 px-4 bg-primary text-white rounded-lg font-medium hover:bg-primary/90 transition-colors"
        >
          {{ t('scanner.scanAnother') }}
        </button>
      </div>
    </template>

    <!-- Upload / Scan View -->
    <template v-else>
      <input
        ref="fileInput"
        type="file"
        accept="image/jpeg,image/png,image/webp"
        capture="environment"
        class="hidden"
        @change="handleFileSelect"
      />

      <!-- Preview state -->
      <div v-if="hasFile" class="card">
        <div class="relative rounded-lg overflow-hidden bg-bg">
          <img
            :src="previewUrl!"
            :alt="t('scanner.photoPreview')"
            class="w-full max-h-64 object-contain"
          />
          <button
            type="button"
            @click="clearFile"
            class="absolute top-2 right-2 p-1.5 bg-black/50 hover:bg-black/70 rounded-full text-white transition-colors"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <button
          @click="scanProduct"
          :disabled="scanning"
          class="mt-4 w-full py-3 px-4 bg-primary text-white rounded-lg font-medium hover:bg-primary/90 transition-colors disabled:opacity-50"
        >
          <span v-if="scanning" class="flex items-center justify-center gap-2">
            <svg class="animate-spin h-5 w-5" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
            {{ t('scanner.analyzing') }}
          </span>
          <span v-else class="flex items-center justify-center gap-2">
            <Icons name="scanner" :size="20" />
            {{ t('scanner.scanButton') }}
          </span>
        </button>
      </div>

      <!-- Empty state - camera + upload buttons -->
      <div v-else class="space-y-4">
        <div class="flex gap-3">
          <!-- Camera button -->
          <button
            v-if="camera.isAvailable"
            @click="openCamera"
            class="flex-1 border-2 border-dashed border-border rounded-lg p-6 text-center cursor-pointer transition-all hover:border-primary/50 hover:bg-primary/5"
          >
            <div class="mb-3 mx-auto w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center text-primary">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                <path stroke-linecap="round" stroke-linejoin="round" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <p class="font-medium text-text text-sm">{{ t('scanner.takePhoto') }}</p>
          </button>

          <!-- Upload button -->
          <div
            @click="openFilePicker"
            @dragover.prevent="isDragging = true"
            @dragleave="isDragging = false"
            @drop.prevent="handleDrop"
            :class="[
              'flex-1 border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-all',
              isDragging
                ? 'border-primary bg-primary/5'
                : 'border-border hover:border-primary/50 hover:bg-primary/5'
            ]"
          >
            <div class="mb-3 mx-auto w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center text-primary">
              <Icons name="scanner" :size="24" />
            </div>
            <p class="font-medium text-text text-sm">{{ t('scanner.uploadLabel') }}</p>
          </div>
        </div>

        <p class="text-text-muted text-sm text-center">{{ t('scanner.uploadHint') }}</p>
      </div>

      <!-- Camera Viewfinder Overlay -->
      <Teleport to="body">
        <div
          v-if="camera.isOpen.value"
          class="fixed inset-0 z-50 bg-black flex flex-col"
        >
          <!-- Camera feed -->
          <video
            :ref="(el: any) => { camera.videoRef.value = el }"
            autoplay
            playsinline
            class="flex-1 object-cover"
          ></video>

          <!-- Controls -->
          <div class="absolute bottom-0 inset-x-0 pb-8 pt-4 bg-gradient-to-t from-black/80 to-transparent flex items-center justify-center gap-8">
            <!-- Close button -->
            <button
              @click="camera.close()"
              class="w-12 h-12 rounded-full bg-white/20 text-white flex items-center justify-center hover:bg-white/30 transition-colors"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>

            <!-- Capture button -->
            <button
              @click="capturePhoto"
              class="w-16 h-16 rounded-full bg-white border-4 border-white/50 flex items-center justify-center hover:scale-105 transition-transform"
            >
              <div class="w-12 h-12 rounded-full bg-white"></div>
            </button>

            <!-- Spacer for alignment -->
            <div class="w-12"></div>
          </div>

          <!-- Error -->
          <p v-if="camera.error.value" class="absolute top-4 inset-x-4 text-center text-white bg-alert/80 rounded-lg p-3 text-sm">
            {{ camera.error.value }}
          </p>
        </div>
      </Teleport>

      <!-- Error result -->
      <div v-if="scanResult && !scanResult.success" class="card border-alert/20 bg-alert/5">
        <p class="text-alert font-medium">{{ scanResult.headline }}</p>
        <p class="text-text-muted text-sm mt-1">{{ scanResult.explanation }}</p>
      </div>
    </template>
  </div>
</template>
