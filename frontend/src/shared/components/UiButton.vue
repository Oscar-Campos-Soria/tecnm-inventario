<script setup lang="ts">
type UiButtonVariant = 'primary' | 'secondary' | 'danger'

withDefaults(
  defineProps<{
    variant?: UiButtonVariant
    type?: 'button' | 'submit'
  }>(),
  { variant: 'secondary', type: 'button' },
)

const variantClasses: Record<UiButtonVariant, string> = {
  primary:
    'bg-accent font-semibold text-on-accent hover:bg-accent-hover disabled:cursor-wait disabled:opacity-60',
  secondary:
    'border border-border bg-card text-primary hover:border-accent hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50',
  danger:
    'bg-destructive font-semibold text-on-destructive hover:bg-destructive/90 disabled:cursor-not-allowed disabled:opacity-50',
}
</script>

<!-- disabled, onClick, aria-* y class extra llegan como atributos (fallthrough). -->
<template>
  <button
    :type="type"
    :class="['min-h-[44px] cursor-pointer rounded-md px-4 text-sm transition-colors duration-150', variantClasses[variant]]"
  >
    <slot />
  </button>
</template>
