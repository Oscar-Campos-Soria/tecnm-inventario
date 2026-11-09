import { http } from '../../shared/api/http'
import type { EditUserInput, ManagedUser, NewUserInput, PagedResult, RoleOption, UserFilters, UserStatus } from './types'

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

// Todo es solo ADMIN (users.manage / roles.manage; el alta usa /api/auth/register).
export const usersApi = {
  list(filters: UserFilters, pageNumber = 1) {
    return http.get<PagedResult<ManagedUser>>(`/api/users/manage${buildQuery({ pageNumber, pageSize: 20, ...filters })}`)
  },
  roles() {
    return http.get<RoleOption[]>('/api/roles')
  },
  create(input: NewUserInput) {
    return http.post<{ userId: number }>('/api/auth/register', input)
  },
  update(userId: number, expectedVersion: number, input: EditUserInput) {
    return http.put(`/api/users/${userId}?expectedVersion=${expectedVersion}`, input)
  },
  setStatus(userId: number, expectedVersion: number, status: UserStatus) {
    return http.patch<{ sessionsRevoked: number }>(`/api/users/${userId}/status?expectedVersion=${expectedVersion}`, { status })
  },
  resetPassword(userId: number, expectedVersion: number, newPassword: string) {
    return http.put<{ sessionsRevoked: number }>(`/api/users/${userId}/password?expectedVersion=${expectedVersion}`, { newPassword })
  },
}
