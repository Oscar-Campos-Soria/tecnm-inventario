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

export interface DownloadedFile {
  blob: Blob
  fileName: string
}

// Nombre de archivo del header Content-Disposition
// ("attachment; filename=foto.jpg"); "documento" si no viene.
function fileNameFrom(disposition: string | null): string {
  const match = /filename\*?=(?:UTF-\d''|")?([^";]+)/i.exec(disposition ?? '')
  if (!match?.[1]) return 'documento'
  try {
    return decodeURIComponent(match[1])
  } catch {
    return match[1]
  }
}

export class ApiError extends Error {
  public readonly status: number
  // Errores de validación por campo. FluentValidation
  // los devuelve en la extensión "errors" de
  // ProblemDetails (el "detail" en ese caso es un
  // texto genérico: "Se produjeron uno o más errores
  // de validación.").
  public readonly errors?: Record<string, string[]>

  constructor(
    message: string,
    status: number,
    errors?: Record<string, string[]>,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
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

interface ProblemDetails {
  detail?: string
  title?: string
  errors?: Record<string, string[]>
}

// El primer mensaje de validación es el más útil
// para mostrar en pantalla; sin esto, el usuario
// vería el texto genérico de FluentValidation.
function firstValidationError(
  errors?: Record<string, string[]>,
): string | undefined {
  if (!errors) return undefined
  for (const messages of Object.values(errors)) {
    if (messages.length > 0) {
      return messages[0]
    }
  }
  return undefined
}

async function extractProblem(
  response: Response,
): Promise<{
  message: string
  errors?: Record<string, string[]>
}> {
  try {
    const problem = (await response.json()) as ProblemDetails
    return {
      message:
        firstValidationError(problem.errors) ??
        problem.detail ??
        problem.title ??
        `Error ${response.status}`,
      errors: problem.errors,
    }
  } catch {
    return { message: `Error ${response.status}` }
  }
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (response.status === 401) {
    setSession(null)
    if (!window.location.pathname.startsWith('/login')) {
      window.location.href = '/login'
    }
    throw new ApiError('No autorizado', response.status)
  }

  if (!response.ok) {
    const problem = await extractProblem(response)
    throw new ApiError(problem.message, response.status, problem.errors)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
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

  return handleResponse<T>(response)
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
  // PATCH (cambios de estado): mismo cuerpo JSON que PUT.
  patch<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'PATCH',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  delete<T>(path: string): Promise<T> {
    return request<T>(path, { method: 'DELETE' })
  },
  // POST multipart (evidencias): el navegador fija Content-Type
  // con el boundary del FormData, así que no se envía desde aquí.
  postForm<T>(path: string, formData: FormData): Promise<T> {
    const session = getSession()

    return fetch(`${BASE_URL}${path}`, {
      method: 'POST',
      body: formData,
      headers: session ? { Authorization: `Bearer ${session.token}` } : {},
    }).then((response) => handleResponse<T>(response))
  },
  // Descarga binaria (evidencias): mismo manejo de 401/403 que el
  // resto del cliente. El llamador revoca el blob URL tras usarlo.
  download(path: string): Promise<DownloadedFile> {
    const session = getSession()

    return fetch(`${BASE_URL}${path}`, {
      headers: session ? { Authorization: `Bearer ${session.token}` } : {},
    }).then(async (response) => {
      if (response.status === 401) {
        setSession(null)
        if (!window.location.pathname.startsWith('/login')) {
          window.location.href = '/login'
        }
        throw new ApiError('No autorizado', response.status)
      }

      if (!response.ok) {
        const problem = await extractProblem(response)
        throw new ApiError(problem.message, response.status, problem.errors)
      }

      return {
        blob: await response.blob(),
        fileName: fileNameFrom(response.headers.get('content-disposition')),
      }
    })
  },
}
