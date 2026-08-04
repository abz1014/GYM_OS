import { lazy, Suspense } from 'react'
import { createBrowserRouter } from 'react-router-dom'

import { AppShell } from '@/shared/components/layout/AppShell'
import { RequireAuth } from '@/shared/components/RequireAuth'
import { PageLoader } from '@/shared/components/PageLoader'
import { HomeRedirect } from '@/shared/components/HomeRedirect'

const LoginPage = lazy(() => import('@/modules/auth/pages/LoginPage'))
const ForgotPasswordPage = lazy(() => import('@/modules/auth/pages/ForgotPasswordPage'))
const AccountPage = lazy(() => import('@/modules/auth/pages/AccountPage'))
const DashboardPage = lazy(() => import('@/modules/dashboard/pages/DashboardPage'))
const MemberPortalPage = lazy(() => import('@/modules/portal/pages/MemberPortalPage'))
const MembersListPage = lazy(() => import('@/modules/members/pages/MembersListPage'))
const MemberDetailPage = lazy(() => import('@/modules/members/pages/MemberDetailPage'))
const MembershipsPage = lazy(() => import('@/modules/memberships/pages/MembershipsPage'))
const AttendancePage = lazy(() => import('@/modules/attendance/pages/AttendancePage'))
const InvoicesListPage = lazy(() => import('@/modules/billing/pages/InvoicesListPage'))
const InvoiceDetailPage = lazy(() => import('@/modules/billing/pages/InvoiceDetailPage'))
const CrmPage = lazy(() => import('@/modules/crm/pages/CrmPage'))
const LeadDetailPage = lazy(() => import('@/modules/crm/pages/LeadDetailPage'))
const TrainersListPage = lazy(() => import('@/modules/trainers/pages/TrainersListPage'))
const TrainerDetailPage = lazy(() => import('@/modules/trainers/pages/TrainerDetailPage'))
const ClassesPage = lazy(() => import('@/modules/classes/pages/ClassesPage'))
const EquipmentPage = lazy(() => import('@/modules/equipment/pages/EquipmentPage'))
const MaintenancePage = lazy(() => import('@/modules/maintenance/pages/MaintenancePage'))
const WorkOrderDetailPage = lazy(() => import('@/modules/maintenance/pages/WorkOrderDetailPage'))
const InventoryPage = lazy(() => import('@/modules/inventory/pages/InventoryPage'))
const WorkoutsPage = lazy(() => import('@/modules/workouts/pages/WorkoutsPage'))
const NutritionPage = lazy(() => import('@/modules/nutrition/pages/NutritionPage'))
const ReportsPage = lazy(() => import('@/modules/reports/pages/ReportsPage'))
const NotificationsPage = lazy(() => import('@/modules/notifications/pages/NotificationsPage'))
const SettingsPage = lazy(() => import('@/modules/settings/pages/SettingsPage'))
const MigrationListPage = lazy(() => import('@/modules/migration/pages/MigrationListPage'))
const ImportJobDetailPage = lazy(() => import('@/modules/migration/pages/ImportJobDetailPage'))

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
          { index: true, element: <HomeRedirect /> },
          { path: 'account', element: withSuspense(<AccountPage />) },
          { path: 'dashboard', element: withSuspense(<DashboardPage />) },
          { path: 'portal', element: withSuspense(<MemberPortalPage />) },
          { path: 'members', element: withSuspense(<MembersListPage />) },
          { path: 'members/:id', element: withSuspense(<MemberDetailPage />) },
          { path: 'memberships', element: withSuspense(<MembershipsPage />) },
          { path: 'attendance', element: withSuspense(<AttendancePage />) },
          { path: 'billing', element: withSuspense(<InvoicesListPage />) },
          { path: 'billing/:id', element: withSuspense(<InvoiceDetailPage />) },
          { path: 'crm', element: withSuspense(<CrmPage />) },
          { path: 'crm/:id', element: withSuspense(<LeadDetailPage />) },
          { path: 'trainers', element: withSuspense(<TrainersListPage />) },
          { path: 'trainers/:id', element: withSuspense(<TrainerDetailPage />) },
          { path: 'classes', element: withSuspense(<ClassesPage />) },
          { path: 'equipment', element: withSuspense(<EquipmentPage />) },
          { path: 'maintenance', element: withSuspense(<MaintenancePage />) },
          { path: 'maintenance/work-orders/:id', element: withSuspense(<WorkOrderDetailPage />) },
          { path: 'inventory', element: withSuspense(<InventoryPage />) },
          { path: 'workouts', element: withSuspense(<WorkoutsPage />) },
          { path: 'nutrition', element: withSuspense(<NutritionPage />) },
          { path: 'reports', element: withSuspense(<ReportsPage />) },
          { path: 'notifications', element: withSuspense(<NotificationsPage />) },
          { path: 'settings', element: withSuspense(<SettingsPage />) },
          { path: 'migration', element: withSuspense(<MigrationListPage />) },
          { path: 'migration/:id', element: withSuspense(<ImportJobDetailPage />) },
        ],
      },
    ],
  },
  { path: '*', element: <HomeRedirect /> },
])
