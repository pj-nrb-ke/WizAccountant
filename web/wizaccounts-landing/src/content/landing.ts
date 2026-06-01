/**
 * Marketing copy aligned with DOCS/ (Plan1, INSIGHT-CHAT-INTENTS, PHASE2/3, MOBILE-APP, Sage AI training).
 */

export const trustSignals = [
  "Sage 200 Evolution",
  "500-query intelligence catalog",
  "On-prem WizConnector",
  "Allowlisted AI operations",
  "Maker-checker approvals",
  "iOS & Android apps",
];

export const coreCapabilities = [
  {
    title: "Insight AI — intent-first routing",
    description:
      "Natural-language questions map to allowlisted Sage operations via intent classification, business-process routing, and a 500-title mega digest — aggregation returns one number, ranking returns top N rows, never full dumps.",
  },
  {
    title: "Live Sage 200 Evolution data",
    description:
      "On-prem WizConnector pairs over outbound HTTPS/WebSocket — no inbound firewall ports. Reads GL, AR, AP, inventory, and orders with site heartbeat and data-as-of timestamps.",
  },
  {
    title: "Reconciliation & investigation SQL",
    description:
      "Inventory vs GL, AR aged top-N, unpaid invoice counts, negative stock on balance sheet (PostGL credit balances) — business meaning before table meaning.",
  },
  {
    title: "Dashboards & AR/AP workspaces",
    description:
      "Trial-balance snapshots, debtors and creditors KPIs, customer and supplier drill-down, global search, and CSV/PDF export for data users already authorised to see.",
  },
  {
    title: "Controlled writes & approvals",
    description:
      "Phase 3 maker-checker: AI proposes structured drafts; humans approve GL journals, AR/AP posts, and allocations with idempotency keys and full write audit trails.",
  },
  {
    title: "Mobile finance operations",
    description:
      "Same cloud API on iOS and Android — site status, dashboard KPIs, read-only AI chat, and an approval inbox with push notifications for approvers on the go.",
  },
];

export const financeIntelligenceBullets = [
  "Debtors, creditors, and trial-balance KPIs from live connector reads",
  "Cross-entity search across customers, suppliers, accounts, and references",
  "Insight replies cite query run, counts, and data-as-of job completion time",
];

export const reconciliationItems = [
  {
    title: "Inventory vs GL",
    body: "Detect balance-sheet stock ledgers with credit balances (PostGL) — not confused with negative quantity on hand. Align valuation before close.",
  },
  {
    title: "AR aging & open items",
    body: "Top-N oldest aged debtors, outstanding customer transactions, and unpaid sales invoice counts — ranked and aggregated, not thousand-row grids.",
  },
  {
    title: "VAT & control accounts",
    body: "Bridge subledgers to control accounts with totals-first validation and explainable variance narratives for controllers and auditors.",
  },
];

export const processIntelligenceItems = [
  "AR: aging, credit limits, collections, and outstanding invoice intelligence",
  "AP: supplier payments, overdue invoices, and allocation reads",
  "Inventory: valuation, warehouses, slow-moving and reorder signals",
];

export const platformModules = [
  {
    phase: "Connect",
    title: "WizConnector on-prem",
    points: [
      "Windows service + tray pairing wizard",
      "Outbound TLS — site online within 60s of start",
      "Allowlisted read handlers only in early phases",
    ],
  },
  {
    phase: "Insight",
    title: "AI Assistant (read-first)",
    points: [
      "Tool allowlist = read handlers; never claims to post",
      "Mega digest fallback for 500 catalog titles",
      "Conversation history per tenant with PII-safe logging policy",
    ],
  },
  {
    phase: "Act",
    title: "Approvals & writes",
    points: [
      "Preparer proposes → approver executes to Sage",
      "Roles: preparer, approver, admin",
      "Duplicate idempotency key prevents double-post",
    ],
  },
  {
    phase: "Mobile",
    title: "Pocket CFO",
    points: [
      "Biometric unlock and secure token storage",
      "Approve journals and payments from push deep links",
      "Practice mode for multi-site firms (roadmap)",
    ],
  },
];

export const enterpriseFeatures = [
  {
    title: "AI guardrails",
    description:
      "Only operations in the Insight allowlist may run from chat. Write requests route to Phase 3 approval — no raw SQL or SDK from the UI.",
  },
  {
    title: "Write audit & governance",
    description:
      "Before/after payloads, approver identity, and Evolution references stored per post. SDK posts are not auto-reversed — clear rollback messaging.",
  },
  {
    title: "Site & tenant security",
    description:
      "Encrypted connector credentials (DPAPI on-prem), job audit per tenant/site, and optional tray consent before cloud posts.",
  },
  {
    title: "Enterprise roadmap",
    description:
      "Practice multi-site mode, SSO (Azure AD / Google), RBAC, site SLA monitoring, and advanced multi-step AI workflows.",
  },
];

export const pricingTiers = [
  {
    name: "Growth",
    priceUsd: 299,
    period: "month",
    description: "Finance teams on Sage 200 Evolution — read product + Insight AI.",
    features: [
      "1 connected site",
      "Insight AI (read-only)",
      "Dashboard & AR/AP workspaces",
      "Core reconciliation handlers",
      "Email alerts (site offline)",
    ],
    highlighted: false,
  },
  {
    name: "Enterprise",
    priceUsd: 899,
    period: "month",
    description: "Groups needing approvals, mobile, and expanded intelligence modules.",
    features: [
      "Up to 5 sites",
      "Everything in Growth",
      "Act approvals & controlled writes",
      "iOS & Android apps",
      "Priority onboarding",
    ],
    highlighted: true,
  },
  {
    name: "Strategic",
    priceUsd: 2499,
    period: "month",
    description: "CFO offices and practices driving multi-entity finance transformation.",
    features: [
      "Unlimited sites (fair use)",
      "Dedicated customer success",
      "Practice / multi-client mode",
      "Custom integrations & SLA",
      "SSO & compliance add-ons",
    ],
    highlighted: false,
  },
];

export function formatUsd(amount: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(amount);
}
