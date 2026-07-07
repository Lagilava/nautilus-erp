import { Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { AppLayout } from './app/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { ProductsPage } from './pages/ProductsPage';
import { CustomersPage } from './pages/CustomersPage';
import { SuppliersPage } from './pages/SuppliersPage';
import { InventoryPage } from './pages/InventoryPage';
import { AuditPage } from './pages/AuditPage';
import { ReportsPage } from './pages/ReportsPage';
import { ComingSoon } from './pages/ComingSoon';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/" element={<DashboardPage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />
        <Route path="/sales-orders" element={<ComingSoon title="Sales Orders" endpoint="/api/sales-orders" />} />
        <Route path="/invoices" element={<ComingSoon title="Invoices" endpoint="/api/invoices" />} />
        <Route
          path="/purchase-orders"
          element={<ComingSoon title="Purchase Orders" endpoint="/api/purchase-orders" />}
        />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/audit" element={<AuditPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
