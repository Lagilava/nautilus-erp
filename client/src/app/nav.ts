import {
  LayoutDashboard,
  Package,
  Users,
  Truck,
  Boxes,
  ShoppingCart,
  FileText,
  ClipboardList,
  ReceiptText,
  BarChart3,
  ScrollText,
  UserCog,
  SlidersHorizontal,
  Landmark,
  BookOpen,
  CalendarClock,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  roles?: string[]; // if set, only shown to users with one of these roles
}

export interface NavSection {
  heading: string;
  items: NavItem[];
}

export const NAV: NavSection[] = [
  {
    heading: 'Overview',
    items: [{ to: '/', label: 'Dashboard', icon: LayoutDashboard }],
  },
  {
    heading: 'Catalog',
    items: [
      { to: '/products', label: 'Products', icon: Package },
      { to: '/inventory', label: 'Inventory', icon: Boxes },
    ],
  },
  {
    heading: 'Sales',
    items: [
      { to: '/customers', label: 'Customers', icon: Users },
      { to: '/sales-orders', label: 'Sales Orders', icon: ShoppingCart },
      { to: '/invoices', label: 'Invoices', icon: FileText },
    ],
  },
  {
    heading: 'Purchasing',
    items: [
      { to: '/suppliers', label: 'Suppliers', icon: Truck },
      { to: '/purchase-orders', label: 'Purchase Orders', icon: ClipboardList },
      { to: '/supplier-invoices', label: 'Supplier Invoices', icon: ReceiptText },
    ],
  },
  {
    heading: 'Accounting',
    items: [
      {
        to: '/accounting/chart-of-accounts',
        label: 'Chart of Accounts',
        icon: Landmark,
        roles: ['Administrator', 'Manager'],
      },
      {
        to: '/accounting/journal-entries',
        label: 'Journal Entries',
        icon: BookOpen,
        roles: ['Administrator', 'Manager'],
      },
      {
        to: '/accounting/bank-reconciliation',
        label: 'Bank Reconciliation',
        icon: Landmark,
        roles: ['Administrator', 'Manager'],
      },
      {
        to: '/accounting/periods',
        label: 'Accounting Periods',
        icon: CalendarClock,
        roles: ['Administrator', 'Manager'],
      },
    ],
  },
  {
    heading: 'Insights',
    items: [
      { to: '/reports', label: 'Reports', icon: BarChart3 },
      { to: '/audit', label: 'Audit Trail', icon: ScrollText, roles: ['Administrator'] },
    ],
  },
  {
    heading: 'Administration',
    items: [
      { to: '/admin/users', label: 'Users', icon: UserCog, roles: ['Administrator'] },
      { to: '/admin/settings', label: 'Settings', icon: SlidersHorizontal, roles: ['Administrator'] },
    ],
  },
];
