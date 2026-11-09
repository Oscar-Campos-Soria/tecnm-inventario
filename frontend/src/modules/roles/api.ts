import { http } from '../../shared/api/http'
import type { Permission, Role } from './types'

// Todo con roles.manage (solo ADMIN).
export const rolesApi = {
  list: () => http.get<Role[]>('/api/roles'),
  permissions: () => http.get<Permission[]>('/api/permissions'),
  create: (input: { code: string; name: string; description: string | null }) => http.post<{ roleId: number }>('/api/roles', input),
  assign: (roleId: number, permissionCodes: string[]) => http.put(`/api/roles/${roleId}/permissions`, { permissionCodes }),
}
