import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// /api pasa por el proxy para que SPA y API compartan origen: las cookies
// de sesión (HttpOnly, SameSite) son first-party y no hace falta CORS.
// changeOrigin queda en false para que el backend vea el host de la SPA
// y arme con él la redirect_uri del login federado.
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  // /assets/ es ruta de la SPA (ficha del activo); los archivos compilados van en /static/.
  build: { assetsDir: 'static' },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5112',
        changeOrigin: false,
      },
    },
  },
})
