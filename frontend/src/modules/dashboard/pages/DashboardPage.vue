<script setup lang="ts">
import { useAuth } from '../../../shared/hooks/useAuth'

const { session } = useAuth()
</script>

<template>
  <div class="space-y-6">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Panel general</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Hola, {{ session?.firstName ?? session?.username ?? 'usuario' }}
      </p>
    </header>

    <div class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="font-medium text-foreground">Métricas del inventario</p>
      <p class="mt-1 text-sm text-muted-foreground">
        Pendiente: endpoint de agregados en <code>Features/Dashboard</code> (FASE 2).
      </p>
    </div>
  </div>
</template>
