<script setup lang="ts">
import { computed, onMounted, ref, useId, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { can } from '../../../shared/utils/permissions'
import { incidentsApi } from '../api'
import type { IncidentDetail, UserItem } from '../types'

// Acciones del técnico sobre un incidente (incidents.resolve; eliminar:
// incidents.delete). Las reglas las aplica el backend; aquí solo se ofrecen
// las transiciones que la matriz permite (IncidentStatusTransitions).
const props = defineProps<{ incident: IncidentDetail }>()

// changed: recargar; notice: mensaje tras resolver (si el activo se liberó).
const emit = defineEmits<{ changed: [notice?: string]; conflict: [] }>()

const OPEN = ['ABIERTO', 'EN_DIAGNOSTICO', 'EN_REPARACION']
const MANUAL_TRANSITIONS: Record<string, { to: string; label: string }[]> = {
  ABIERTO: [
    { to: 'EN_DIAGNOSTICO', label: 'Iniciar diagnóstico' },
    { to: 'EN_REPARACION', label: 'Iniciar reparación' },
  ],
  EN_DIAGNOSTICO: [{ to: 'EN_REPARACION', label: 'Iniciar reparación' }],
  EN_REPARACION: [{ to: 'EN_DIAGNOSTICO', label: 'Volver a diagnóstico' }],
  RESUELTO: [{ to: 'CERRADO', label: 'Cerrar incidente' }],
}

const { session } = useAuth()
const router = useRouter()
const assignFieldId = useId()
const diagnosisFieldId = useId()
const solutionFieldId = useId()

const canResolve = computed(() => can(session.value, 'resolve'))
const canDelete = computed(() => can(session.value, 'delete'))
const isOpen = computed(() => OPEN.includes(props.incident.statusCode))
const transitions = computed(() => MANUAL_TRANSITIONS[props.incident.statusCode] ?? [])

const technicians = ref<UserItem[]>([])
const assignTo = ref('')
const diagnosis = ref('')
const solution = ref('')
const busy = ref(false)
const error = ref('')

onMounted(() => {
  if (canResolve.value) {
    incidentsApi.technicians().then((list) => (technicians.value = list)).catch(() => (technicians.value = []))
  }
})

watch(
  () => props.incident,
  (incident) => {
    assignTo.value = incident.assignedToId !== null ? String(incident.assignedToId) : ''
    diagnosis.value = incident.diagnosis ?? ''
  },
  { immediate: true },
)

async function run(action: () => Promise<string | undefined | void>) {
  error.value = ''
  busy.value = true
  try {
    const notice = await action()
    emit('changed', notice || undefined)
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) emit('conflict')
    else error.value = err instanceof ApiError ? err.message : 'No se pudo completar la acción.'
  } finally {
    busy.value = false
  }
}

function assign() {
  return run(async () => {
    await incidentsApi.assign(props.incident.incidentId, props.incident.version, { assignedToUserId: Number(assignTo.value) })
  })
}

function changeStatus(to: string) {
  return run(async () => {
    await incidentsApi.updateStatus(props.incident.incidentId, props.incident.version, { newStatusCode: to })
  })
}

function resolve() {
  if (!solution.value.trim()) {
    error.value = 'La solución aplicada es obligatoria para resolver.'
    return
  }
  return run(async () => {
    const result = await incidentsApi.resolve(props.incident.incidentId, props.incident.version, {
      solution: solution.value.trim(),
      diagnosis: diagnosis.value.trim() || undefined,
    })
    solution.value = ''
    return result.assetReleased
      ? 'Incidente resuelto. El activo volvió a circulación.'
      : `Incidente resuelto. El activo sigue fuera de circulación: quedan ${result.openIncidentsRemaining} incidente(s) abierto(s).`
  })
}

async function remove() {
  if (!window.confirm('¿Eliminar este incidente? Queda registrado en la auditoría.')) return
  error.value = ''
  busy.value = true
  try {
    await incidentsApi.remove(props.incident.incidentId, props.incident.version)
    await router.push('/incidents')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) emit('conflict')
    else error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar el incidente.'
  } finally {
    busy.value = false
  }
}

const fieldClass = 'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground'
</script>

<template>
  <section v-if="canResolve || canDelete" aria-labelledby="acciones-incidente" class="space-y-5 rounded-lg border border-border bg-card p-5">
    <h2 id="acciones-incidente" class="font-display text-base font-semibold text-foreground">Atención técnica</h2>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <div v-if="canResolve && isOpen">
      <label :for="assignFieldId" class="mb-1 block text-sm font-medium text-foreground">Técnico asignado</label>
      <div class="flex flex-wrap gap-2">
        <select
          :id="assignFieldId"
          v-model="assignTo"
          :disabled="busy"
          class="min-h-[44px] min-w-0 flex-1 cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground"
        >
          <option value="" disabled>Sin asignar</option>
          <option v-for="tech in technicians" :key="tech.userId" :value="String(tech.userId)">
            {{ tech.firstName }} {{ tech.lastName }} ({{ tech.username }})
          </option>
        </select>
        <UiButton :disabled="busy || !assignTo || Number(assignTo) === incident.assignedToId" @click="assign">Asignar</UiButton>
      </div>
    </div>

    <div v-if="canResolve && transitions.length > 0" class="flex flex-wrap gap-2">
      <UiButton v-for="t in transitions" :key="t.to" :disabled="busy" @click="changeStatus(t.to)">{{ t.label }}</UiButton>
    </div>
    <p v-if="canResolve && incident.statusCode === 'ABIERTO'" class="-mt-3 text-xs text-muted-foreground">
      Al iniciar la reparación el activo pasa a "En mantenimiento".
    </p>

    <form v-if="canResolve && isOpen" novalidate class="space-y-3 border-t border-border pt-4" @submit.prevent="resolve">
      <div>
        <label :for="diagnosisFieldId" class="mb-1 block text-sm font-medium text-foreground">
          Diagnóstico <span class="font-normal text-muted-foreground">(opcional)</span>
        </label>
        <textarea :id="diagnosisFieldId" v-model="diagnosis" rows="2" maxlength="4000" :class="fieldClass" placeholder="Qué se encontró" />
      </div>
      <div>
        <label :for="solutionFieldId" class="mb-1 block text-sm font-medium text-foreground">Solución aplicada</label>
        <textarea :id="solutionFieldId" v-model="solution" rows="3" maxlength="4000" :class="fieldClass" placeholder="Qué se hizo para corregir la falla" />
      </div>
      <UiButton type="submit" variant="primary" :disabled="busy">Resolver incidente</UiButton>
    </form>

    <div v-if="canDelete && !isOpen" class="border-t border-border pt-4">
      <p class="mb-2 text-sm text-muted-foreground">Solo se eliminan incidentes registrados por error; la eliminación queda en la auditoría.</p>
      <UiButton variant="danger" :disabled="busy" @click="remove">Eliminar incidente</UiButton>
    </div>
  </section>
</template>
