<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'

// Lector de QR/código de barras con la cámara usando la API nativa
// BarcodeDetector (sin librerías). Solo se ofrece donde existe (Chrome en
// Android, Safari reciente); en el resto se escanea con lector USB o tecleando.
interface DetectedBarcode {
  rawValue: string
}
interface BarcodeDetectorLike {
  detect(source: HTMLVideoElement): Promise<DetectedBarcode[]>
}
type BarcodeDetectorCtor = new (options?: { formats?: string[] }) => BarcodeDetectorLike

const emit = defineEmits<{ detected: [code: string] }>()

const Detector = (globalThis as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector
const supported = Boolean(Detector) && Boolean(navigator.mediaDevices?.getUserMedia)

const video = ref<HTMLVideoElement | null>(null)
const active = ref(false)
const error = ref('')
let stream: MediaStream | null = null
let timer: ReturnType<typeof setTimeout> | undefined
let lastCode = ''
let lastAt = 0

async function start() {
  error.value = ''
  try {
    stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
    active.value = true
    // El <video> aparece tras activar: esperar al siguiente tick de render.
    await new Promise((r) => setTimeout(r))
    if (!video.value) return
    video.value.srcObject = stream
    await video.value.play()
    const detector = new Detector!({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13'] })
    const tick = async () => {
      if (!active.value || !video.value) return
      try {
        const [code] = await detector.detect(video.value)
        const value = code?.rawValue.trim()
        // Un mismo código frente a la cámara se reporta una vez cada 3 s.
        if (value && (value !== lastCode || Date.now() - lastAt > 3000)) {
          lastCode = value
          lastAt = Date.now()
          emit('detected', value)
        }
      } catch {
        // Cuadro ilegible: se reintenta en el siguiente.
      }
      timer = setTimeout(tick, 300)
    }
    tick()
  } catch {
    error.value = 'No se pudo usar la cámara. Revisa el permiso del navegador o usa el lector/teclado.'
    stop()
  }
}

function stop() {
  active.value = false
  clearTimeout(timer)
  stream?.getTracks().forEach((t) => t.stop())
  stream = null
}

onBeforeUnmount(stop)
</script>

<template>
  <div v-if="supported">
    <button
      type="button"
      class="min-h-[44px] cursor-pointer rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted"
      @click="active ? stop() : start()"
    >
      {{ active ? 'Apagar cámara' : 'Escanear con la cámara' }}
    </button>
    <video v-if="active" ref="video" class="mt-2 w-full max-w-sm rounded-md border border-border" muted playsinline aria-label="Vista de la cámara" />
    <p v-if="error" role="alert" class="mt-2 text-sm text-destructive">{{ error }}</p>
  </div>
</template>
