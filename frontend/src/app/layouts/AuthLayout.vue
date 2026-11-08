<script setup lang="ts">
import BrandMark from '../../shared/components/BrandMark.vue'

/**
 * Layout para pantallas fuera de la aplicación autenticada (login,
 * recuperación de acceso): panel institucional a la izquierda con la
 * etiqueta de inventario, formulario a la derecha. En móvil solo queda
 * el formulario.
 */

// Barras del código de barras: anchos fijos (no aleatorios) para que la
// etiqueta se vea igual en cada carga.
const BARS = [2, 1, 3, 1, 1, 2, 4, 1, 2, 1, 1, 3, 2, 1, 4, 1, 1, 2, 3, 1, 2, 2, 1, 1, 3, 1, 2, 4, 1, 1, 2, 1, 3]
</script>

<template>
  <div class="grid min-h-screen bg-card md:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
    <aside class="relative hidden overflow-hidden bg-primary text-on-primary md:flex md:flex-col md:justify-between md:p-10 lg:p-14">
      <div class="flex items-center gap-3">
        <BrandMark inverted />
        <span class="font-display text-lg font-semibold">TecNM Inventario</span>
      </div>

      <div class="my-12 flex justify-center">
        <!-- Etiqueta de inventario impresa: el objeto que lleva cada activo. -->
        <figure aria-hidden="true" class="asset-tag relative w-full max-w-[22rem] rounded-xl bg-card p-6 text-foreground">
          <span class="absolute right-5 top-5 h-3.5 w-3.5 rounded-full border-2 border-border bg-primary" />
          <p class="text-xs font-medium text-muted-foreground">Tecnológico Nacional de México</p>
          <p class="mt-1 font-display text-3xl font-semibold tracking-tight text-primary">TEC-LAB-0142</p>

          <dl class="mt-5 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
            <dt class="text-muted-foreground">Equipo</dt>
            <dd class="font-medium">Computadora de escritorio</dd>
            <dt class="text-muted-foreground">Ubicación</dt>
            <dd class="font-medium">Edificio B, Laboratorio de redes</dd>
            <dt class="text-muted-foreground">Estado</dt>
            <dd>
              <span class="rounded-full bg-muted px-2 py-0.5 text-xs font-semibold text-accent">Asignado</span>
            </dd>
          </dl>

          <div class="mt-6 flex h-12 items-stretch gap-[2px]">
            <span
              v-for="(width, index) in BARS"
              :key="index"
              :class="index % 2 === 0 ? 'bg-foreground' : 'bg-transparent'"
              :style="{ width: `${width * 2}px` }"
            />
          </div>
        </figure>
      </div>

      <div class="max-w-md">
        <p class="font-display text-2xl font-semibold leading-snug lg:text-3xl">
          Cada equipo del campus, con su historia completa.
        </p>
        <p class="mt-3 text-base leading-relaxed text-on-primary/75">
          Alta, ubicación, resguardo, incidentes y baja de los activos
          tecnológicos, en un solo registro auditado.
        </p>
      </div>
    </aside>

    <main class="flex items-center justify-center px-4 py-10 sm:px-8">
      <div class="w-full max-w-sm">
        <slot />
      </div>
    </main>
  </div>
</template>
