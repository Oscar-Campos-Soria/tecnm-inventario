<script setup lang="ts">
import { onMounted, ref, useId, watch } from 'vue'

// Diálogo nativo <dialog>: el navegador aporta el bloqueo de foco,
// Escape y el backdrop (::backdrop se estiliza en shared/styles/index.css),
// sin librerías.
const props = defineProps<{
  open: boolean
  title: string
}>()

const emit = defineEmits<{ close: [] }>()

const dialog = ref<HTMLDialogElement | null>(null)
const titleId = useId()

function sync(open: boolean) {
  const el = dialog.value
  if (!el) return
  if (open && !el.open) el.showModal()
  else if (!open && el.open) el.close()
}

onMounted(() => sync(props.open))
watch(() => props.open, sync)

function onBackdropClick(event: MouseEvent) {
  // Clic en el backdrop (el target es el propio diálogo).
  if (event.target === dialog.value) emit('close')
}
</script>

<template>
  <dialog
    ref="dialog"
    :aria-labelledby="titleId"
    class="ui-modal"
    @close="emit('close')"
    @click="onBackdropClick"
  >
    <div class="flex items-start justify-between gap-4">
      <h2 :id="titleId" class="font-display text-lg font-semibold text-foreground">
        {{ title }}
      </h2>
      <button
        type="button"
        aria-label="Cerrar"
        class="min-h-[44px] min-w-[44px] cursor-pointer rounded-md px-2 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground"
        @click="emit('close')"
      >
        <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
          <path d="M6 6l12 12M18 6L6 18" />
        </svg>
      </button>
    </div>
    <div class="mt-3">
      <slot />
    </div>
  </dialog>
</template>
