<script setup lang="ts">
import { ref } from 'vue'
import AcquisitionsTab from '../components/AcquisitionsTab.vue'
import SuppliersTab from '../components/SuppliersTab.vue'

const tab = ref<'compras' | 'proveedores'>('compras')
const TABS = [
  { id: 'compras', label: 'Compras' },
  { id: 'proveedores', label: 'Proveedores' },
] as const
</script>

<template>
  <div class="space-y-4">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Adquisiciones</h1>
      <p class="mt-1 text-sm text-muted-foreground">Compras de equipo y los proveedores que las surten.</p>
    </header>

    <div role="tablist" aria-label="Secciones de adquisiciones" class="flex gap-1 border-b border-border">
      <button
        v-for="t in TABS"
        :id="`tab-${t.id}`"
        :key="t.id"
        type="button"
        role="tab"
        :aria-selected="tab === t.id"
        :aria-controls="`panel-${t.id}`"
        :class="['min-h-[44px] cursor-pointer border-b-2 px-4 text-sm font-medium transition-colors', tab === t.id ? 'border-accent text-accent' : 'border-transparent text-secondary hover:text-foreground']"
        @click="tab = t.id"
      >
        {{ t.label }}
      </button>
    </div>

    <div :id="`panel-${tab}`" role="tabpanel" :aria-labelledby="`tab-${tab}`">
      <AcquisitionsTab v-if="tab === 'compras'" />
      <SuppliersTab v-else />
    </div>
  </div>
</template>
