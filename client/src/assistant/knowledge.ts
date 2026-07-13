/**
 * The assistant's knowledge base. Each entry is a self-contained help topic:
 * how to do something, what a concept means, or where to find a screen. The
 * retrieval engine (see engine.ts) indexes the `title`, `keywords` and
 * `utterances` of every entry and ranks them against the user's question.
 *
 * This is deliberately data, not code — new topics can be added here without
 * touching the engine. All content is written for Nautilus ERP staff.
 */

export interface KbLink {
  /** Visible label. */
  label: string;
  /** In-app route to navigate to when clicked. */
  to: string;
}

export interface KbEntry {
  id: string;
  /** Human title, shown as the answer heading. */
  title: string;
  /** Module this belongs to, for grouping and intent boosts. */
  module:
    | 'general'
    | 'products'
    | 'inventory'
    | 'customers'
    | 'suppliers'
    | 'sales'
    | 'purchasing'
    | 'reports'
    | 'account'
    | 'admin';
  /** Extra terms to index (abbreviations, jargon) beyond the answer text. */
  keywords: string[];
  /** Example ways a user might phrase this question — heavily weighted. */
  utterances: string[];
  /** Short prose answer shown above the steps. */
  answer: string;
  /** Optional ordered walkthrough. */
  steps?: string[];
  /** Deep links surfaced under the answer. */
  links?: KbLink[];
  /** Ids of related topics offered as follow-up chips. */
  related?: string[];
  /** Roles this topic is relevant to; if set, hidden from other roles. */
  roles?: string[];
}

export const KNOWLEDGE: KbEntry[] = [
  // ─── General / orientation ────────────────────────────────────────────────
  {
    id: 'orientation',
    title: 'Getting around Nautilus ERP',
    module: 'general',
    keywords: ['navigate', 'menu', 'sidebar', 'sections', 'modules', 'overview', 'start', 'begin', 'lost'],
    utterances: [
      'how do i use this system', 'where do i start', 'help me get started',
      'i am new here', 'how does this work', 'what can this system do',
      'give me a tour', 'i am lost',
    ],
    answer:
      'Nautilus is organised into modules in the left sidebar: Catalog (Products, Inventory), Sales (Customers, Sales Orders, Invoices), Purchasing (Suppliers, Purchase Orders, Supplier Invoices), and Insights (Reports, Audit). The Dashboard is your home base with the key numbers. Press Ctrl+K anywhere to jump to a page or find a record instantly.',
    steps: [
      'Open the Dashboard for today’s snapshot of sales, receivables and stock.',
      'Use the left sidebar to move between modules.',
      'Press Ctrl+K to search records or jump straight to a screen.',
      'Ask me anytime how to do a specific task — e.g. “how do I create an invoice?”',
    ],
    links: [{ label: 'Open Dashboard', to: '/' }],
    related: ['search', 'create-sales-order', 'create-invoice'],
  },
  {
    id: 'search',
    title: 'Search and jump to anything (Ctrl+K)',
    module: 'general',
    keywords: ['command palette', 'quick find', 'shortcut', 'ctrl k', 'jump', 'lookup', 'find record'],
    utterances: [
      'how do i search', 'how to find a customer quickly', 'find an order',
      'is there a shortcut', 'quick way to open a page', 'how do i look something up',
    ],
    answer:
      'Press Ctrl+K (Cmd+K on Mac) to open the command palette. Start typing and it searches products, customers, suppliers, sales orders, invoices and purchase orders live, and also lets you jump to any page. Use the arrow keys to move and Enter to open.',
    links: [{ label: 'Open Dashboard', to: '/' }],
    related: ['orientation'],
  },
  {
    id: 'concept-ar-ap',
    title: 'What are Accounts Receivable and Payable?',
    module: 'general',
    keywords: ['receivable', 'payable', 'owed', 'debt', 'outstanding', 'balance', 'money owed'],
    utterances: [
      'what is accounts receivable', 'what does accounts payable mean',
      'what is ar and ap', 'how much do customers owe us', 'how much do we owe suppliers',
      'what is the difference between receivable and payable',
    ],
    answer:
      'Accounts Receivable (AR) is money customers owe you — the total of unpaid customer invoices. Accounts Payable (AP) is money you owe suppliers — the total of unpaid supplier invoices. Both appear on the Dashboard, and the Reports page breaks each one down by how overdue it is.',
    links: [
      { label: 'View Reports', to: '/reports' },
      { label: 'Open Dashboard', to: '/' },
    ],
    related: ['aging-report', 'record-payment'],
  },

  // ─── Products ─────────────────────────────────────────────────────────────
  {
    id: 'create-product',
    title: 'Add a new product',
    module: 'products',
    keywords: ['new product', 'catalog', 'sku', 'price', 'add item', 'create item'],
    utterances: [
      'how do i add a product', 'create a new item', 'how to set up a product',
      'add something to the catalog', 'register a new product',
    ],
    answer:
      'Products live in the Catalog. Add one from the Products page, giving it a name, SKU, and pricing. Once created it can be added to sales and purchase orders.',
    steps: [
      'Go to Products in the sidebar.',
      'Click “New product”.',
      'Enter the name, SKU, unit price and any cost/description fields.',
      'Save. The product is now available to orders and inventory.',
    ],
    links: [{ label: 'Open Products', to: '/products' }],
    related: ['adjust-inventory', 'low-stock'],
  },

  // ─── Inventory ────────────────────────────────────────────────────────────
  {
    id: 'adjust-inventory',
    title: 'Adjust stock levels',
    module: 'inventory',
    keywords: ['stock', 'quantity', 'count', 'stock take', 'increase', 'decrease', 'write off', 'on hand'],
    utterances: [
      'how do i change stock levels', 'adjust inventory', 'update quantity on hand',
      'record a stock take', 'my stock count is wrong', 'how to add stock',
    ],
    answer:
      'Use the Inventory page to see quantity on hand for every product and make adjustments — for example after a stock take, receiving goods, or writing off damage. Each adjustment is recorded so the history stays auditable.',
    steps: [
      'Open Inventory from the sidebar.',
      'Find the product (type in the search box to filter).',
      'Enter the adjustment or new counted quantity and a reason.',
      'Save — the on-hand figure and inventory value update immediately.',
    ],
    links: [{ label: 'Open Inventory', to: '/inventory' }],
    related: ['low-stock', 'create-product', 'reorder-draft'],
  },
  {
    id: 'low-stock',
    title: 'Find and handle low-stock items',
    module: 'inventory',
    keywords: ['low stock', 'reorder point', 'running out', 'shortage', 'replenish', 'out of stock'],
    utterances: [
      'which items are low on stock', 'what needs reordering', 'show low stock',
      'what is running out', 'how do i know what to restock',
    ],
    answer:
      'The Dashboard shows a “Low-stock items” count under Needs attention, and the Inventory page flags each item that has fallen below its reorder point. From a low supplier item you can generate a draft purchase order to restock.',
    steps: [
      'Check the Dashboard → Needs attention → Low-stock items.',
      'Click through to Inventory to see exactly which products are low.',
      'Create a reorder draft PO to replenish (ask me “how do I create a reorder draft”).',
    ],
    links: [
      { label: 'Open Inventory', to: '/inventory' },
      { label: 'Open Dashboard', to: '/' },
    ],
    related: ['reorder-draft', 'adjust-inventory', 'create-purchase-order'],
  },

  // ─── Customers ────────────────────────────────────────────────────────────
  {
    id: 'create-customer',
    title: 'Add a new customer',
    module: 'customers',
    keywords: ['new customer', 'client', 'contact', 'account', 'buyer'],
    utterances: [
      'how do i add a customer', 'create a client', 'set up a new customer',
      'register a buyer', 'add a new account',
    ],
    answer:
      'Add customers from the Customers page. Capture their name, contact details and any billing information; once saved they can be selected on sales orders and invoices.',
    steps: [
      'Go to Customers in the sidebar.',
      'Click “New customer”.',
      'Fill in name, email, phone and address.',
      'Save. The customer is now selectable on new sales orders.',
    ],
    links: [{ label: 'Open Customers', to: '/customers' }],
    related: ['create-sales-order', 'create-invoice'],
  },

  // ─── Suppliers ────────────────────────────────────────────────────────────
  {
    id: 'create-supplier',
    title: 'Add a new supplier',
    module: 'suppliers',
    keywords: ['new supplier', 'vendor', 'seller', 'source'],
    utterances: [
      'how do i add a supplier', 'create a vendor', 'set up a new supplier',
      'register a vendor',
    ],
    answer:
      'Suppliers are added from the Suppliers page. Record their name and contact details so they can be used on purchase orders and supplier invoices.',
    steps: [
      'Go to Suppliers in the sidebar.',
      'Click “New supplier”.',
      'Enter name, contact and payment terms.',
      'Save. The supplier is now available on purchase orders.',
    ],
    links: [{ label: 'Open Suppliers', to: '/suppliers' }],
    related: ['create-purchase-order', 'supplier-invoice'],
  },

  // ─── Sales ────────────────────────────────────────────────────────────────
  {
    id: 'create-sales-order',
    title: 'Create a sales order',
    module: 'sales',
    keywords: ['new sale', 'order', 'sell', 'line items', 'quote', 'customer order'],
    utterances: [
      'how do i create a sales order', 'make a new sale', 'raise an order for a customer',
      'sell to a customer', 'add a sales order', 'how to take an order',
    ],
    answer:
      'A sales order records what a customer wants to buy. Create one from the Sales Orders page: pick the customer, add product line items with quantities, and save. From the order you can later raise an invoice.',
    steps: [
      'Go to Sales Orders and click “New sales order”.',
      'Choose the customer.',
      'Add each product as a line item with quantity and price.',
      'Review the total and Save.',
      'Open the order to fulfil it or convert it into an invoice.',
    ],
    links: [{ label: 'Open Sales Orders', to: '/sales-orders' }],
    related: ['create-invoice', 'create-customer', 'sales-order-status'],
  },
  {
    id: 'sales-order-status',
    title: 'Sales order statuses explained',
    module: 'sales',
    keywords: ['status', 'draft', 'confirmed', 'fulfilled', 'open', 'closed', 'stages'],
    utterances: [
      'what do the order statuses mean', 'what is a draft order',
      'what does fulfilled mean', 'stages of a sales order', 'is my order open or closed',
    ],
    answer:
      'A sales order moves through stages: Draft (still being edited), Confirmed (agreed, ready to fulfil), Fulfilled (goods sent), and it is considered closed once invoiced/paid. “Open sales orders” on the Dashboard counts those not yet completed.',
    links: [{ label: 'Open Sales Orders', to: '/sales-orders' }],
    related: ['create-sales-order', 'create-invoice'],
  },
  {
    id: 'create-invoice',
    title: 'Create an invoice',
    module: 'sales',
    keywords: ['bill', 'charge', 'invoice customer', 'raise invoice', 'new invoice'],
    utterances: [
      'how do i create an invoice', 'bill a customer', 'raise an invoice',
      'invoice a sales order', 'make a new invoice', 'how to charge a client',
    ],
    answer:
      'Invoices bill the customer for a sale. You can raise one from a sales order (carrying its line items across) or create a standalone invoice from the Invoices page. Once issued, its total adds to Accounts Receivable until paid.',
    steps: [
      'Open the sales order and choose to invoice it — or go to Invoices → “New invoice”.',
      'Confirm the customer and line items.',
      'Set the invoice/due date and Save.',
      'Send it to the customer; record the payment when it arrives.',
    ],
    links: [{ label: 'Open Invoices', to: '/invoices' }],
    related: ['record-payment', 'create-sales-order', 'aging-report'],
  },
  {
    id: 'record-payment',
    title: 'Record a customer payment',
    module: 'sales',
    keywords: ['pay', 'paid', 'receipt', 'settle', 'mark paid', 'payment received'],
    utterances: [
      'how do i record a payment', 'mark an invoice as paid', 'customer paid me',
      'settle an invoice', 'log a payment', 'how to receive payment',
    ],
    answer:
      'Open the invoice and record the payment against it. This reduces Accounts Receivable and updates the invoice status. Partial payments are supported — the outstanding balance stays on the invoice until fully settled.',
    steps: [
      'Go to Invoices and open the one that was paid.',
      'Record a payment with the amount and date received.',
      'Save — the status and receivables update automatically.',
    ],
    links: [{ label: 'Open Invoices', to: '/invoices' }],
    related: ['create-invoice', 'aging-report', 'concept-ar-ap'],
  },
  {
    id: 'attachments',
    title: 'Attach files to an order or invoice',
    module: 'sales',
    keywords: ['attachment', 'upload', 'document', 'file', 'pdf', 'photo', 'proof'],
    utterances: [
      'how do i attach a file', 'upload a document to an order',
      'add a pdf to an invoice', 'attach proof of delivery',
    ],
    answer:
      'Sales and purchasing detail pages have an Attachments panel. Use it to upload supporting documents — delivery notes, signed POs, receipts — which stay linked to that record for anyone who opens it later.',
    steps: [
      'Open the sales order, invoice, purchase order or supplier invoice.',
      'Find the Attachments panel on the detail page.',
      'Drag a file in or click to browse, then upload.',
    ],
    related: ['create-invoice', 'create-purchase-order'],
  },

  // ─── Purchasing ───────────────────────────────────────────────────────────
  {
    id: 'create-purchase-order',
    title: 'Create a purchase order',
    module: 'purchasing',
    keywords: ['buy', 'order stock', 'po', 'procure', 'new purchase order', 'restock order'],
    utterances: [
      'how do i create a purchase order', 'raise a po', 'order stock from a supplier',
      'buy inventory', 'make a purchase order', 'how to procure goods',
    ],
    answer:
      'A purchase order tells a supplier what you want to buy. Create one from the Purchase Orders page: choose the supplier, add products with quantities and costs, and save. When the goods arrive you receive them against the PO.',
    steps: [
      'Go to Purchase Orders and click “New purchase order”.',
      'Choose the supplier.',
      'Add product line items with quantities and unit cost.',
      'Save, then send it to the supplier.',
      'Receive the goods against the PO when they arrive to update stock.',
    ],
    links: [{ label: 'Open Purchase Orders', to: '/purchase-orders' }],
    related: ['reorder-draft', 'receive-goods', 'supplier-invoice'],
  },
  {
    id: 'reorder-draft',
    title: 'Generate a reorder draft to restock',
    module: 'purchasing',
    keywords: ['reorder', 'restock', 'auto po', 'replenish', 'draft purchase order', 'low stock order'],
    utterances: [
      'how do i reorder stock', 'create a reorder draft', 'auto generate a purchase order',
      'restock low items', 'make a po for low stock automatically',
    ],
    answer:
      'Nautilus can build a draft purchase order for items that have fallen below their reorder point, so you don’t have to key them in by hand. Review the suggested quantities, adjust if needed, pick the supplier, and confirm to turn it into a real PO.',
    steps: [
      'Check low-stock items on Inventory or the Dashboard.',
      'Choose to create a reorder draft — suggested products and quantities are pre-filled.',
      'Review and adjust the lines, set the supplier.',
      'Confirm to create the purchase order.',
    ],
    links: [
      { label: 'Open Purchase Orders', to: '/purchase-orders' },
      { label: 'Open Inventory', to: '/inventory' },
    ],
    related: ['low-stock', 'create-purchase-order', 'receive-goods'],
  },
  {
    id: 'receive-goods',
    title: 'Receive goods against a purchase order',
    module: 'purchasing',
    keywords: ['receive', 'goods in', 'delivery', 'arrived', 'stock in', 'grn'],
    utterances: [
      'how do i receive stock', 'goods arrived from supplier', 'mark a po as received',
      'record a delivery', 'receive against a purchase order',
    ],
    answer:
      'When a supplier’s delivery arrives, receive it against the purchase order. This raises the inventory on hand for those products and moves the PO towards completion. You can receive partially if only some lines arrived.',
    steps: [
      'Open the purchase order from the Purchase Orders page.',
      'Record the received quantities for each line.',
      'Save — inventory increases and the PO status updates.',
    ],
    links: [{ label: 'Open Purchase Orders', to: '/purchase-orders' }],
    related: ['create-purchase-order', 'supplier-invoice', 'adjust-inventory'],
  },
  {
    id: 'supplier-invoice',
    title: 'Record a supplier invoice (bill)',
    module: 'purchasing',
    keywords: ['vendor bill', 'supplier bill', 'payable', 'accounts payable', 'we owe'],
    utterances: [
      'how do i record a supplier invoice', 'enter a vendor bill', 'log a bill we received',
      'add a supplier invoice', 'record what we owe a supplier',
    ],
    answer:
      'A supplier invoice is a bill you received from a vendor. Record it from the Supplier Invoices page (often against a purchase order). Its total adds to Accounts Payable until you pay it.',
    steps: [
      'Go to Supplier Invoices and click “New supplier invoice”.',
      'Select the supplier and, if relevant, the related purchase order.',
      'Enter the amounts and due date.',
      'Save — it now appears in Accounts Payable.',
    ],
    links: [{ label: 'Open Supplier Invoices', to: '/supplier-invoices' }],
    related: ['create-purchase-order', 'concept-ar-ap', 'aging-report'],
  },

  // ─── Reports ──────────────────────────────────────────────────────────────
  {
    id: 'aging-report',
    title: 'Read the aging reports (overdue AR/AP)',
    module: 'reports',
    keywords: ['aging', 'ageing', 'overdue', 'buckets', 'receivable report', 'payable report', '30 60 90'],
    utterances: [
      'how do i see overdue invoices', 'what is the aging report', 'show me who is overdue',
      'which customers are late paying', 'how much is past due', 'aging buckets',
    ],
    answer:
      'The Reports page includes aging reports for receivables and payables. They group outstanding balances into buckets — current, 1–30 days, 31–60, 61–90 and 90+ overdue — so you can see who to chase and which bills are becoming urgent.',
    steps: [
      'Go to Reports in the sidebar.',
      'Open the Receivables (or Payables) aging report.',
      'Read across the buckets; the far-right columns are the most overdue.',
    ],
    links: [{ label: 'Open Reports', to: '/reports' }],
    related: ['concept-ar-ap', 'record-payment', 'supplier-invoice'],
  },
  {
    id: 'reports-overview',
    title: 'What reports are available',
    module: 'reports',
    keywords: ['insights', 'analytics', 'export', 'statements', 'sales report'],
    utterances: [
      'what reports can i run', 'where are the reports', 'show me analytics',
      'how do i see business performance',
    ],
    answer:
      'The Reports page gathers your insights — sales performance, receivables and payables aging, and stock value. The Dashboard also charts invoiced sales over the last six months. Use these to spot trends, overdue accounts and slow-moving stock.',
    links: [
      { label: 'Open Reports', to: '/reports' },
      { label: 'Open Dashboard', to: '/' },
    ],
    related: ['aging-report', 'concept-ar-ap'],
  },

  // ─── Account / self-service ───────────────────────────────────────────────
  {
    id: 'change-password',
    title: 'Change your password',
    module: 'account',
    keywords: ['reset password', 'update password', 'new password', 'security'],
    utterances: [
      'how do i change my password', 'update my password', 'i want a new password',
      'reset my password', 'change my login',
    ],
    answer:
      'Change your password from your Profile page while signed in. If you’re locked out, use the “Forgot your password?” link on the sign-in screen to receive a reset email.',
    steps: [
      'Click your name (top-right) to open your Profile.',
      'Find the password section and enter your current and new password.',
      'Save.',
    ],
    links: [{ label: 'Open Profile', to: '/profile' }],
    related: ['setup-mfa', 'edit-profile'],
  },
  {
    id: 'setup-mfa',
    title: 'Set up two-factor authentication (MFA)',
    module: 'account',
    keywords: ['2fa', 'mfa', 'authenticator', 'verification code', 'otp', 'secure login'],
    utterances: [
      'how do i enable mfa', 'set up two factor', 'add an authenticator',
      'turn on 2fa', 'i want a verification code at login',
    ],
    answer:
      'Enable multi-factor authentication from your Profile for stronger security. You’ll scan a QR code with an authenticator app; after that, sign-in asks for a 6-digit code. Keep your recovery codes somewhere safe in case you lose the app.',
    steps: [
      'Open your Profile from the top-right menu.',
      'Start MFA setup and scan the QR code with your authenticator app.',
      'Enter the 6-digit code to confirm, and save your recovery codes.',
    ],
    links: [{ label: 'Open Profile', to: '/profile' }],
    related: ['change-password', 'edit-profile'],
  },
  {
    id: 'edit-profile',
    title: 'Edit your profile details',
    module: 'account',
    keywords: ['name', 'contact', 'my details', 'account settings', 'personal'],
    utterances: [
      'how do i change my name', 'update my profile', 'edit my details',
      'change my email', 'my personal information',
    ],
    answer:
      'Your Profile page holds your name and contact details. Update them there and save — your name in the top bar refreshes immediately.',
    links: [{ label: 'Open Profile', to: '/profile' }],
    related: ['change-password', 'setup-mfa'],
  },
  {
    id: 'sign-out',
    title: 'Sign out',
    module: 'account',
    keywords: ['log out', 'logout', 'leave', 'exit', 'end session'],
    utterances: ['how do i log out', 'sign out', 'end my session', 'log off'],
    answer: 'Click the sign-out icon at the far right of the top bar to end your session securely.',
    related: ['change-password'],
  },

  // ─── Admin ────────────────────────────────────────────────────────────────
  {
    id: 'manage-users',
    title: 'Add or manage users',
    module: 'admin',
    roles: ['Administrator'],
    keywords: ['user', 'staff account', 'invite', 'deactivate', 'permissions', 'roles'],
    utterances: [
      'how do i add a user', 'create a staff account', 'invite a colleague',
      'manage users', 'deactivate someone', 'give someone access',
    ],
    answer:
      'Administrators manage staff from Administration → Users. Add a user, assign their roles (which control what they can see and do), and deactivate accounts when someone leaves.',
    steps: [
      'Go to Users under Administration (Administrators only).',
      'Click to add a user and enter their details.',
      'Assign the appropriate role(s).',
      'Save — they can now sign in with the access their role grants.',
    ],
    links: [{ label: 'Open Users', to: '/admin/users' }],
    related: ['roles', 'settings', 'audit'],
  },
  {
    id: 'roles',
    title: 'How roles and permissions work',
    module: 'admin',
    roles: ['Administrator'],
    keywords: ['permission', 'access', 'administrator', 'privileges', 'what can i see'],
    utterances: [
      'what do roles do', 'how do permissions work', 'why can’t i see a page',
      'what is an administrator', 'control access',
    ],
    answer:
      'A user’s role determines which modules and actions they can access. Administrators see everything, including Users, Settings and the Audit Trail; other roles see the operational screens relevant to their work. Set roles on each user in Administration → Users.',
    links: [{ label: 'Open Users', to: '/admin/users' }],
    related: ['manage-users', 'audit'],
  },
  {
    id: 'settings',
    title: 'Business settings',
    module: 'admin',
    roles: ['Administrator'],
    keywords: ['configuration', 'preferences', 'company details', 'tax', 'defaults'],
    utterances: [
      'where are the settings', 'change company details', 'configure the system',
      'set tax rates', 'business configuration',
    ],
    answer:
      'Administration → Settings holds business-wide configuration such as company details and defaults. Changes here affect everyone, so they’re restricted to Administrators.',
    links: [{ label: 'Open Settings', to: '/admin/settings' }],
    related: ['manage-users', 'roles'],
  },
  {
    id: 'audit',
    title: 'The audit trail',
    module: 'admin',
    roles: ['Administrator'],
    keywords: ['history', 'log', 'who changed', 'activity', 'tracking', 'compliance'],
    utterances: [
      'how do i see who changed something', 'view the audit log', 'track changes',
      'what is the audit trail', 'see recent activity',
    ],
    answer:
      'The Audit Trail (Administration) records who created, changed or deleted records and when — useful for compliance and for tracing a mistake. The Dashboard also shows a short recent-activity feed for administrators.',
    links: [{ label: 'Open Audit Trail', to: '/audit' }],
    related: ['manage-users', 'roles'],
  },
];

/** Quick lookup by id, for resolving related/follow-up topics. */
export const KB_BY_ID: Record<string, KbEntry> = Object.fromEntries(
  KNOWLEDGE.map((e) => [e.id, e]),
);

/** A handful of good starter prompts shown on an empty conversation. */
export const STARTER_PROMPTS: string[] = [
  'How do I create a sales order?',
  'How do I record a customer payment?',
  'What items are low on stock?',
  'How do I create a purchase order?',
  'How do I change my password?',
  'What does accounts receivable mean?',
];

/**
 * Maps app routes to the topics most relevant on that screen, so the
 * assistant can see "what page am I looking at" and answer accordingly —
 * without any network call, just a lookup against the current URL.
 */
export interface PageContext {
  /** Route prefix this context applies to. */
  path: string;
  /** Human label for the screen, shown in the assistant header. */
  label: string;
  /** Module the ranking boost applies to. */
  module: KbEntry['module'];
  /** Topic ids most useful on this screen, most relevant first. */
  topicIds: string[];
}

export const PAGE_CONTEXTS: PageContext[] = [
  { path: '/admin/users', label: 'Users', module: 'admin', topicIds: ['manage-users', 'roles'] },
  { path: '/admin/settings', label: 'Business Settings', module: 'admin', topicIds: ['settings'] },
  { path: '/purchase-orders', label: 'Purchase Orders', module: 'purchasing', topicIds: ['create-purchase-order', 'reorder-draft', 'receive-goods'] },
  { path: '/supplier-invoices', label: 'Supplier Invoices', module: 'purchasing', topicIds: ['supplier-invoice', 'concept-ar-ap'] },
  { path: '/sales-orders', label: 'Sales Orders', module: 'sales', topicIds: ['create-sales-order', 'sales-order-status', 'create-invoice'] },
  { path: '/invoices', label: 'Invoices', module: 'sales', topicIds: ['create-invoice', 'record-payment', 'attachments'] },
  { path: '/products', label: 'Products', module: 'products', topicIds: ['create-product', 'adjust-inventory'] },
  { path: '/inventory', label: 'Inventory', module: 'inventory', topicIds: ['adjust-inventory', 'low-stock', 'reorder-draft'] },
  { path: '/customers', label: 'Customers', module: 'customers', topicIds: ['create-customer', 'create-sales-order'] },
  { path: '/suppliers', label: 'Suppliers', module: 'suppliers', topicIds: ['create-supplier', 'create-purchase-order'] },
  { path: '/reports', label: 'Reports', module: 'reports', topicIds: ['aging-report', 'reports-overview'] },
  { path: '/audit', label: 'Audit Trail', module: 'admin', topicIds: ['audit'] },
  { path: '/profile', label: 'My Profile', module: 'account', topicIds: ['edit-profile', 'change-password', 'setup-mfa'] },
  { path: '/', label: 'Dashboard', module: 'general', topicIds: ['orientation', 'low-stock', 'concept-ar-ap'] },
];

/**
 * Resolve the current route to a page context, longest-prefix-first so
 * `/purchase-orders/42` still matches `/purchase-orders` rather than falling
 * through to the Dashboard's `/`.
 */
export function getPageContext(pathname: string): PageContext | null {
  const sorted = [...PAGE_CONTEXTS].sort((a, b) => b.path.length - a.path.length);
  for (const ctx of sorted) {
    if (ctx.path === '/' ? pathname === '/' : pathname === ctx.path || pathname.startsWith(`${ctx.path}/`)) {
      return ctx;
    }
  }
  return null;
}
