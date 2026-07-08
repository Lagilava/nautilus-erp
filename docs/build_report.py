"""
Builds docs/Nautilus-ERP-System-Report.docx from facts verified against this repository.

Run:  python docs/build_report.py
Requires: pip install python-docx

Every claim in the generated document was checked against source in src/ and client/.
Nothing here is aspirational: where a capability does not exist, the document says so.
"""

from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor

REPO = Path(__file__).resolve().parent.parent
LOGO = REPO / "client" / "public" / "nautilus-logo.png"
OUT = REPO / "docs" / "Nautilus-ERP-System-Report.docx"


def logo_stream():
    """
    A fresh stream per use: python-docx consumes the file object, and the logo is placed
    twice (title page and page header).
    """
    from io import BytesIO

    return BytesIO(LOGO.read_bytes())

TEAL = RGBColor(0x0E, 0x73, 0x67)
INK = RGBColor(0x14, 0x31, 0x2B)
MUTED = RGBColor(0x5A, 0x6B, 0x66)

TABLE_STYLE = "Light Grid Accent 1"


# --------------------------------------------------------------------------- helpers
def field(paragraph, instruction: str):
    """Insert a Word field code (used for TOC and page numbers)."""
    run = paragraph.add_run()
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = instruction
    sep = OxmlElement("w:fldChar")
    sep.set(qn("w:fldCharType"), "separate")
    placeholder = OxmlElement("w:t")
    placeholder.text = ""
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    for el in (begin, instr, sep, placeholder, end):
        run._r.append(el)


def h1(doc, text):
    return doc.add_heading(text, level=1)


def h2(doc, text):
    return doc.add_heading(text, level=2)


def para(doc, text, *, italic=False, size=None, color=None, align=None, space_after=8):
    p = doc.add_paragraph()
    run = p.add_run(text)
    run.italic = italic
    if size:
        run.font.size = Pt(size)
    if color:
        run.font.color.rgb = color
    if align is not None:
        p.alignment = align
    p.paragraph_format.space_after = Pt(space_after)
    return p


def bullets(doc, items):
    for item in items:
        doc.add_paragraph(item, style="List Bullet")


def table(doc, headers, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(headers))
    try:
        t.style = TABLE_STYLE
    except KeyError:
        t.style = "Table Grid"
    t.autofit = True
    for i, head in enumerate(headers):
        cell = t.rows[0].cells[i]
        cell.text = ""
        run = cell.paragraphs[0].add_run(head)
        run.bold = True
    for row in rows:
        cells = t.add_row().cells
        for i, value in enumerate(row):
            cells[i].text = ""
            cells[i].paragraphs[0].add_run(str(value))
    if widths:
        for row in t.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Inches(w)
    doc.add_paragraph()
    return t


def caption(doc, text):
    p = doc.add_paragraph()
    run = p.add_run(text)
    run.italic = True
    run.font.size = Pt(9)
    run.font.color.rgb = MUTED
    p.paragraph_format.space_after = Pt(12)


def style_document(doc):
    normal = doc.styles["Normal"]
    normal.font.name = "Calibri"
    normal.font.size = Pt(11)
    normal.font.color.rgb = INK
    normal.paragraph_format.space_after = Pt(8)
    normal.paragraph_format.line_spacing = 1.15

    for name, size in (("Heading 1", 18), ("Heading 2", 13.5)):
        st = doc.styles[name]
        st.font.name = "Calibri"
        st.font.size = Pt(size)
        st.font.bold = True
        st.font.color.rgb = TEAL if name == "Heading 1" else INK
        st.paragraph_format.space_before = Pt(16 if name == "Heading 1" else 10)
        st.paragraph_format.space_after = Pt(6)


def add_header_logo(section):
    """Small logo, right-aligned, in the running header."""
    p = section.header.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    label = p.add_run("Nautilus ERP    ")
    label.font.size = Pt(8)
    label.font.color.rgb = MUTED
    p.add_run().add_picture(logo_stream(), width=Inches(0.28))


def add_footer_page_numbers(section):
    p = section.footer.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("Page ")
    run.font.size = Pt(9)
    run.font.color.rgb = MUTED
    field(p, "PAGE")
    run2 = p.add_run(" of ")
    run2.font.size = Pt(9)
    run2.font.color.rgb = MUTED
    field(p, "NUMPAGES")
    for r in p.runs:
        r.font.size = Pt(9)
        r.font.color.rgb = MUTED


# --------------------------------------------------------------------------- document
def build():
    doc = Document()
    style_document(doc)

    first = doc.sections[0]
    for margin in ("top_margin", "bottom_margin", "left_margin", "right_margin"):
        setattr(first, margin, Inches(1))

    # ---------------- title page (no header/footer chrome)
    doc.add_paragraph()
    doc.add_paragraph()
    logo_p = doc.add_paragraph()
    logo_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    logo_p.add_run().add_picture(logo_stream(), width=Inches(2.0))

    t = doc.add_paragraph()
    t.alignment = WD_ALIGN_PARAGRAPH.CENTER
    tr = t.add_run("Nautilus ERP")
    tr.font.size = Pt(34)
    tr.bold = True
    tr.font.color.rgb = TEAL

    para(
        doc,
        "Business Management System — System Report",
        size=14,
        color=INK,
        align=WD_ALIGN_PARAGRAPH.CENTER,
    )
    para(
        doc,
        "An integrated sales, purchasing and inventory platform for a Fiji-registered business",
        size=11,
        color=MUTED,
        align=WD_ALIGN_PARAGRAPH.CENTER,
        italic=True,
    )
    doc.add_paragraph()
    para(
        doc,
        "Purpose: to describe, accurately and without exaggeration, what this system does today, "
        "who may use each part of it, and what it does not yet do.",
        size=10.5,
        color=INK,
        align=WD_ALIGN_PARAGRAPH.CENTER,
    )
    doc.add_paragraph()
    para(doc, "9 July 2026", size=11, color=MUTED, align=WD_ALIGN_PARAGRAPH.CENTER)
    para(
        doc,
        "Prepared from the source code of the repository, not from a specification.",
        size=9,
        color=MUTED,
        align=WD_ALIGN_PARAGRAPH.CENTER,
        italic=True,
    )

    # ---------------- body section with header/footer
    body = doc.add_section(WD_SECTION.NEW_PAGE)
    for margin in ("top_margin", "bottom_margin", "left_margin", "right_margin"):
        setattr(body, margin, Inches(1))
    body.header.is_linked_to_previous = False
    body.footer.is_linked_to_previous = False
    add_header_logo(body)
    add_footer_page_numbers(body)
    # keep the title page clean
    first.footer.is_linked_to_previous = False
    first.header.is_linked_to_previous = False

    # ---------------- table of contents
    h1(doc, "Contents")
    toc_p = doc.add_paragraph()
    field(toc_p, r'TOC \o "1-3" \h \z \u')
    caption(
        doc,
        "This table of contents is a live Word field. If it appears empty or out of date, "
        "click anywhere inside it and press F9 (or Ctrl+A then F9) to refresh it.",
    )

    doc.add_page_break()

    # ============================================================ 1
    h1(doc, "1. Executive summary")
    para(
        doc,
        "Nautilus ERP is an enterprise resource planning system — a single application in which a "
        "business records what it sells, what it buys, and what it holds in stock, instead of "
        "spreading those records across spreadsheets and separate tools. It is built for a "
        "Fiji-registered trading business: a supermarket, wholesaler, distributor or pharmacy that "
        "issues VAT tax invoices in Fiji dollars and needs an auditable record of every stock "
        "movement and every payment.",
    )
    para(
        doc,
        "The system covers six areas of the business. A catalogue holds products, categories, units "
        "of measure, currencies and tax rates. An inventory module tracks stock in warehouses and "
        "values it using FIFO — first-in, first-out, meaning the oldest stock you bought is treated "
        "as the first stock you sold. A sales module carries a customer order through to an issued "
        "invoice and the payments against it. A purchasing module does the mirror image: a purchase "
        "order, the goods receipt when the delivery arrives, the supplier's bill and the payment. A "
        "reporting module summarises the position and exports it. An administration module manages "
        "users, roles, branches and company details, and exposes an audit trail of every change.",
    )
    para(
        doc,
        "Three independent layers of access control sit over all of this. Roles decide what kind of "
        "action a person may take. Branch scoping decides which records they can see at all. "
        "Segregation of duties — five maker-checker rules — prevents one person from completing a "
        "purchase-to-payment chain on their own, which is the classic route by which a business is "
        "defrauded by its own staff. Customers never sign in; this is a staff system.",
    )
    para(
        doc,
        "Two limitations deserve to appear this early, because a report that buries them is not worth "
        "reading. First, the system does not submit invoices to the Fiji Revenue and Customs Service. "
        "The integration point for FRCS fiscalization exists and is honest about itself: every issued "
        "invoice is recorded as NotSubmitted. Nothing is faked as accredited. Second, Nautilus ERP is "
        "not an accounting system. It has no general ledger and no double-entry bookkeeping. It "
        "records the operational truth of the business and can feed an accountant; it does not replace "
        "one. Section 6 sets out the full list of known limitations.",
    )

    # ============================================================ 2
    h1(doc, "2. What the system does")
    para(
        doc,
        "The following six modules make up the working system. Each is implemented end to end: a "
        "domain model, application logic, an HTTP endpoint, and a screen in the browser client.",
    )

    table(
        doc,
        ["Module", "What it holds", "What you can do with it"],
        [
            (
                "Catalogue and reference data",
                "Products, categories, units of measure, currencies, taxes and tax rates, branches, warehouses",
                "Maintain the shared vocabulary every other module depends on; set VAT rates with effective dates",
            ),
            (
                "Inventory",
                "Stock levels per product and warehouse, FIFO cost layers, an immutable stock-movement ledger",
                "Receive, issue, transfer and adjust stock; set reorder levels; view stock valuation and full movement history",
            ),
            (
                "Sales",
                "Customers, sales orders, invoices with line-level VAT, customer payments",
                "Take an order, fulfil it from stock, raise and issue a tax invoice, record part or full payment",
            ),
            (
                "Purchasing",
                "Suppliers, purchase orders, goods receipts, supplier invoices, supplier payments",
                "Raise and approve a purchase order, receive goods into stock, approve the supplier's bill, pay it",
            ),
            (
                "Reporting",
                "Dashboard key figures, inventory valuation, sales trend",
                "Read the current position; export the inventory valuation report as CSV, Excel or PDF; print a Fiji tax invoice as PDF",
            ),
            (
                "Administration",
                "User accounts, roles, branch assignment, company profile, audit log",
                "Create users, assign roles and a branch, activate or deactivate accounts, edit company and tax details, review who changed what",
            ),
        ],
        widths=[1.6, 2.2, 2.7],
    )
    caption(doc, "Table 1 — The six modules of Nautilus ERP.")

    h2(doc, "2.1 Catalogue and reference data")
    para(
        doc,
        "Nothing else works until the catalogue is right. A product carries its category, its unit of "
        "measure, its pricing and the tax that applies to it. Tax itself is modelled properly rather "
        "than as a number on an invoice: a tax has a treatment — Standard, ZeroRated or Exempt — and a "
        "Standard tax carries a history of effective-dated rates. Changing Fiji's VAT rate is therefore "
        "a data operation performed by an administrator, not a change to the software.",
    )

    h2(doc, "2.2 Inventory")
    para(
        doc,
        "Stock is held per product per warehouse. Every change to a quantity on hand is written as an "
        "entry in a ledger that is never edited or deleted: a receipt, an issue, a positive or negative "
        "stock-take adjustment, or the two halves of a transfer between warehouses. Because the ledger "
        "is immutable, the stock position at any past date can be reconstructed and defended.",
    )
    para(
        doc,
        "Valuation uses FIFO cost layers. When stock arrives, a layer is created recording the quantity "
        "and the unit cost paid. When stock goes out, the oldest layers are consumed first and the cost "
        "of goods sold is taken from them. A transfer between warehouses preserves the original cost, "
        "so moving stock does not invent or destroy value. Attempting to issue, transfer or adjust more "
        "stock than exists is refused outright; nothing is half-posted.",
    )

    h2(doc, "2.3 Sales, purchasing and reporting")
    para(
        doc,
        "Sales and purchasing are described in detail in section 4, because their value lies in the "
        "sequence of states each document passes through. Reporting is deliberately narrow: a dashboard "
        "of key figures (customer, supplier and product counts, inventory value, low-stock count, sales "
        "this month, money owed by customers and to suppliers, open orders on both sides), a sales trend, "
        "and an inventory valuation report exportable as CSV, Excel or PDF. A customer invoice can also "
        "be rendered as a Fiji-format PDF tax invoice.",
    )

    # ============================================================ 3
    h1(doc, "3. Who uses it, and what they can see")
    para(
        doc,
        "Access is decided by three separate mechanisms, applied one after the other. A request must "
        "pass all three. They answer three different questions: what kind of action is this person "
        "allowed to perform; which records are they allowed to touch; and does this particular action "
        "conflict with something they already did on the same document.",
    )

    h2(doc, "3.1 Roles")
    para(
        doc,
        "There are three roles. Every signed-in user holds at least one. Customers and suppliers do not "
        "have accounts and cannot sign in — Nautilus ERP is an internal staff system, and there is no "
        "public self-registration of any kind.",
    )
    table(
        doc,
        ["Role", "Can read", "Can change", "Cannot"],
        [
            (
                "Administrator",
                "Everything, including the audit trail",
                "Everything: all transactions, plus users, roles, branch assignment, company profile and reference data",
                "Bypass segregation-of-duties rules; those apply regardless of role",
            ),
            (
                "Manager",
                "All operational data within their branch scope",
                "Transactions and reference data: stock movements, sales orders, invoices, purchase orders, goods receipts, supplier invoices and payments",
                "Manage user accounts or roles; view the audit trail; edit the company profile",
            ),
            (
                "Staff",
                "Operational data within their branch scope — orders, invoices, stock levels, reports",
                "Nothing in the transactional modules; staff are read-only there, and may update their own profile and password",
                "Post stock movements, confirm or approve documents, record payments, manage users",
            ),
        ],
        widths=[1.1, 1.9, 2.4, 1.6],
    )
    caption(
        doc,
        "Table 2 — The three roles. Write access to sales, purchasing and inventory endpoints is "
        "restricted to Administrator and Manager; Staff read.",
    )

    h2(doc, "3.2 Branch scoping")
    para(
        doc,
        "A role says what you may do; it does not say which records you may do it to. That is branch "
        "scoping, and it works at the level of the individual record. When a user is assigned to a "
        "branch, their sign-in token carries a branch claim. Warehouse-bound data — stock, movements, "
        "sales orders, purchase orders — belongs to a branch through its warehouse, so a branch-scoped "
        "user sees and acts on only the warehouses of their branch. A user with no branch assigned is "
        "unrestricted, which is the correct default for a head-office administrator. A user whose branch "
        "has no warehouses sees nothing, which is the correct behaviour for a misconfigured account: it "
        "fails closed.",
    )

    h2(doc, "3.3 Segregation of duties")
    para(
        doc,
        "Segregation of duties is the principle that no single person should be able to complete a "
        "transaction chain alone. The fraud it prevents is well known: raise a purchase order to a "
        "supplier you control, approve your own order, record goods that never arrived, approve the "
        "invoice for them, and pay it. Nautilus ERP breaks that chain in five places. Each rule compares "
        "the person attempting the current step against the people who performed the earlier, conflicting "
        "steps on the same document chain.",
    )
    table(
        doc,
        ["Rule", "The person acting may not be…", "What it stops"],
        [
            (
                "PurchaseOrderApproval",
                "the person who raised the purchase order",
                "Approving your own commitment to spend money",
            ),
            (
                "GoodsReceipt",
                "the person who raised or approved the order",
                "Confirming the arrival of goods you ordered and signed off",
            ),
            (
                "SupplierInvoiceApproval",
                "the person who entered the bill, or who received the goods",
                "Certifying a liability you created or vouched for",
            ),
            (
                "SupplierPayment",
                "the person who approved the supplier's bill",
                "Releasing money against a bill you yourself approved",
            ),
            (
                "InvoiceVoid",
                "the person who issued the customer invoice",
                "Quietly cancelling a sale you recorded — a common way to conceal cash theft",
            ),
        ],
        widths=[1.9, 2.6, 2.5],
    )
    caption(doc, "Table 3 — The five segregation-of-duties rules, enforced by default.")
    para(
        doc,
        "A blocked attempt returns a 403 Forbidden response with an explanation, and is written to the "
        "log — a refused attempt is itself information worth keeping. The rules are on by default. A "
        "business too small to staff both sides of a control can switch individual rules off, or all of "
        "them, through configuration, accepting the audit trail as the compensating control. Documents "
        "created with no signed-in user, such as those from the demo data seeder, have no recorded actor "
        "and therefore never trip a rule.",
    )

    # ============================================================ 4
    h1(doc, "4. The document lifecycles")
    para(
        doc,
        "A document in an ERP is not a form; it is a small state machine. Each document may only move "
        "between defined states, and an attempt at an illegal move is refused with a 409 Conflict rather "
        "than being quietly allowed. This is what makes the records trustworthy after the fact.",
    )

    h2(doc, "4.1 Order to cash")
    para(
        doc,
        "A customer places an order. The order is captured as a Draft, which is still editable. Confirming "
        "it commits the business to the sale. Fulfilling it issues the stock — an all-or-nothing operation "
        "that consumes FIFO cost layers and writes the ledger entries; if there is not enough stock, "
        "nothing is posted and the order simply remains Confirmed. An invoice is then raised from the "
        "order, snapshotting the VAT rate in force on that date so an issued invoice never changes "
        "retroactively. Issuing the invoice makes it a committed tax document. Payments received against "
        "it move it to PartiallyPaid and then to Paid. An invoice can be voided only before any payment "
        "has been recorded, and never by the person who issued it.",
    )

    h2(doc, "4.2 Procure to pay")
    para(
        doc,
        "The purchasing chain is the mirror image. A purchase order starts as a Draft, is confirmed and "
        "placed with the supplier, and then receives goods — possibly across several deliveries, moving "
        "it to PartiallyReceived and finally Received. Posting a goods receipt is the point at which "
        "stock actually enters the warehouse and a FIFO cost layer is created at the purchase-order unit "
        "cost. Over-receiving a line is refused and nothing is posted. The supplier's bill is then raised "
        "from the order, capturing input VAT, approved, and paid. Four of the five segregation-of-duties "
        "rules police this chain.",
    )

    table(
        doc,
        ["Document", "States", "Notes on the transitions"],
        [
            (
                "Sales order",
                "Draft → Confirmed → Fulfilled; Cancelled",
                "Fulfilment issues stock via FIFO; all-or-nothing. Cancelled is terminal.",
            ),
            (
                "Customer invoice",
                "Draft → Issued → PartiallyPaid → Paid; Void",
                "Issuing snapshots the VAT rate and calls the fiscalization port. Void only before any payment.",
            ),
            (
                "Purchase order",
                "Draft → Confirmed → PartiallyReceived → Received; Cancelled",
                "Goods receipts drive the received states. Cancellation only before receipt.",
            ),
            (
                "Supplier invoice",
                "Draft → Approved → PartiallyPaid → Paid; Cancelled",
                "Approved is a committed liability. Cancellation only before payment.",
            ),
            (
                "Stock movement",
                "Receipt, Issue, AdjustmentIn, AdjustmentOut, TransferIn, TransferOut",
                "Not a lifecycle but a ledger: entries are immutable and never revised.",
            ),
        ],
        widths=[1.4, 2.4, 3.1],
    )
    caption(doc, "Table 4 — Document state machines. Illegal transitions are rejected with 409 Conflict.")

    # ============================================================ 5
    h1(doc, "5. Fiji localization")
    para(
        doc,
        "The system assumes a Fiji-registered business as its default case rather than treating Fiji as a "
        "configuration afterthought. Three things follow from that.",
    )
    para(
        doc,
        "VAT. Fiji's standard Value Added Tax rate is 15 percent. It is stored as a percentage — the "
        "number 15, meaning 15%, not the fraction 0.15 — and it is effective-dated. A tax carries a "
        "history of rate rows, each with a start date and an optional end date; the rate in force on a "
        "given date is the one whose window contains it. Adding a new rate closes the current open one. "
        "Zero-rated and exempt supplies are modelled as distinct treatments, not merely as a rate of "
        "zero, because the distinction matters for revenue reporting. Every invoice line snapshots the "
        "rate that applied on the day of issue.",
    )
    para(
        doc,
        "The tax invoice. A company profile holds the legal and trading name, the FRCS Taxpayer "
        "Identification Number (TIN), address, contact details and the base currency, which defaults to "
        "FJD. An issued invoice can be rendered as a PDF laid out as a Fiji tax invoice: seller name and "
        "TIN, buyer name and TIN, line items with their snapshotted VAT, totals in Fiji dollars, and the "
        "invoice's fiscalization status printed plainly on the face of the document. Payment methods "
        "include the tender types actually used in Fiji, mobile wallet among them.",
    )

    h2(doc, "5.1 FRCS / VMS fiscalization — the honest status")
    para(
        doc,
        "This system does not submit invoices to the Fiji Revenue and Customs Service, and it does not "
        "pretend to.",
    )
    para(
        doc,
        "Fiscalization — the electronic accreditation of invoices with the revenue authority through "
        "FRCS's TPOS / VAT Monitoring System — is present in the architecture as a port: a named boundary "
        "with a defined contract, where a real adapter will one day be plugged in. What ships today is a "
        "null stub. When an invoice is issued, the stub is called; it writes a log line saying no verified "
        "adapter is configured, and returns the status NotSubmitted. Every issued invoice therefore carries "
        "a fiscal status of NotSubmitted, and that status is shown in the interface and printed on the PDF.",
    )
    para(
        doc,
        "This is a deliberate decision, not an omission awaiting apology. The FRCS specification, including "
        "the accredited invoice-numbering format, has not been verified against an authoritative source. "
        "Writing code that reports invoices as successfully submitted when they were not would produce a "
        "system that lies to its operator about their tax compliance. The boundary is modelled so that "
        "substituting a verified adapter is a single dependency-injection change and requires no rework of "
        "the sales module. Until then, a business using Nautilus ERP must meet its FRCS fiscalization "
        "obligations by other means.",
    )

    # ============================================================ 6
    h1(doc, "6. Security posture")
    para(
        doc,
        "Signing in issues two tokens. A short-lived access token, valid for fifteen minutes, accompanies "
        "each request. A longer-lived refresh token exchanges for a new access token when that expires. "
        "Refresh tokens rotate on every use: the old one is consumed and replaced, and presenting an "
        "already-consumed token is treated as evidence of theft and rejected. Refresh tokens are stored "
        "hashed with SHA-256 rather than in the clear, so a reader of the database cannot impersonate a "
        "user. Passwords must be at least eight characters with upper case, lower case, a digit and a "
        "symbol; five failed attempts lock the account for fifteen minutes; every attempt, successful or "
        "not, is written to a login history. Password reset and forgotten-password responses never reveal "
        "whether an account exists.",
    )
    para(
        doc,
        "Beyond authentication, the API applies rate limiting (tighter on the authentication endpoints than "
        "elsewhere), response compression, baseline security headers, HTTPS redirection outside development, "
        "and a startup guard that refuses to boot outside development if the JWT signing key is missing, "
        "too short, or still the development default. All database access is through Entity Framework with "
        "parameterized queries; there is no string-concatenated SQL. Every insert, update and delete of a "
        "business entity is captured by an audit interceptor and readable, by administrators only, as an "
        "audit trail. Deletion is soft: records are marked deleted, not removed.",
    )

    h2(doc, "6.1 Known limitations and what is not implemented")
    para(
        doc,
        "The following are true of the system as it stands. They are stated plainly because a reader who "
        "discovers them later would be right to distrust everything else in this document.",
    )
    bullets(
        doc,
        [
            "No multi-factor authentication. A password is the only factor.",
            "No email verification of accounts, and no public self-registration — accounts are created by an administrator, which mitigates the first point but does not remove it.",
            "The refresh token is held in the browser's localStorage. This is convenient and survives a page reload, but it is readable by any script that manages to run on the page, so a cross-site scripting flaw would be more damaging than it would be with an HttpOnly cookie.",
            "No general ledger and no double-entry accounting. Nautilus ERP records operational transactions — stock, invoices, payments — not journals, chart of accounts or trial balance. It is not a substitute for accounting software.",
            "FRCS / VMS fiscalization is not integrated. Every issued invoice remains NotSubmitted. See section 5.1.",
            "Real-time notifications sent through SignalR are broadcast to every connected staff member. Per-user targeting exists in the code, but the business events that fire notifications use the broadcast path, so a notification about one branch's invoice reaches everyone signed in.",
            "The email sender is a logging stub. Emails are queued for background delivery and then written to the log rather than sent; a real SMTP sender must be supplied.",
            "The Hangfire background job store is in-memory, so queued work does not survive a restart of the API.",
            "Multi-currency is modelled — FJD is the base currency and currencies are reference data — but there is no exchange-rate table in use and no bank reconciliation, RTGS/ACH or mobile-wallet payment integration. Mobile wallet is a payment method you may record, not a payment rail the system connects to.",
            "There is no offline mode. GUID primary keys keep that option open architecturally; nothing implements it.",
        ],
    )

    # ============================================================ 7
    h1(doc, "7. Architecture and technology")
    para(
        doc,
        "For readers who care how it is built: Nautilus ERP is a .NET 9 solution laid out as Clean "
        "Architecture, a structure in which dependencies point inward toward the business rules. The "
        "Domain project — the entities and the rules that govern them — knows nothing of databases, web "
        "frameworks or user interfaces, and a test in the suite fails the build if anyone makes it "
        "otherwise. Around it sits an Application layer holding the use cases, then Infrastructure and "
        "Persistence for the outside world, then a thin web API whose controllers only translate HTTP "
        "into application requests and back.",
    )
    para(
        doc,
        "Application logic follows CQRS — Command Query Responsibility Segregation, meaning operations that "
        "change data and operations that read it are separate objects — dispatched through the MediatR "
        "library, with FluentValidation running as a pipeline step before any handler executes. Expected "
        "failures are not exceptions. A handler returns a Result carrying either a value or an Error with a "
        "code, and the API maps those codes to HTTP status: validation to 400, unauthorized to 401, not "
        "found to 404, forbidden to 403, conflict to 409, locked-out account to 423. Exceptions are reserved "
        "for the genuinely unexpected.",
    )
    para(
        doc,
        "Persistence is Entity Framework Core against SQL Server, with SQLite used for zero-infrastructure "
        "local development. Domain entities carry no persistence attributes; mapping lives in configuration "
        "classes. Every entity has a GUID primary key, created and modified stamps, a soft-delete marker "
        "applied through a global query filter, and a concurrency token. The browser client is a React and "
        "TypeScript single-page application built with Vite, using TanStack Query for server state and a "
        "Tailwind-based design system. Reports are exported through a provider-agnostic exporter: CSV "
        "natively, Excel via ClosedXML, PDF via QuestPDF.",
    )

    # ============================================================ 8
    h1(doc, "8. Running the system")
    para(
        doc,
        "Local development needs the .NET 9 SDK and Node 20 or later. Docker is optional — it is required "
        "only if you want SQL Server and Redis, or containerised deployment. On Windows, the run-dev.bat "
        "script at the repository root starts the API and the client in their own terminals and opens a "
        "browser. Run manually, the API starts with `dotnet run --project src/ERP.API` and listens on "
        "http://localhost:5126, creating a SQLite database from the model on first run; the client starts "
        "with `npm install` then `npm run dev` in the client directory and serves http://localhost:5173, "
        "proxying API calls so that no CORS or development-certificate configuration is needed.",
    )
    para(
        doc,
        "In the Development environment a demo data seeder creates three accounts. These credentials are "
        "seeded only in development and must never exist in a deployed system.",
    )
    table(
        doc,
        ["Email", "Password", "Role"],
        [
            ("admin@erp.local", "Admin#12345", "Administrator"),
            ("manager@erp.local", "Demo#12345", "Manager"),
            ("staff@erp.local", "Demo#12345", "Staff"),
        ],
        widths=[2.4, 2.0, 2.0],
    )
    caption(
        doc,
        "Table 5 — Development-only demo sign-ins. Walk the purchasing flow with two different accounts: "
        "segregation of duties will refuse an approval by the same person who raised the order.",
    )
    para(
        doc,
        "For a containerised deployment, a Dockerfile at the root builds the API and one in the client "
        "directory builds the SPA behind nginx. Secrets are supplied as environment variables and never "
        "committed: the JWT signing key, the SQL connection string, the optional bootstrap administrator, "
        "and the allowed browser origins. Interactive API documentation is available at /swagger in "
        "development, and a health endpoint at /health reports live database connectivity.",
    )

    # ============================================================ 9
    h1(doc, "9. Roadmap")
    para(
        doc,
        "The following would need to be true before this system carried a real business's records. They "
        "are listed in the order a cautious operator would tackle them.",
    )
    bullets(
        doc,
        [
            "Verify the FRCS TPOS / VMS specification and implement a real IFiscalizationService adapter, including accredited invoice numbering. Until this exists, invoices remain NotSubmitted.",
            "Move the refresh token out of localStorage and into an HttpOnly, Secure, SameSite cookie, and add multi-factor authentication for administrators at minimum.",
            "Add email verification and a real SMTP sender, and move Hangfire from in-memory storage to a durable store so queued work survives a restart.",
            "Target real-time notifications at the users who should receive them, rather than broadcasting every event to every signed-in staff member.",
            "Decide the accounting boundary: either add a general ledger with double-entry posting from the operational documents, or define and build a clean export to the accounting package the business already uses.",
            "Implement effective-dated exchange rates and, if the business needs them, bank reconciliation against Fiji bank statement formats and the RBF payment rails.",
            "Production operations: SQL Server with a tested backup and restore, secrets in a managed vault rather than environment variables, centralised log shipping, and a load test of the reporting queries against a realistic data volume.",
        ],
    )
    para(
        doc,
        "Nothing in this section describes work that has been started. It describes work that has not.",
        italic=True,
    )

    doc.add_paragraph()
    para(
        doc,
        "Document generated from source by docs/build_report.py on 9 July 2026.",
        size=9,
        color=MUTED,
        italic=True,
        align=WD_ALIGN_PARAGRAPH.CENTER,
    )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUT)
    return OUT


if __name__ == "__main__":
    path = build()
    print(f"Wrote {path} ({path.stat().st_size:,} bytes)")
