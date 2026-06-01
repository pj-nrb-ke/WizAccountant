import {
  Brain,
  Cloud,
  LineChart,
  Network,
  Scale,
  ShieldCheck,
  Smartphone,
  Sparkles,
  Wallet,
} from "lucide-react";
import { MotionSection } from "@/components/landing/motion-section";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  coreCapabilities,
  financeIntelligenceBullets,
  reconciliationItems,
  processIntelligenceItems,
} from "@/content/landing";
import { cn } from "@/lib/utils";

const capabilityIcons = [
  Sparkles,
  Cloud,
  Scale,
  LineChart,
  ShieldCheck,
  Smartphone,
];

function SectionHeader({
  eyebrow,
  title,
  description,
  align = "center",
}: {
  eyebrow: string;
  title: string;
  description: string;
  align?: "center" | "left";
}) {
  return (
    <div
      className={cn(
        "mx-auto max-w-2xl",
        align === "center" && "text-center"
      )}
    >
      <Badge variant="accent" className="mb-4">
        {eyebrow}
      </Badge>
      <h2 className="text-3xl font-bold tracking-tight text-foreground md:text-4xl">
        {title}
      </h2>
      <p className="mt-4 text-lg leading-relaxed text-muted-foreground">
        {description}
      </p>
    </div>
  );
}

export function CoreAiCapabilities() {
  return (
    <MotionSection id="capabilities" className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <SectionHeader
          eyebrow="Core capabilities"
          title="Intent-first finance intelligence on live Sage data"
          description="Insight routes questions through allowlisted operations — count and how many return one number, top-N returns ranked rows, reconciliation runs SQL both sides first. No raw SDK or SQL from chat."
        />
        <div className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {coreCapabilities.map((item, i) => {
            const Icon = capabilityIcons[i] ?? Brain;
            return (
              <Card
                key={item.title}
                className="group transition-all hover:border-primary/40 hover:shadow-[0_0_40px_-12px_var(--glow)]"
              >
                <CardHeader>
                  <div className="mb-2 flex h-10 w-10 items-center justify-center rounded-xl bg-primary/15 text-primary">
                    <Icon className="h-5 w-5" />
                  </div>
                  <CardTitle className="text-base leading-snug">
                    {item.title}
                  </CardTitle>
                  <CardDescription>{item.description}</CardDescription>
                </CardHeader>
              </Card>
            );
          })}
        </div>
      </div>
    </MotionSection>
  );
}

export function FinanceIntelligence() {
  return (
    <MotionSection
      id="intelligence"
      className="border-y border-border bg-surface/20 py-20 md:py-28"
    >
      <div className="mx-auto grid max-w-6xl items-center gap-12 px-4 md:grid-cols-2 md:px-6">
        <div>
          <SectionHeader
            eyebrow="Finance intelligence"
            title="Phase 2 read product — dashboards your team uses daily"
            description="Trial-balance snapshots, debtors and creditors totals, AR/AP browse and drill-down, global search, and exports — all from live connector reads with job audit."
            align="left"
          />
          <ul className="mt-8 space-y-4 text-muted-foreground">
            {financeIntelligenceBullets.map((line) => (
              <li key={line} className="flex gap-3 text-sm leading-relaxed md:text-base">
                <LineChart className="mt-0.5 h-4 w-4 shrink-0 text-cyan-500 dark:text-cyan-400" />
                {line}
              </li>
            ))}
          </ul>
        </div>
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="border-b border-border bg-primary/5 px-6 py-4">
              <p className="text-xs text-muted-foreground">
                Insight · customer.aged.top
              </p>
              <p className="mt-1 font-medium text-foreground">
                Top 5 customers by oldest aged debit balance — ranked by invoice
                date, not full customer master export.
              </p>
            </div>
            <div className="grid gap-px bg-border sm:grid-cols-3">
              {[
                { label: "Debtors", value: "$4.8M" },
                { label: "Creditors", value: "$2.1M" },
                { label: "TB balance", value: "✓" },
              ].map((kpi) => (
                <div key={kpi.label} className="bg-surface/60 px-4 py-5">
                  <p className="text-xs text-muted-foreground">{kpi.label}</p>
                  <p className="mt-1 text-xl font-semibold">{kpi.value}</p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </MotionSection>
  );
}

export function ReconciliationIntelligence() {
  return (
    <MotionSection id="reconciliation" className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <SectionHeader
          eyebrow="Reconciliation intelligence"
          title="Totals-first match intelligence across GL and subledgers"
          description="Dedicated SQL handlers for inventory vs GL, AR aging, VAT bridges, and open items — with mega-digest fallback when your wording matches the 500-query catalog."
        />
        <div className="mt-14 grid gap-6 md:grid-cols-3">
          {reconciliationItems.map((item, i) => (
            <Card
              key={item.title}
              className={cn(i === 1 && "md:-translate-y-2 md:shadow-xl")}
            >
              <CardHeader>
                <Scale className="mb-2 h-5 w-5 text-primary" />
                <CardTitle className="text-base">{item.title}</CardTitle>
                <CardDescription>{item.body}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </div>
      </div>
    </MotionSection>
  );
}

export function TreasuryIntelligence() {
  return (
    <MotionSection
      id="treasury"
      className="border-y border-border bg-gradient-to-b from-surface/30 to-background py-20 md:py-28"
    >
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="grid items-center gap-12 lg:grid-cols-2">
          <Card className="order-2 border-cyan-500/20 lg:order-1">
            <CardHeader>
              <Wallet className="h-5 w-5 text-cyan-500 dark:text-cyan-400" />
              <CardTitle className="mt-2">Liquidity horizon</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {[
                { label: "Projected cash (30d)", value: "$2.6M", trend: "+2.1%" },
                { label: "Inflow confidence", value: "87%", trend: "stable" },
                { label: "Covenant headroom", value: "$420K", trend: "watch" },
              ].map((row) => (
                <div
                  key={row.label}
                  className="flex items-center justify-between rounded-xl border border-border bg-background/50 px-4 py-3"
                >
                  <span className="text-sm text-muted-foreground">{row.label}</span>
                  <div className="text-right">
                    <p className="font-semibold">{row.value}</p>
                    <p className="text-xs text-muted-foreground">{row.trend}</p>
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
          <div className="order-1 lg:order-2">
            <SectionHeader
              eyebrow="Treasury intelligence"
              title="Working capital and cash trajectory in one view"
              description="Combine GL cash positions, AR/AP behaviour, and forward signals — so treasury and the CFO office act before liquidity tightens."
              align="left"
            />
          </div>
        </div>
      </div>
    </MotionSection>
  );
}

export function BusinessProcessIntelligence() {
  return (
    <MotionSection className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="grid gap-12 lg:grid-cols-2 lg:items-center">
          <SectionHeader
            eyebrow="Sage domain intelligence"
            title="AR, AP, inventory, and GL — trained for business meaning"
            description="Domain routing covers customers, suppliers, stock valuation, journals, and manufacturing contexts — aligned with the Sage AI agent training framework."
            align="left"
          />
          <div className="space-y-3">
            {processIntelligenceItems.map((text) => (
              <div
                key={text}
                className="flex items-center gap-4 rounded-2xl border border-border bg-surface/40 px-5 py-4 transition-colors hover:border-primary/30"
              >
                <Network className="h-5 w-5 shrink-0 text-primary" />
                <p className="text-sm font-medium text-foreground md:text-base">
                  {text}
                </p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </MotionSection>
  );
}
