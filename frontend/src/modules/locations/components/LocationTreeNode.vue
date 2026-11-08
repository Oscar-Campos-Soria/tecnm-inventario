<script setup lang="ts">
import { computed } from 'vue'
import type { LocationItem } from '../types'

// Nodo del árbol de ubicaciones; se renderiza a sí mismo para los hijos.
const props = defineProps<{
  item: LocationItem
  depth: number
  childrenByParent: Map<number, LocationItem[]>
  expanded: Set<number>
  // Con búsqueda activa todo queda expandido y el botón se deshabilita.
  searching: boolean
  // Ids visibles por la búsqueda (null = sin búsqueda).
  visibleIds: Set<number> | null
  typeNameByCode: Map<string, string>
  canManage: boolean
}>()

const emit = defineEmits<{
  toggle: [locationId: number]
  addChild: [locationId: number]
}>()

const children = computed(() =>
  (props.childrenByParent.get(props.item.locationId) ?? []).filter(
    (child) => props.visibleIds === null || props.visibleIds.has(child.locationId),
  ),
)
const hasChildren = computed(() => (props.childrenByParent.get(props.item.locationId) ?? []).length > 0)
const isExpanded = computed(() => props.searching || props.expanded.has(props.item.locationId))
const typeName = computed(() => props.typeNameByCode.get(props.item.locationTypeCode) ?? props.item.locationTypeCode)
</script>

<template>
  <li>
    <div
      class="flex min-h-[44px] items-center gap-1.5 rounded-md px-2 text-sm transition-colors duration-150 hover:bg-muted"
      :style="{ paddingLeft: `${depth * 1.5 + 0.5}rem` }"
    >
      <button
        v-if="hasChildren"
        type="button"
        :disabled="searching"
        :aria-expanded="isExpanded"
        :aria-label="`${isExpanded ? 'Contraer' : 'Expandir'} ${item.name}`"
        :title="searching ? 'Expandido por la búsqueda' : isExpanded ? 'Contraer' : 'Expandir'"
        class="min-h-[44px] min-w-[44px] shrink-0 cursor-pointer rounded-md px-0 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground disabled:cursor-default disabled:opacity-50"
        @click="emit('toggle', item.locationId)"
      >
        <svg
          viewBox="0 0 24 24"
          :class="['h-5 w-5 transition-transform duration-150', isExpanded ? 'rotate-90' : '']"
          fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"
        >
          <path d="M9 6l6 6-6 6" />
        </svg>
      </button>
      <span v-else class="w-[44px] shrink-0" aria-hidden="true" />

      <RouterLink :to="`/locations/${item.locationId}`" class="font-medium text-foreground underline-offset-2 hover:underline">
        {{ item.name }}
      </RouterLink>
      <span class="text-muted-foreground">{{ item.code }}</span>
      <span class="ml-auto rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">{{ typeName }}</span>
      <button
        v-if="canManage"
        type="button"
        :aria-label="`Agregar sub-ubicación a ${item.name}`"
        title="Agregar sub-ubicación"
        class="min-h-[44px] min-w-[44px] shrink-0 cursor-pointer rounded-md px-0 text-secondary transition-colors duration-150 hover:bg-muted hover:text-foreground"
        @click="emit('addChild', item.locationId)"
      >
        <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
          <path d="M12 5v14M5 12h14" />
        </svg>
      </button>
    </div>

    <ul v-if="hasChildren && isExpanded" class="border-l border-border" :style="{ marginLeft: `${depth * 1.5 + 1.625}rem` }">
      <LocationTreeNode
        v-for="child in children"
        :key="child.locationId"
        :item="child"
        :depth="depth + 1"
        :children-by-parent="childrenByParent"
        :expanded="expanded"
        :searching="searching"
        :visible-ids="visibleIds"
        :type-name-by-code="typeNameByCode"
        :can-manage="canManage"
        @toggle="emit('toggle', $event)"
        @add-child="emit('addChild', $event)"
      />
    </ul>
  </li>
</template>
