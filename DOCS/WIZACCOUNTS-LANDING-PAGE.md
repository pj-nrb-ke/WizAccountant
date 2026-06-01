# WizAccounts Landing Page — Design & Implementation

Branch: `landing-page`  
App path: `web/wizaccounts-landing`

## Section hierarchy (Phase 1)

```text
Navbar (fixed)
  ↓
Hero — headline, executive copy, CTAs, live dashboard mockup
  ↓
Trust bar — credibility signals
  ↓
Core AI capabilities — 4 capability cards
  ↓
Finance intelligence — narrative + insight card
  ↓
Reconciliation intelligence — Inventory/GL, AR/GL, VAT
  ↓
Treasury intelligence — liquidity KPIs
  ↓
Business process intelligence — AR/AP/inventory signals
  ↓
Dashboard preview — full-width mockup
  ↓
Enterprise trust — security, explainability, governance
  ↓
Testimonials
  ↓
Pricing preview (3 tiers)
  ↓
Final CTA band
  ↓
Footer
```

## UX narrative

Scroll progression alternates **copy-left / visual-right** and centered section headers to maintain “fintech SaaS” curiosity — not ERP density.

## References applied

- Launch UI — spacing, gradient hero glow, card elevation
- Cruip / Page UI patterns — section rhythm, CTA placement, trust band
- Stripe / Linear / Vercel tone — dark charcoal, restrained motion, executive copy

## Known polish areas

- Replace placeholder trust logos with real customer marks (when approved)
- Wire CTAs to demo booking / CRM URLs
- Add `next/image` OG asset and favicon brand mark
- Optional: retry `npx shadcn add` for additional registry components when network stable
- Performance: lazy-load below-fold motion if needed on low-end mobile
