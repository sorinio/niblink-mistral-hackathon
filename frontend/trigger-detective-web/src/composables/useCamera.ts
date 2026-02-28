import { ref } from 'vue'

export function useCamera() {
  const isOpen = ref(false)
  const error = ref<string | null>(null)
  const stream = ref<MediaStream | null>(null)
  const videoRef = ref<HTMLVideoElement | null>(null)

  const isAvailable = typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia

  async function open() {
    if (!isAvailable) {
      error.value = 'Camera not supported in this browser'
      return
    }

    error.value = null

    try {
      const mediaStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1920 }, height: { ideal: 1080 } }
      })

      stream.value = mediaStream
      isOpen.value = true

      // Attach stream to video element after next tick
      requestAnimationFrame(() => {
        if (videoRef.value && stream.value) {
          videoRef.value.srcObject = stream.value
        }
      })
    } catch (err: any) {
      if (err.name === 'NotAllowedError') {
        error.value = 'Camera access denied'
      } else if (err.name === 'NotFoundError') {
        error.value = 'No camera found'
      } else {
        error.value = `Camera error: ${err.message}`
      }
    }
  }

  function capture(): File | null {
    if (!videoRef.value || !stream.value) return null

    const video = videoRef.value
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight

    const ctx = canvas.getContext('2d')
    if (!ctx) return null

    ctx.drawImage(video, 0, 0)

    // Convert canvas to blob synchronously via dataURL
    const dataUrl = canvas.toDataURL('image/jpeg', 0.9)
    const byteString = atob(dataUrl.split(',')[1] ?? '')
    const arrayBuffer = new ArrayBuffer(byteString.length)
    const uint8Array = new Uint8Array(arrayBuffer)
    for (let i = 0; i < byteString.length; i++) {
      uint8Array[i] = byteString.charCodeAt(i)
    }
    const blob = new Blob([arrayBuffer], { type: 'image/jpeg' })
    return new File([blob], `capture-${Date.now()}.jpg`, { type: 'image/jpeg' })
  }

  function close() {
    if (stream.value) {
      stream.value.getTracks().forEach(track => track.stop())
      stream.value = null
    }
    if (videoRef.value) {
      videoRef.value.srcObject = null
    }
    isOpen.value = false
    error.value = null
  }

  return {
    isAvailable,
    isOpen,
    error,
    videoRef,
    open,
    capture,
    close
  }
}
