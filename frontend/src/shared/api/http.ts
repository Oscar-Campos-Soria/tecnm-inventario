/**
 * Cliente HTTP base para consumir la API (.NET 10).
 *
 * - baseURL desde VITE_API_BASE_URL (ver frontend/.env.example)
 * - inyecta el token JWT de la sesión en localStorage
 * - ante un 401 limpia la sesión y redirige al login
 * - errores: lee `detail` de ProblemDetails (.NET)
 */

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5112'

const SESSION_KEY = 'tecnm.session'

export interface AuthSession {
  token: string
  userId: number
  username: string
  roleCode: string
  expiresAt: string
}

export class ApiError extends Error {
  public readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

function isExpired(iso: string): boolean {
  const time = Date.parse(iso)
  return Number.isNaN(time) || time <= Date.now()
}

export function getSession(): AuthSession | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as AuthSession
    return isExpired(session.expiresAt) ? null : session
  } catch {
    return null
  }
}

export function setSession(session: AuthSession | null): void {
  if (session === null) {
    localStorage.removeItem(SESSION_KEY)
  } else {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session))
  }
}

async function extractProblemDetail(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `Error ${response.status}`
  } catch {
    return `Error ${response.status}`
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const session = getSession()

  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(session ? { Authorization: `Bearer ${session.token}` } : {}),
      ...init?.headers,
    },
  })

  if (response.status === 401) {
    setSession(null)
    if (!window.location.pathname.startsWith('/login')) {
      window.location.href = '/login'
    }
    throw new ApiError('No autorizado', response.status)
  }

  if (!response.ok) {
    throw new ApiError(await extractProblemDetail(response), response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const http = {
  get<T>(path: string): Promise<T> {
    return request<T>(path)
  },
  post<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  put<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'PUT',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  delete<T>(path: string): Promise<T> {
    return request<T>(path, { method: 'DELETE' })
  },
}
