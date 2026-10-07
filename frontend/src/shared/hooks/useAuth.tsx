import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { getSession, setSession } from '../api/http'
import type { AuthSession } from '../api/http'

interface AuthContextValue {
  session: AuthSession | null
  login: (usernameOrEmail: string, password: string) => Promise<AuthSession>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<AuthSession | null>(() => getSession())

  const login = useCallback(async (usernameOrEmail: string, password: string) => {
    // La respuesta de POST /api/auth/login tiene la forma exacta de AuthSession
    const response = await fetchLogin(usernameOrEmail, password)
    setSession(response)
    setSessionState(response)
    return response
  }, [])

  const logout = useCallback(() => {
    setSession(null)
    setSessionState(null)
  }, [])

  const value = useMemo(() => ({ session, login, logout }), [session, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  }
  return context
}

async function fetchLogin(usernameOrEmail: string, password: string): Promise<AuthSession> {
  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5112'}/api/auth/login`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ usernameOrEmail, password }),
    },
  )

  if (!response.ok) {
    const problem = await response.json().catch(
      () => ({ detail: 'No se pudo conectar con el servidor.' }) as { detail?: string },
    )
    throw new Error(problem.detail ?? `Error ${response.status}`)
  }

  return (await response.json()) as AuthSession
}
