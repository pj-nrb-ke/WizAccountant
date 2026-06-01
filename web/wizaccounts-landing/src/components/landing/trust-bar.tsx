import { MotionSection } from "@/components/landing/motion-section";
import { trustSignals } from "@/content/landing";

export function TrustBar() {
  return (
    <MotionSection className="border-y border-border bg-surface/30 py-10">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <p className="mb-6 text-center text-xs font-medium uppercase tracking-widest text-muted-foreground">
          Platform capabilities from production architecture
        </p>
        <div className="flex flex-wrap items-center justify-center gap-x-8 gap-y-3">
          {trustSignals.map((name) => (
            <span
              key={name}
              className="text-sm font-medium text-foreground/55 transition-colors hover:text-foreground/85"
            >
              {name}
            </span>
          ))}
        </div>
      </div>
    </MotionSection>
  );
}
