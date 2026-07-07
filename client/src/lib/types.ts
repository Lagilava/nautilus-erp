// Shared API contract types. Enums are serialized as strings by the API.

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface AuthResult {
  userId: string;
  email: string;
  roles: string[];
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
}

export interface UserIdentity {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

export interface Dashboard {
  customerCount: number;
  supplierCount: number;
  productCount: number;
  inventoryValue: number;
  lowStockCount: number;
  salesThisMonth: number;
  accountsReceivable: number;
  accountsPayable: number;
  openSalesOrders: number;
  openPurchaseOrders: number;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  description?: string | null;
  barcode?: string | null;
  categoryId: string;
  categoryName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  taxId: string;
  taxCode: string;
  costPrice: number;
  sellingPrice: number;
  isActive: boolean;
}

export interface Customer {
  id: string;
  code: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  taxIdentificationNumber?: string | null;
  creditLimit: number;
  isActive: boolean;
}

export interface Category {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  parentCategoryId?: string | null;
  isActive: boolean;
}

export interface UnitOfMeasure {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface TaxRate {
  id: string;
  percentage: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface Tax {
  id: string;
  code: string;
  name: string;
  treatment: 'Standard' | 'ZeroRated' | 'Exempt';
  isActive: boolean;
  currentRate: number;
  rates: TaxRate[];
}

export interface StockLevel {
  productId: string;
  sku: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  quantityOnHand: number;
  reorderLevel: number;
  isBelowReorder: boolean;
  stockValue: number;
}

/** RFC 7807 problem-details as returned by the API on failure. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
