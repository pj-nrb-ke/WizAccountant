import { MotionSection } from "@/components/landing/motion-section";
import { Badge } from "@/components/ui/badge";
import { enterpriseFeatures } from "@/content/landing";
import { Lock, FileCheck, Eye, Building2 } from "lucide-react";

const icons = [Eye, FileCheck, Lock, Building2];

export function FeatureHighlights() {
  return (
    <MotionSection className="border-y border-border bg-surface/20 py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="text-center">
          <Badge variant="accent" className="mb-4">
            Enterprise trust
          </Badge>
          <h2 className="text-3xl font-bold tracking-tight md:text-4xl">
            Guardrails, audit, and governance by design
          </h2>
          <p className="mx-auto mt-4 max-w-2xl text-muted-foreground">
            Phase 2 read-only AI with allowlisted tools. Phase 3 writes only after
            human approval — with idempotency and full audit export.
          </p>
        </div>
        <div className="mt-12 flex flex-wrap justify-center gap-3">
          {[
            "Allowlisted ops",
            "Explainable AI",
            "Maker-checker",
            "Write audit",
          ].map((tag) => (
            <span
              key={tag}
              className="rounded-full border border-border bg-background/60 px-4 py-1.5 text-xs font-medium text-muted-foreground"
            >
              {tag}
            </span>
          ))}
        </div>
        <div className="mt-14 grid gap-6 sm:grid-cols-2">
          {enterpriseFeatures.map((f, i) => {
            const Icon = icons[i] ?? Eye;
            return (
              <div
                key={f.title}
                className="rounded-2xl border border-border bg-background/40 p-6 transition-all hover:border-primary/30"
              >
                <Icon className="h-5 w-5 text-primary" />
                <h3 className="mt-4 font-semibold text-foreground">{f.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                  {f.description}
                </p>
              </div>
            );
          })}
        </div>
      </div>
    </MotionSection>
  );
}
