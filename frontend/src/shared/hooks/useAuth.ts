import { createContext, useContext } from 'react'
import type { AuthSession } from '../api/http'

export interface AuthContextValue {
  session: AuthSession | null
  login: (usernameOrEmail: string, password: string) => Promise<AuthSession>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  }
  return context
}
