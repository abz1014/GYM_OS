import { lazy, Suspense } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppShell } from '@/shared/components/layout/AppShell'
import { RequireAuth } from '@/shared/components/RequireAuth'
import { PageLoader } from '@/shared/components/PageLoader'

const LoginPage = lazy(() => import('@/modules/auth/pages/LoginPage'))
const ForgotPasswordPage = lazy(() => import('@/modules/auth/pages/ForgotPasswordPage'))
const DashboardPage = lazy(() => import('@/modules/dashboard/pages/DashboardPage'))
const MembersListPage = lazy(() => import('@/modules/members/pages/MembersListPage'))
const MemberDetailPage = lazy(() => import('@/modules/members/pages/MemberDetailPage'))
const MembershipsPage = lazy(() => import('@/modules/memberships/pages/MembershipsPage'))
const AttendancePage = lazy(() => import('@/modules/attendance/pages/AttendancePage'))
const InvoicesListPage = lazy(() => import('@/modules/billing/pages/InvoicesListPage'))
const InvoiceDetailPage = lazy(() => import('@/modules/billing/pages/InvoiceDetailPage'))
const CrmPage = lazy(() => import('@/modules/crm/pages/CrmPage'))
const TrainersListPage = lazy(() => import('@/modules/trainers/pages/TrainersListPage'))
const TrainerDetailPage = lazy(() => import('@/modules/trainers/pages/TrainerDetailPage'))
const EquipmentPage = lazy(() => import('@/modules/equipment/pages/EquipmentPage'))
const MaintenancePage = lazy(() => import('@/modules/maintenance/pages/MaintenancePage'))
const InventoryPage = lazy(() => import('@/modules/inventory/pages/InventoryPage'))
const WorkoutsPage = lazy(() => import('@/modules/workouts/pages/WorkoutsPage'))
const NutritionPage = lazy(() => import('@/modules/nutrition/pages/NutritionPage'))

function withSuspense(element: React.ReactNode) {
  return <Suspense fallback={<PageLoader />}>{element}</Suspense>
}

export const router = createBrowserRouter([
  { path: '/login', element: withSuspense(<LoginPage />) },
  { path: '/forgot-password', element: withSuspense(<ForgotPasswordPage />) },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/dashboard" replace /> },
          { path: 'dashboard', element: withSuspense(<DashboardPage />) },
          { path: 'members', element: withSuspense(<MembersListPage />) },
          { path: 'members/:id', element: withSuspense(<MemberDetailPage />) },
          { path: 'memberships', element: withSuspense(<MembershipsPage />) },
          { path: 'attendance', element: withSuspense(<AttendancePage />) },
          { path: 'billing', element: withSuspense(<InvoicesListPage />) },
          { path: 'billing/:id', element: withSuspense(<InvoiceDetailPage />) },
          { path: 'crm', element: withSuspense(<CrmPage />) },
          { path: 'trainers', element: withSuspense(<TrainersListPage />) },
          { path: 'trainers/:id', element: withSuspense(<TrainerDetailPage />) },
          { path: 'equipment', element: withSuspense(<EquipmentPage />) },
          { path: 'maintenance', element: withSuspense(<MaintenancePage />) },
          { path: 'inventory', element: withSuspense(<InventoryPage />) },
          { path: 'workouts', element: withSuspense(<WorkoutsPage />) },
          { path: 'nutrition', element: withSuspense(<NutritionPage />) },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/dashboard" replace /> },
])
