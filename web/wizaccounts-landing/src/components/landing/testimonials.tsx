import { MotionSection } from "@/components/landing/motion-section";
import { Card, CardContent } from "@/components/ui/card";

const quotes = [
  {
    quote:
      "We stopped exporting grids to Excel. Reconciliation narratives land on my desk before the team finishes coffee.",
    role: "Group Financial Director",
    org: "Multi-site distribution",
  },
  {
    quote:
      "Treasury finally sees the same truth as the GL — with explainability our auditors actually appreciate.",
    role: "CFO",
    org: "Manufacturing group",
  },
  {
    quote:
      "It feels like fintech, not Sage desktop. Our board asks for WizAccounts slides now.",
    role: "Finance Director",
    org: "Professional services",
  },
];

export function Testimonials() {
  return (
    <MotionSection className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <h2 className="text-center text-3xl font-bold tracking-tight md:text-4xl">
          What finance leaders are saying
        </h2>
        <div className="mt-14 grid gap-6 md:grid-cols-3">
          {quotes.map((t) => (
            <Card key={t.role} className="flex flex-col">
              <CardContent className="flex flex-1 flex-col pt-6">
                <p className="flex-1 text-sm leading-relaxed text-foreground/90 md:text-base">
                  &ldquo;{t.quote}&rdquo;
                </p>
                <div className="mt-6 border-t border-border pt-4">
                  <p className="text-sm font-medium">{t.role}</p>
                  <p className="text-xs text-muted-foreground">{t.org}</p>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </MotionSection>
  );
}
