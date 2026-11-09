<script setup lang="ts">
import { computed } from 'vue'
import { usage } from '../status'
import type { LicenseSummary } from '../types'

const props = defineProps<{ license: Pick<LicenseSummary, 'inUse' | 'quantityPurchased'> }>()
const u = computed(() => usage(props.license))
</script>

<template>
  <div class="min-w-36">
    <p :class="['text-sm', u.full ? 'font-semibold text-priority-high' : 'text-foreground']">{{ u.text }}</p>
    <div
      v-if="u.percent !== null"
      role="meter"
      :aria-valuenow="license.inUse"
      aria-valuemin="0"
      :aria-valuemax="license.quantityPurchased ?? 0"
      :aria-label="u.text"
      class="mt-1 h-1.5 overflow-hidden rounded-full bg-muted"
    >
      <div :class="['h-full rounded-full', u.full ? 'bg-priority-high' : 'bg-accent']" :style="{ width: `${u.percent}%` }" />
    </div>
  </div>
</template>
