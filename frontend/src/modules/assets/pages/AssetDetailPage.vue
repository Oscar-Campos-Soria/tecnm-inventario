<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiField from '../../../shared/components/UiField.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../../shared/utils/format'
import { assetsApi, catalogName } from '../api'
import type {
  AssetAssignment,
  AssetDetail,
  AssetMovement,
  GetCatalogsResponse,
  LocationItem,
  PagedResult,
  UserItem,
} from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

const route = useRoute()
const { hasPermission } = useAuth()
const numericId = computed(() => Number(route.params.assetId))
const isValidId = computed(() => Number.isInteger(numericId.value))

const state = ref<LoadState>('loading')
const detail = ref<AssetDetail | null>(null)
const catalogs = ref<GetCatalogsResponse | null>(null)
const locations = ref<LocationItem[]>([])
const movements = ref<PagedResult<AssetMovement> | null>(null)
const movementsPage = ref(1)
const assignments = ref<AssetAssignment[]>([])

const movementForm = reactive({ toLocationId: '', movementReasonId: '', notes: '' })
const assignmentForm = reactive({ assignedTo: '', assignmentTypeId: '', locationId: '', notes: '' })
const userSearch = ref('')
const users = ref<UserItem[]>([])
const submitting = ref<'movement' | 'assignment' | null>(null)
const formError = ref('')
const detailLoading = ref(false)

// Bloquea los formularios mientras hay una operación o una recarga de la
// ficha en curso (evita un falso 409 por enviar con la versión de antes).
const busy = computed(() => submitting.value !== null || detailLoading.value)

async function loadDetail() {
  detailLoading.value = true
  try {
    const [d, c, l] = await Promise.all([
      assetsApi.getById(numericId.value),
      assetsApi.catalogs(),
      // GET /api/locations exige locations.view; sin él la ficha carga igual
      // y la ubicación se muestra como "—".
      assetsApi.locations().catch(() => [] as LocationItem[]),
    ])
    detail.value = d
    catalogs.value = c
    locations.value = l
  } finally {
    detailLoading.value = false
  }
}

async function loadMovements() {
  movements.value = await assetsApi.getMovements(numericId.value, movementsPage.value)
}

async function loadAssignments() {
  assignments.value = await assetsApi.getAssignments(numericId.value)
}

// Un 409 significa que otra persona modificó el activo: se recargan los
// datos automáticamente; los botones quedan deshabilitados mientras tanto.
function operationError(err: unknown, fallback: string): string {
  if (err instanceof ApiError && err.status === 409) {
    loadDetail().catch(() => {
      // La recarga es automática; si falla, el usuario recarga a mano.
    })
    return 'Otra persona modificó este activo. Recargamos los datos; revísalos e intenta de nuevo.'
  }
  return err instanceof ApiError ? err.message : fallback
}

// Carga inicial (y al cambiar de activo en la misma vista).
watch(
  numericId,
  () => {
    if (!isValidId.value) return
    state.value = 'loading'
    loadDetail()
      .then(() => (state.value = 'ready'))
      .catch((err) => {
        state.value = err instanceof ApiError && err.status === 404 ? 'not-found' : 'error'
      })
  },
  { immediate: true },
)

// Movimientos y asignaciones: cada llamada por su cuenta (un fallo no vacía la otra).
watch(
  [state, movementsPage],
  () => {
    if (state.value !== 'ready') return
    assetsApi
      .getMovements(numericId.value, movementsPage.value)
      .then((m) => (movements.value = m))
      .catch(() => (movements.value = null))
    assetsApi
      .getAssignments(numericId.value)
      .then((a) => (assignments.value = a))
      .catch(() => (assignments.value = []))
  },
  { immediate: true },
)

// Búsqueda de usuarios con debounce de 200 ms.
let userTimer: ReturnType<typeof setTimeout> | undefined
watch(
  userSearch,
  (search) => {
    clearTimeout(userTimer)
    userTimer = setTimeout(() => {
      assetsApi
        .users(search)
        .then((list) => (users.value = list))
        .catch(() => (users.value = []))
    }, 200)
  },
  { immediate: true },
)
onBeforeUnmount(() => clearTimeout(userTimer))

const currentLocationName = computed(
  () => locations.value.find((l) => l.locationId === detail.value?.currentLocationId)?.name ?? '—',
)
const assetTypeName = computed(() => catalogName(catalogs.value?.assetTypes, detail.value?.assetTypeCode ?? ''))
const assetStatusName = computed(() => catalogName(catalogs.value?.assetStatuses, detail.value?.statusCode ?? ''))

async function handleMovementSubmit() {
  formError.value = ''
  if (!detail.value) return
  if (!movementForm.toLocationId || !movementForm.movementReasonId) {
    formError.value = 'Selecciona ubicación de destino y motivo.'
    return
  }
  submitting.value = 'movement'
  try {
    // La versión mostrada en pantalla es la que se envía como expectedVersion.
    await assetsApi.createMovement(numericId.value, detail.value.version, {
      toLocationId: Number(movementForm.toLocationId),
      movementReasonId: Number(movementForm.movementReasonId),
      notes: movementForm.notes.trim() || undefined,
    })
    Object.assign(movementForm, { toLocationId: '', movementReasonId: '', notes: '' })
    await Promise.all([loadDetail(), loadMovements()])
  } catch (err) {
    formError.value = operationError(err, 'No se pudo registrar el movimiento.')
  } finally {
    submitting.value = null
  }
}

async function handleAssignmentSubmit() {
  formError.value = ''
  if (!detail.value) return
  if (!assignmentForm.assignedTo || !assignmentForm.assignmentTypeId) {
    formError.value = 'Selecciona usuario y tipo de asignación.'
    return
  }
  submitting.value = 'assignment'
  try {
    await assetsApi.createAssignment(numericId.value, detail.value.version, {
      assignedTo: Number(assignmentForm.assignedTo),
      assignmentTypeId: Number(assignmentForm.assignmentTypeId),
      locationId: assignmentForm.locationId ? Number(assignmentForm.locationId) : null,
      notes: assignmentForm.notes.trim() || undefined,
    })
    Object.assign(assignmentForm, { assignedTo: '', assignmentTypeId: '', locationId: '', notes: '' })
    await Promise.all([loadDetail(), loadAssignments()])
  } catch (err) {
    formError.value = operationError(err, 'No se pudo registrar la asignación.')
  } finally {
    submitting.value = null
  }
}

async function handleReturn(assignmentId: number) {
  formError.value = ''
  if (!detail.value) return
  try {
    await assetsApi.returnAssignment(numericId.value, assignmentId, detail.value.version)
    await Promise.all([loadDetail(), loadAssignments()])
  } catch (err) {
    formError.value = operationError(err, 'No se pudo devolver la asignación.')
  }
}

const inputClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const selectClass = 'min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground'
</script>

<template>
  <p v-if="isValidId && state === 'loading'" class="text-sm text-muted-foreground">Cargando ficha del activo…</p>

  <div v-else-if="!isValidId || state === 'not-found'" class="space-y-3">
    <p class="font-medium text-foreground">Activo no encontrado</p>
    <RouterLink to="/assets" class="text-sm text-primary underline-offset-2 hover:underline">Volver a activos</RouterLink>
  </div>

  <p v-else-if="state === 'error' || !detail" role="alert" class="text-sm text-destructive">
    No se pudo cargar la ficha del activo.
  </p>

  <div v-else class="space-y-6">
    <header class="space-y-1">
      <RouterLink to="/assets" class="text-sm text-secondary underline-offset-2 hover:underline">Activos</RouterLink>
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="font-display text-2xl font-semibold text-foreground">{{ detail.inventoryCode }}</h1>
        <span class="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">{{ assetTypeName }}</span>
        <span class="rounded-full bg-success/10 px-2.5 py-0.5 text-xs font-medium text-success">{{ assetStatusName }}</span>
        <!-- Ticket de soporte con este equipo ya elegido (módulo tickets). -->
        <RouterLink
          v-if="hasPermission('tickets.create')"
          :to="{ path: '/tickets', query: { nuevo: '1', assetId: String(detail.assetId) } }"
          class="ml-auto inline-flex min-h-[44px] items-center rounded-md border border-border bg-card px-4 text-sm text-foreground transition-colors duration-150 hover:bg-muted"
        >
          Reportar problema
        </RouterLink>
      </div>
    </header>

    <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
      {{ formError }}
    </p>

    <section aria-labelledby="datos-generales" class="rounded-lg border border-border bg-card p-5">
      <h2 id="datos-generales" class="mb-3 font-display text-base font-semibold text-foreground">Datos generales</h2>
      <dl class="grid gap-x-8 gap-y-3 sm:grid-cols-2">
        <UiField label="Fabricante" :value="detail.manufacturerName" />
        <UiField label="Modelo" :value="detail.modelName" />
        <UiField label="Serie" :value="detail.serialNumber" />
        <UiField label="Hostname" :value="detail.hostname" />
        <UiField label="Ubicación actual">
          <RouterLink
            v-if="detail.currentLocationId !== null"
            :to="`/locations/${detail.currentLocationId}`"
            class="text-primary underline-offset-2 hover:underline"
          >
            {{ currentLocationName }}
          </RouterLink>
          <template v-else>—</template>
        </UiField>
        <UiField label="Versión" :value="String(detail.version)" />
      </dl>
      <dl v-if="detail.notes" class="mt-3 border-t border-border pt-3">
        <UiField label="Notas" :value="detail.notes" />
      </dl>
    </section>

    <section aria-labelledby="movimientos" class="rounded-lg border border-border bg-card p-5">
      <h2 id="movimientos" class="mb-3 font-display text-base font-semibold text-foreground">Movimientos de ubicación</h2>

      <div v-if="movements && movements.items.length > 0" class="mb-5 overflow-x-auto rounded-md border border-border">
        <table class="w-full text-left text-sm">
          <thead>
            <tr class="border-b border-border bg-muted">
              <th scope="col" class="px-3 py-2 font-semibold text-foreground">Fecha</th>
              <th scope="col" class="px-3 py-2 font-semibold text-foreground">Origen</th>
              <th scope="col" class="px-3 py-2 font-semibold text-foreground">Destino</th>
              <th scope="col" class="px-3 py-2 font-semibold text-foreground">Motivo</th>
              <th scope="col" class="px-3 py-2 font-semibold text-foreground">Por</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="movement in movements.items" :key="movement.assetMovementId" class="border-b border-border last:border-0">
              <td class="px-3 py-2.5 text-secondary">{{ formatDateTime(movement.movedAt) }}</td>
              <td class="px-3 py-2.5 text-secondary">{{ movement.fromLocationName ?? '—' }}</td>
              <td class="px-3 py-2.5 font-medium text-foreground">{{ movement.toLocationName }}</td>
              <td class="px-3 py-2.5 text-secondary">{{ movement.movementReasonName }}</td>
              <td class="px-3 py-2.5 text-secondary">{{ movement.movedByUsername }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <p v-if="movements && movements.items.length === 0" class="mb-5 text-sm text-muted-foreground">Sin movimientos registrados.</p>

      <nav v-if="movements && movements.totalPages > 1" class="mb-5 flex items-center gap-2" aria-label="Paginación de movimientos">
        <UiButton :disabled="movementsPage <= 1" @click="movementsPage--">Anterior</UiButton>
        <span class="text-sm text-muted-foreground">Página {{ movements.pageNumber }} de {{ movements.totalPages }}</span>
        <UiButton :disabled="movementsPage >= movements.totalPages" @click="movementsPage++">Siguiente</UiButton>
      </nav>

      <h3 class="mb-2 text-sm font-semibold text-foreground">Registrar movimiento</h3>
      <form class="grid gap-3 sm:grid-cols-2" novalidate @submit.prevent="handleMovementSubmit">
        <div>
          <label for="to-location" class="mb-1 block text-sm font-medium text-foreground">Ubicación de destino</label>
          <select id="to-location" v-model="movementForm.toLocationId" :class="selectClass">
            <option value="">Selecciona una ubicación</option>
            <option v-for="location in locations" :key="location.locationId" :value="String(location.locationId)">
              {{ location.name }} ({{ location.code }})
            </option>
          </select>
        </div>
        <div>
          <label for="movement-reason" class="mb-1 block text-sm font-medium text-foreground">Motivo</label>
          <select id="movement-reason" v-model="movementForm.movementReasonId" :class="selectClass">
            <option value="">Selecciona un motivo</option>
            <option v-for="reason in catalogs?.movementReasons ?? []" :key="reason.id" :value="String(reason.id)">
              {{ reason.name }}
            </option>
          </select>
        </div>
        <div class="sm:col-span-2">
          <label for="movement-notes" class="mb-1 block text-sm font-medium text-foreground">Notas</label>
          <input id="movement-notes" v-model="movementForm.notes" type="text" :class="inputClass" placeholder="Observaciones del movimiento (opcional)" />
        </div>
        <div class="sm:col-span-2">
          <UiButton type="submit" variant="primary" :disabled="busy">
            {{ submitting === 'movement' ? 'Registrando…' : 'Registrar movimiento' }}
          </UiButton>
        </div>
      </form>
    </section>

    <section aria-labelledby="asignaciones" class="rounded-lg border border-border bg-card p-5">
      <h2 id="asignaciones" class="mb-3 font-display text-base font-semibold text-foreground">Asignaciones</h2>

      <p v-if="assignments.length === 0" class="mb-5 text-sm text-muted-foreground">Sin asignaciones registradas.</p>

      <ul v-else class="mb-5 space-y-2">
        <li
          v-for="assignment in assignments"
          :key="assignment.assetAssignmentId"
          class="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border px-3 py-2.5 text-sm"
        >
          <span class="font-medium text-foreground">{{ assignment.assignedToUsername }}</span>
          <span class="text-secondary">{{ assignment.assignmentTypeName }}</span>
          <span class="text-secondary">{{ assignment.locationName ?? 'Sin ubicación de entrega' }}</span>
          <span class="text-muted-foreground">
            {{ formatDate(assignment.assignedAt) }}<template v-if="assignment.returnedAt"> → {{ formatDate(assignment.returnedAt) }}</template>
          </span>
          <span v-if="assignment.isActive" class="rounded-full bg-success/10 px-2 py-0.5 text-xs font-medium text-success">Activa</span>
          <span v-else class="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">Devuelta</span>
          <UiButton
            v-if="assignment.isActive"
            :disabled="busy"
            class="ml-auto"
            @click="handleReturn(assignment.assetAssignmentId)"
          >
            Devolver
          </UiButton>
        </li>
      </ul>

      <h3 class="mb-2 text-sm font-semibold text-foreground">Asignar activo</h3>
      <form class="grid gap-3 sm:grid-cols-2" novalidate @submit.prevent="handleAssignmentSubmit">
        <div>
          <label for="assigned-to" class="mb-1 block text-sm font-medium text-foreground">Usuario</label>
          <input
            id="assigned-to-search"
            v-model="userSearch"
            type="search"
            placeholder="Busca por nombre o usuario"
            :class="['mb-1.5', inputClass]"
            aria-label="Buscar usuario"
          />
          <select id="assigned-to" v-model="assignmentForm.assignedTo" :class="selectClass">
            <option value="">Selecciona un usuario</option>
            <option v-for="user in users" :key="user.userId" :value="String(user.userId)">
              {{ user.firstName }} {{ user.lastName }} ({{ user.username }})
            </option>
          </select>
        </div>
        <div>
          <label for="assignment-type" class="mb-1 block text-sm font-medium text-foreground">Tipo de asignación</label>
          <select id="assignment-type" v-model="assignmentForm.assignmentTypeId" :class="selectClass">
            <option value="">Selecciona un tipo</option>
            <option v-for="type in catalogs?.assignmentTypes ?? []" :key="type.id" :value="String(type.id)">
              {{ type.name }}
            </option>
          </select>
        </div>
        <div>
          <label for="assignment-location" class="mb-1 block text-sm font-medium text-foreground">Ubicación de entrega</label>
          <select id="assignment-location" v-model="assignmentForm.locationId" :class="selectClass">
            <option value="">Ninguna</option>
            <option v-for="location in locations" :key="location.locationId" :value="String(location.locationId)">
              {{ location.name }}
            </option>
          </select>
        </div>
        <div>
          <label for="assignment-notes" class="mb-1 block text-sm font-medium text-foreground">Notas</label>
          <input id="assignment-notes" v-model="assignmentForm.notes" type="text" :class="inputClass" placeholder="Observaciones (opcional)" />
        </div>
        <div class="sm:col-span-2">
          <UiButton type="submit" variant="primary" :disabled="busy">
            {{ submitting === 'assignment' ? 'Asignando…' : 'Asignar' }}
          </UiButton>
        </div>
      </form>
    </section>
  </div>
</template>
