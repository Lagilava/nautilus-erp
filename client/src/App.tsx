import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { AppLayout } from './app/AppLayout';
import { Loading } from './components/ui';

// Route-level code-splitting: each page becomes its own chunk, fetched on navigation
// rather than bundled into the single ~950KB initial download. Auth pages (the very first
// thing an unauthenticated visitor loads) stay eager so sign-in isn't gated on a second
// round trip; everything behind ProtectedRoute is lazy.
import { LoginPage } from './pages/LoginPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';

const ProfilePage = lazy(() => import('./pages/ProfilePage').then((m) => ({ default: m.ProfilePage })));
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const ProductsPage = lazy(() => import('./pages/ProductsPage').then((m) => ({ default: m.ProductsPage })));
const CustomersPage = lazy(() => import('./pages/CustomersPage').then((m) => ({ default: m.CustomersPage })));
const SuppliersPage = lazy(() => import('./pages/SuppliersPage').then((m) => ({ default: m.SuppliersPage })));
const InventoryPage = lazy(() => import('./pages/InventoryPage').then((m) => ({ default: m.InventoryPage })));
const AuditPage = lazy(() => import('./pages/AuditPage').then((m) => ({ default: m.AuditPage })));
const ReportsPage = lazy(() => import('./pages/ReportsPage').then((m) => ({ default: m.ReportsPage })));
const SalesOrdersPage = lazy(() =>
  import('./pages/sales/SalesOrdersPage').then((m) => ({ default: m.SalesOrdersPage })),
);
const SalesOrderDetailPage = lazy(() =>
  import('./pages/sales/SalesOrderDetailPage').then((m) => ({ default: m.SalesOrderDetailPage })),
);
const InvoicesPage = lazy(() => import('./pages/sales/InvoicesPage').then((m) => ({ default: m.InvoicesPage })));
const InvoiceDetailPage = lazy(() =>
  import('./pages/sales/InvoiceDetailPage').then((m) => ({ default: m.InvoiceDetailPage })),
);
const PurchaseOrdersPage = lazy(() =>
  import('./pages/purchasing/PurchaseOrdersPage').then((m) => ({ default: m.PurchaseOrdersPage })),
);
const PurchaseOrderDetailPage = lazy(() =>
  import('./pages/purchasing/PurchaseOrderDetailPage').then((m) => ({ default: m.PurchaseOrderDetailPage })),
);
const SupplierInvoicesPage = lazy(() =>
  import('./pages/purchasing/SupplierInvoicesPage').then((m) => ({ default: m.SupplierInvoicesPage })),
);
const SupplierInvoiceDetailPage = lazy(() =>
  import('./pages/purchasing/SupplierInvoiceDetailPage').then((m) => ({ default: m.SupplierInvoiceDetailPage })),
);
const ChartOfAccountsPage = lazy(() =>
  import('./pages/accounting/ChartOfAccountsPage').then((m) => ({ default: m.ChartOfAccountsPage })),
);
const JournalEntriesPage = lazy(() =>
  import('./pages/accounting/JournalEntriesPage').then((m) => ({ default: m.JournalEntriesPage })),
);
const JournalEntryDetailPage = lazy(() =>
  import('./pages/accounting/JournalEntryDetailPage').then((m) => ({ default: m.JournalEntryDetailPage })),
);
const BankReconciliationPage = lazy(() =>
  import('./pages/accounting/BankReconciliationPage').then((m) => ({ default: m.BankReconciliationPage })),
);
const AccountingPeriodsPage = lazy(() =>
  import('./pages/accounting/AccountingPeriodsPage').then((m) => ({ default: m.AccountingPeriodsPage })),
);
const UsersPage = lazy(() => import('./pages/admin/UsersPage').then((m) => ({ default: m.UsersPage })));
const SettingsPage = lazy(() => import('./pages/admin/SettingsPage').then((m) => ({ default: m.SettingsPage })));

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      {/* Target of the emailed password-reset link. */}
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route
          path="/"
          element={
            <Suspense fallback={<Loading />}>
              <DashboardPage />
            </Suspense>
          }
        />
        <Route
          path="/profile"
          element={
            <Suspense fallback={<Loading />}>
              <ProfilePage />
            </Suspense>
          }
        />
        <Route
          path="/products"
          element={
            <Suspense fallback={<Loading />}>
              <ProductsPage />
            </Suspense>
          }
        />
        <Route
          path="/inventory"
          element={
            <Suspense fallback={<Loading />}>
              <InventoryPage />
            </Suspense>
          }
        />
        <Route
          path="/customers"
          element={
            <Suspense fallback={<Loading />}>
              <CustomersPage />
            </Suspense>
          }
        />
        <Route
          path="/suppliers"
          element={
            <Suspense fallback={<Loading />}>
              <SuppliersPage />
            </Suspense>
          }
        />

        <Route
          path="/sales-orders"
          element={
            <Suspense fallback={<Loading />}>
              <SalesOrdersPage />
            </Suspense>
          }
        />
        <Route
          path="/sales-orders/:id"
          element={
            <Suspense fallback={<Loading />}>
              <SalesOrderDetailPage />
            </Suspense>
          }
        />
        <Route
          path="/invoices"
          element={
            <Suspense fallback={<Loading />}>
              <InvoicesPage />
            </Suspense>
          }
        />
        <Route
          path="/invoices/:id"
          element={
            <Suspense fallback={<Loading />}>
              <InvoiceDetailPage />
            </Suspense>
          }
        />

        <Route
          path="/purchase-orders"
          element={
            <Suspense fallback={<Loading />}>
              <PurchaseOrdersPage />
            </Suspense>
          }
        />
        <Route
          path="/purchase-orders/:id"
          element={
            <Suspense fallback={<Loading />}>
              <PurchaseOrderDetailPage />
            </Suspense>
          }
        />
        <Route
          path="/supplier-invoices"
          element={
            <Suspense fallback={<Loading />}>
              <SupplierInvoicesPage />
            </Suspense>
          }
        />
        <Route
          path="/supplier-invoices/:id"
          element={
            <Suspense fallback={<Loading />}>
              <SupplierInvoiceDetailPage />
            </Suspense>
          }
        />

        <Route
          path="/accounting/chart-of-accounts"
          element={
            <Suspense fallback={<Loading />}>
              <ChartOfAccountsPage />
            </Suspense>
          }
        />
        <Route
          path="/accounting/journal-entries"
          element={
            <Suspense fallback={<Loading />}>
              <JournalEntriesPage />
            </Suspense>
          }
        />
        <Route
          path="/accounting/journal-entries/:id"
          element={
            <Suspense fallback={<Loading />}>
              <JournalEntryDetailPage />
            </Suspense>
          }
        />
        <Route
          path="/accounting/bank-reconciliation"
          element={
            <Suspense fallback={<Loading />}>
              <BankReconciliationPage />
            </Suspense>
          }
        />
        <Route
          path="/accounting/periods"
          element={
            <Suspense fallback={<Loading />}>
              <AccountingPeriodsPage />
            </Suspense>
          }
        />

        <Route
          path="/reports"
          element={
            <Suspense fallback={<Loading />}>
              <ReportsPage />
            </Suspense>
          }
        />
        <Route
          path="/audit"
          element={
            <Suspense fallback={<Loading />}>
              <AuditPage />
            </Suspense>
          }
        />

        <Route
          path="/admin/users"
          element={
            <Suspense fallback={<Loading />}>
              <UsersPage />
            </Suspense>
          }
        />
        <Route
          path="/admin/settings"
          element={
            <Suspense fallback={<Loading />}>
              <SettingsPage />
            </Suspense>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
