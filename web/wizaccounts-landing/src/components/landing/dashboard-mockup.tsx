"use client";

import { motion } from "framer-motion";
import {
  ArrowDownRight,
  ArrowUpRight,
  Brain,
  LineChart,
  Scale,
  Sparkles,
  Wallet,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

function MiniBar({ heights, className }: { heights: number[]; className?: string }) {
  return (
    <div className={cn("flex h-16 items-end gap-1", className)}>
      {heights.map((h, i) => (
        <div
          key={i}
          className="w-2 rounded-sm bg-gradient-to-t from-primary/30 to-cyan-400/80"
          style={{ height: `${h}%` }}
        />
      ))}
    </div>
  );
}

export function DashboardMockup({ className }: { className?: string }) {
  return (
    <motion.div
      className={cn(
        "relative overflow-hidden rounded-2xl border border-border bg-surface/80 p-4 shadow-xl ring-1 ring-black/5 backdrop-blur-xl dark:shadow-2xl dark:shadow-black/40 dark:ring-white/5 md:p-5",
        className
      )}
      initial={{ opacity: 0, y: 32, rotateX: 4 }}
      animate={{ opacity: 1, y: 0, rotateX: 0 }}
      transition={{ duration: 0.8, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
    >
      <div className="pointer-events-none absolute -right-20 -top-20 h-56 w-56 rounded-full bg-primary/20 blur-3xl" />
      <div className="pointer-events-none absolute -bottom-16 -left-16 h-48 w-48 rounded-full bg-cyan-500/15 blur-3xl" />

      <div className="relative mb-4 flex items-center justify-between gap-3 border-b border-border pb-3">
        <div className="flex items-center gap-2">
          <div className="flex gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-red-500/80" />
            <span className="h-2.5 w-2.5 rounded-full bg-amber-400/80" />
            <span className="h-2.5 w-2.5 rounded-full bg-emerald-400/80" />
          </div>
          <span className="text-xs font-medium text-muted-foreground">
            Finance Command Center
          </span>
        </div>
        <Badge variant="glow" className="gap-1">
          <Sparkles className="h-3 w-3" />
          AI Live
        </Badge>
      </div>

      <div className="relative grid gap-3 md:grid-cols-12">
        <div className="rounded-xl border border-border bg-background/60 p-3 md:col-span-4">
          <div className="mb-2 flex items-center gap-2 text-xs text-muted-foreground">
            <Wallet className="h-3.5 w-3.5 text-cyan-600 dark:text-cyan-400" />
            Treasury forecast
          </div>
          <p className="text-2xl font-semibold tracking-tight text-foreground">
            $3.8M
          </p>
          <p className="mt-1 flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-400">
            <ArrowUpRight className="h-3 w-3" />
            +8.4% projected liquidity
          </p>
          <MiniBar heights={[40, 55, 48, 72, 65, 80, 74]} className="mt-3" />
        </div>

        <div className="rounded-xl border border-border bg-background/60 p-3 md:col-span-4">
          <div className="mb-2 flex items-center gap-2 text-xs text-muted-foreground">
            <Scale className="h-3.5 w-3.5 text-primary" />
            Reconciliation
          </div>
          <p className="text-lg font-semibold text-foreground">3 variances</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Inventory vs GL · AR vs control · VAT bridge
          </p>
          <div className="mt-3 space-y-2">
            {[
              { label: "Inventory / GL", pct: 98 },
              { label: "AR / GL", pct: 100 },
              { label: "VAT control", pct: 94 },
            ].map((row) => (
              <div key={row.label}>
                <div className="mb-1 flex justify-between text-[10px] text-muted-foreground">
                  <span>{row.label}</span>
                  <span>{row.pct}%</span>
                </div>
                <div className="h-1.5 overflow-hidden rounded-full bg-border">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-primary to-cyan-400"
                    style={{ width: `${row.pct}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-xl border border-primary/25 bg-primary/5 p-3 md:col-span-4">
          <div className="mb-2 flex items-center gap-2 text-xs font-medium text-blue-800 dark:text-blue-100">
            <Brain className="h-3.5 w-3.5" />
            Explainability
          </div>
          <p className="text-sm font-medium leading-snug text-foreground">
            VAT liability up 12% — driven by Q2 import mix and delayed input claims.
          </p>
          <p className="mt-2 text-[10px] text-muted-foreground">
            Confidence 94% · Sources: PostGL, VAT201, supplier ledger
          </p>
        </div>

        <div className="rounded-xl border border-border bg-background/60 p-3 md:col-span-7">
          <div className="mb-2 flex items-center justify-between">
            <span className="flex items-center gap-2 text-xs text-muted-foreground">
              <LineChart className="h-3.5 w-3.5" />
              Cashflow trajectory
            </span>
            <span className="text-[10px] text-muted-foreground">90-day view</span>
          </div>
          <div className="relative h-24">
            <svg
              className="h-full w-full"
              viewBox="0 0 280 80"
              preserveAspectRatio="none"
              aria-hidden
            >
              <defs>
                <linearGradient id="cashGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="rgb(59,130,246)" stopOpacity="0.35" />
                  <stop offset="100%" stopColor="rgb(59,130,246)" stopOpacity="0" />
                </linearGradient>
              </defs>
              <path
                d="M0,55 C40,48 80,62 120,40 S200,20 280,35 L280,80 L0,80 Z"
                fill="url(#cashGrad)"
              />
              <path
                d="M0,55 C40,48 80,62 120,40 S200,20 280,35"
                fill="none"
                stroke="rgb(34,211,238)"
                strokeWidth="2"
              />
            </svg>
          </div>
        </div>

        <div className="rounded-xl border border-border bg-background/60 p-3 md:col-span-5">
          <p className="text-xs text-muted-foreground">Investigation queue</p>
          <ul className="mt-2 space-y-2">
            {[
              "GP decline — margin compression in Region North",
              "Customer payment drift — top 5 accounts",
              "Supplier risk — extended terms cluster",
            ].map((item) => (
              <li
                key={item}
                className="flex items-start gap-2 rounded-lg border border-border/80 bg-surface/50 px-2 py-1.5 text-[11px] text-foreground/90"
              >
                <Sparkles className="mt-0.5 h-3 w-3 shrink-0 text-cyan-600 dark:text-cyan-400" />
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-3 border-t border-border pt-3 text-[10px] text-muted-foreground">
        <span className="flex items-center gap-1">
          <ArrowDownRight className="h-3 w-3 text-amber-600 dark:text-amber-400" />
          Outflows monitored
        </span>
        <span>·</span>
        <span>Finance-grade audit trail</span>
        <span>·</span>
        <span>Explainable AI outputs</span>
      </div>
    </motion.div>
  );
}
