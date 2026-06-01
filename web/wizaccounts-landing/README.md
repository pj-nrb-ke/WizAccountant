# WizAccounts Landing Page

Premium marketing site for **WizAccounts** — AI-powered finance intelligence (Next.js, Tailwind CSS, Framer Motion, Lucide).

## Quick start

```bash
cd web/wizaccounts-landing
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Page architecture (scroll flow)

| # | Section | Component |
|---|---------|-----------|
| 1 | Navbar | `navbar.tsx` |
| 2 | Hero + dashboard preview | `hero.tsx`, `dashboard-mockup.tsx` |
| 3 | Trust / credibility | `trust-bar.tsx` |
| 4 | Core AI capabilities | `intelligence-sections.tsx` → `CoreAiCapabilities` |
| 5 | Finance intelligence | `FinanceIntelligence` |
| 6 | Reconciliation intelligence | `ReconciliationIntelligence` |
| 7 | Treasury forecasting | `TreasuryIntelligence` |
| 8 | Business process intelligence | `BusinessProcessIntelligence` |
| 9 | Dashboard preview (full width) | `dashboard-preview.tsx` |
| 10 | Enterprise trust / features | `feature-highlights.tsx` |
| 11 | Testimonials | `testimonials.tsx` |
| 12 | Pricing preview | `pricing-preview.tsx` |
| 13 | Final CTA | `cta-section.tsx` |
| 14 | Footer | `footer.tsx` |

## Design system

- **Mode:** Dark-first (`html.dark`)
- **Palette:** Charcoal background (`#070B12`), graphite surfaces, electric blue + cyan accents
- **Typography:** Geist Sans — large headings, generous line-height
- **Motion:** Framer Motion fade-up on scroll (`motion-section.tsx`), hero entrance animations

## Build

```bash
npm run build
npm start
```

Static export can be added later for VPS `file_server` deployment (see `config/secrets/website-hosting-notes.md`).
