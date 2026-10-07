import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from '../../shared/hooks/useAuth'
import { AuthLayout } from '../layouts/AuthLayout'
import { MainLayout } from '../layouts/MainLayout'
import { LoginPage } from '../../modules/auth/pages/Login'
import { DashboardPage } from '../../modules/dashboard/pages/DashboardPage'

function RequireAuth() {
  const { session } = useAuth()
  if (!session) {
    return <Navigate to="/login" replace />
  }
  return (
    <MainLayout>
      <Outlet />
    </MainLayout>
  )
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route
            path="/login"
            element={
              <AuthLayout>
                <LoginPage />
              </AuthLayout>
            }
          />
          <Route element={<RequireAuth />}>
            <Route index element={<DashboardPage />} />
            {/* Rutas de módulos se agregan dentro de RequireAuth (FASE 2-3) */}
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
