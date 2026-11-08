import { createApp } from 'vue'
import '../shared/styles/index.css'
import App from './App.vue'
import { router } from './router'

const app = createApp(App)
app.use(router)

// Se monta cuando la primera navegación terminó (sesión ya resuelta por el
// guard): así no parpadea el menú antes de redirigir al login. Mientras,
// se ve el "Cargando sesión…" estático de index.html.
router.isReady().then(() => app.mount('#root'))
