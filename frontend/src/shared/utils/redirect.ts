/** Destino seguro tras el login: solo rutas internas de la SPA (no "//otro-sitio"). */
export function safeRedirect(value: unknown): string {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//')
    ? value
    : '/'
}
