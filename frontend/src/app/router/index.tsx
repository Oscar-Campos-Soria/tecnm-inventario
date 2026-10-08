import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AuthProvider } from '../../shared/hooks/AuthProvider'
import { useAuth } from '../../shared/hooks/useAuth'
import { AuthLayout } from '../layouts/AuthLayout'
import { MainLayout } from '../layouts/MainLayout'
import { LoginPage } from '../../modules/auth/pages/Login'
import { DashboardPage } from '../../modules/dashboard/pages/DashboardPage'
import { AssetsPage } from '../../modules/assets/pages/AssetsPage'
import { AssetDetailPage } from '../../modules/assets/pages/AssetDetailPage'
import { LocationsPage } from '../../modules/locations/pages/LocationsPage'
import { LocationDetailPage } from '../../modules/locations/pages/LocationDetailPage'

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
            <Route path="assets" element={<AssetsPage />} />
            <Route path="assets/:assetId" element={<AssetDetailPage />} />
            <Route path="locations" element={<LocationsPage />} />
            <Route
              path="locations/:locationId"
              element={<LocationDetailPage />}
            />
            {/* Rutas de módulos se agregan dentro de RequireAuth (FASE 2-3) */}
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
