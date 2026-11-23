<script setup lang="ts">
import BrandMark from '../../shared/components/BrandMark.vue'

/**
 * Layout para pantallas fuera de la aplicación autenticada (login,
 * recuperación de acceso): panel institucional a la izquierda con la
 * etiqueta de inventario y su historial, formulario a la derecha. En móvil
 * solo queda el formulario.
 */

// Vida de un equipo de ejemplo, con los mismos colores de estado que usa la app.
const HISTORY = [
  { label: 'Alta en el inventario', date: '12 ene', chip: 'chip-success', status: 'Disponible' },
  { label: 'Entregada a Redes', date: '20 ene', chip: 'chip-info', status: 'Asignado' },
  { label: 'No enciende la pantalla', date: '4 mar', chip: 'chip-danger', status: 'Con falla' },
  { label: 'Cambio de panel', date: '6 mar', chip: 'chip-warning', status: 'En mantenimiento' },
  { label: 'De vuelta al laboratorio', date: '9 mar', chip: 'chip-info', status: 'Asignado' },
]
</script>

<template>
  <div class="grid min-h-screen bg-card md:grid-cols-[minmax(0,6fr)_minmax(0,5fr)]">
    <aside class="relative hidden overflow-hidden bg-sidebar text-on-primary md:flex md:flex-col md:justify-between md:gap-10 md:p-10 lg:p-14">
      <div class="flex items-center gap-3">
        <BrandMark inverted />
        <span class="font-display text-lg font-bold">TecNM Inventario</span>
      </div>

      <!-- Etiqueta de inventario: el objeto que lleva cada activo, imprimiendo su historia. -->
      <figure aria-hidden="true" class="asset-tag relative mx-auto w-full max-w-[27rem] rounded-lg bg-card p-6 text-foreground lg:p-7">
        <span class="absolute right-6 top-6 size-4 rounded-full bg-sidebar" />
        <p class="text-sm text-muted-foreground">Computadora, Laboratorio de redes</p>
        <p class="mt-1 font-display text-4xl font-bold tracking-tight text-primary">TEC-LAB-0142</p>
        <ol class="mt-5 border-t-2 border-foreground">
          <li
            v-for="(event, i) in HISTORY"
            :key="i"
            class="tag-line flex items-center gap-3 border-b border-border py-2.5 text-sm"
            :style="{ '--i': i }"
          >
            <span class="w-12 shrink-0 text-muted-foreground tabular-nums">{{ event.date }}</span>
            <span class="min-w-0 flex-1 truncate font-medium">{{ event.label }}</span>
            <span :class="['chip', event.chip]">{{ event.status }}</span>
          </li>
        </ol>
      </figure>

      <div class="max-w-xl">
        <p class="font-display text-3xl font-bold leading-tight text-balance lg:text-4xl">Cada equipo del campus, con su historia completa.</p>
        <p class="mt-3 max-w-md leading-relaxed text-sidebar-muted">
          Dónde está, quién lo tiene, qué le pasó y quién lo arregló, en un solo registro auditado.
        </p>
      </div>
    </aside>

    <main class="flex items-center justify-center px-4 py-10 sm:px-8">
      <div class="page-enter w-full max-w-sm">
        <slot />
      </div>
    </main>
  </div>
</template>
