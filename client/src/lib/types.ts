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

/** What POST /api/auth/login returns: either tokens directly, or an MFA challenge to redeem. */
export interface LoginResult {
  mfaRequired: boolean;
  mfaChallengeToken?: string | null;
  tokens?: AuthResult | null;
}

export interface UserIdentity {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  mfaEnabled: boolean;
}

export interface MfaSetup {
  sharedKey: string;
  authenticatorUri: string;
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

// --- Sales ---
export type SalesOrderStatus = 'Draft' | 'Confirmed' | 'Fulfilled' | 'Cancelled';
export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Void';
export type FiscalStatus = 'NotSubmitted' | 'Submitted' | 'Failed';
export type PaymentMethod = 'Cash' | 'Card' | 'BankTransfer' | 'MobileWallet' | 'Cheque';

export interface SalesOrderSummary {
  id: string;
  number: string;
  customerId: string;
  orderDate: string;
  status: SalesOrderStatus;
  subTotal: number;
}
export interface SalesOrderLine {
  id: string;
  productId: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}
export interface SalesOrderDetail extends SalesOrderSummary {
  warehouseId: string;
  notes?: string | null;
  lines: SalesOrderLine[];
}

export interface InvoiceSummary {
  id: string;
  number: string;
  customerId: string;
  issueDate: string;
  status: InvoiceStatus;
  fiscalStatus: FiscalStatus;
  total: number;
  balance: number;
}
export interface InvoiceLine {
  id: string;
  productId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  lineSubTotal: number;
  lineTax: number;
  lineTotal: number;
}
export interface InvoiceDetail {
  id: string;
  number: string;
  customerId: string;
  salesOrderId?: string | null;
  issueDate: string;
  dueDate?: string | null;
  status: InvoiceStatus;
  fiscalStatus: FiscalStatus;
  fiscalReference?: string | null;
  subTotal: number;
  taxTotal: number;
  total: number;
  amountPaid: number;
  balance: number;
  lines: InvoiceLine[];
}
export interface PaymentRecord {
  id: string;
  number: string;
  invoiceId: string;
  amount: number;
  paymentDate: string;
  method: PaymentMethod;
  reference?: string | null;
}

// --- Purchasing ---
export type PurchaseOrderStatus = 'Draft' | 'Confirmed' | 'PartiallyReceived' | 'Received' | 'Cancelled';
export type SupplierInvoiceStatus = 'Draft' | 'Approved' | 'PartiallyPaid' | 'Paid' | 'Cancelled';

export interface Supplier {
  id: string;
  code: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  taxIdentificationNumber?: string | null;
  isActive: boolean;
}
export interface PurchaseOrderSummary {
  id: string;
  number: string;
  supplierId: string;
  orderDate: string;
  status: PurchaseOrderStatus;
  subTotal: number;
}
export interface PurchaseOrderLine {
  id: string;
  productId: string;
  quantity: number;
  unitCost: number;
  quantityReceived: number;
  outstandingQuantity: number;
  lineTotal: number;
}
export interface PurchaseOrderDetail extends PurchaseOrderSummary {
  warehouseId: string;
  notes?: string | null;
  lines: PurchaseOrderLine[];
}

export interface SupplierInvoiceSummary {
  id: string;
  number: string;
  supplierId: string;
  issueDate: string;
  status: SupplierInvoiceStatus;
  total: number;
  balance: number;
}
export interface SupplierInvoiceLine {
  id: string;
  productId: string;
  description: string;
  quantity: number;
  unitCost: number;
  taxRate: number;
  lineSubTotal: number;
  lineTax: number;
  lineTotal: number;
}
export interface SupplierInvoiceDetail {
  id: string;
  number: string;
  supplierId: string;
  purchaseOrderId?: string | null;
  supplierReference?: string | null;
  issueDate: string;
  dueDate?: string | null;
  status: SupplierInvoiceStatus;
  subTotal: number;
  taxTotal: number;
  total: number;
  amountPaid: number;
  balance: number;
  lines: SupplierInvoiceLine[];
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  branchId: string;
  branchName: string;
  isActive: boolean;
}

export interface UserAccount {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  isActive: boolean;
  /** Branch scope; null = unrestricted (sees every branch). */
  branchId?: string | null;
  branchName?: string | null;
}

/** RFC 7807 problem-details as returned by the API on failure. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
