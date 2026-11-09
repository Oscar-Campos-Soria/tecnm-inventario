import { http } from '../../shared/api/http'
import type { Profile } from './types'

export const profileApi = {
  get: () => http.get<Profile>('/api/auth/profile'),
  changePassword: (currentPassword: string, newPassword: string) =>
    http.post<{ otherSessionsClosed: number }>('/api/auth/profile/password', { currentPassword, newPassword }),
  unlink: (provider: string) => http.delete(`/api/auth/profile/external-logins/${encodeURIComponent(provider)}`),
}
