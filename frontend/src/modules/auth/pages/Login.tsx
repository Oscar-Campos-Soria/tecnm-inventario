import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../../shared/api/http'
import { useAuth } from '../../../shared/hooks/useAuth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()

  const [usernameOrEmail, setUsernameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (!usernameOrEmail.trim() || !password) {
      setError('Ingresa tu usuario y contraseña.')
      return
    }

    setIsSubmitting(true)
    try {
      await login(usernameOrEmail.trim(), password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'No se pudo conectar con el servidor. Verifica que la API esté activa.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="w-full max-w-sm">
      <div className="mb-8 text-center">
        <BrandMark />
        <h1 className="mt-4 font-display text-2xl font-semibold text-foreground">
          TecNM Assets
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Inventario de activos tecnológicos
        </p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="rounded-lg border border-border bg-card p-6"
        noValidate
      >
        <div className="mb-4">
          <label htmlFor="username" className="mb-1.5 block text-sm font-medium text-foreground">
            Usuario o correo
          </label>
          <input
            id="username"
            type="text"
            autoComplete="username"
            autoFocus
            value={usernameOrEmail}
            onChange={(event) => setUsernameOrEmail(event.target.value)}
            className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground placeholder:text-muted-foreground"
            placeholder="tu.usuario@tecnm.edu.mx"
          />
        </div>

        <div className="mb-5">
          <label htmlFor="password" className="mb-1.5 block text-sm font-medium text-foreground">
            Contraseña
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground placeholder:text-muted-foreground"
            placeholder="••••••••"
          />
        </div>

        {error && (
          <p role="alert" className="mb-4 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="min-h-[44px] w-full cursor-pointer rounded-md bg-primary font-semibold text-on-primary transition-colors duration-150 hover:bg-secondary disabled:cursor-wait disabled:opacity-60"
        >
          {isSubmitting ? 'Iniciando sesión…' : 'Iniciar sesión'}
        </button>
      </form>
    </div>
  )
}

function BrandMark() {
  return (
    <svg viewBox="0 0 32 32" className="mx-auto h-10 w-10" role="img" aria-label="TecNM Assets">
      <rect width="32" height="32" rx="6" fill="var(--color-accent)" />
      <path
        d="M16 8l7 4v8l-7 4-7-4v-8l7-4z"
        fill="none"
        stroke="var(--color-on-accent)"
        strokeWidth="2"
        strokeLinejoin="round"
      />
    </svg>
  )
}
