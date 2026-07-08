import { Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { AppLayout } from './app/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { ProfilePage } from './pages/ProfilePage';
import { DashboardPage } from './pages/DashboardPage';
import { ProductsPage } from './pages/ProductsPage';
import { CustomersPage } from './pages/CustomersPage';
import { SuppliersPage } from './pages/SuppliersPage';
import { InventoryPage } from './pages/InventoryPage';
import { AuditPage } from './pages/AuditPage';
import { ReportsPage } from './pages/ReportsPage';
import { SalesOrdersPage } from './pages/sales/SalesOrdersPage';
import { SalesOrderDetailPage } from './pages/sales/SalesOrderDetailPage';
import { InvoicesPage } from './pages/sales/InvoicesPage';
import { InvoiceDetailPage } from './pages/sales/InvoiceDetailPage';
import { PurchaseOrdersPage } from './pages/purchasing/PurchaseOrdersPage';
import { PurchaseOrderDetailPage } from './pages/purchasing/PurchaseOrderDetailPage';
import { SupplierInvoicesPage } from './pages/purchasing/SupplierInvoicesPage';
import { SupplierInvoiceDetailPage } from './pages/purchasing/SupplierInvoiceDetailPage';
import { UsersPage } from './pages/admin/UsersPage';
import { SettingsPage } from './pages/admin/SettingsPage';

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
        <Route path="/" element={<DashboardPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />

        <Route path="/sales-orders" element={<SalesOrdersPage />} />
        <Route path="/sales-orders/:id" element={<SalesOrderDetailPage />} />
        <Route path="/invoices" element={<InvoicesPage />} />
        <Route path="/invoices/:id" element={<InvoiceDetailPage />} />

        <Route path="/purchase-orders" element={<PurchaseOrdersPage />} />
        <Route path="/purchase-orders/:id" element={<PurchaseOrderDetailPage />} />
        <Route path="/supplier-invoices" element={<SupplierInvoicesPage />} />
        <Route path="/supplier-invoices/:id" element={<SupplierInvoiceDetailPage />} />

        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/audit" element={<AuditPage />} />

        <Route path="/admin/users" element={<UsersPage />} />
        <Route path="/admin/settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
