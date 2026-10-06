/**
 * Cliente HTTP base para consumir la API (.NET 10).
 *
 * - baseURL desde VITE_API_BASE_URL (ver frontend/.env.example)
 * - inyecta el token JWT almacenado en localStorage
 * - ante un 401 limpia la sesión y redirige al login
 */

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5112'

const TOKEN_KEY = 'tecnm.token'

export class ApiError extends Error {
  public readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null): void {
  if (token === null) {
    localStorage.removeItem(TOKEN_KEY)
  } else {
    localStorage.setItem(TOKEN_KEY, token)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken()

  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (response.status === 401) {
    setToken(null)
    if (!window.location.pathname.startsWith('/login')) {
      window.location.href = '/login'
    }
    throw new ApiError('No autorizado', response.status)
  }

  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new ApiError(detail || `Error ${response.status}`, response.status)
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
