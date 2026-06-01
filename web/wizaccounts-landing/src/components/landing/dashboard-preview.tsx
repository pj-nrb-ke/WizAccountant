import { MotionSection } from "@/components/landing/motion-section";
import { DashboardMockup } from "@/components/landing/dashboard-mockup";
import { Badge } from "@/components/ui/badge";

export function DashboardPreview() {
  return (
    <MotionSection className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 text-center md:px-6">
        <Badge variant="glow" className="mb-4">
          Platform preview
        </Badge>
        <h2 className="text-3xl font-bold tracking-tight md:text-4xl">
          One command center for the modern CFO
        </h2>
        <p className="mx-auto mt-4 max-w-2xl text-lg text-muted-foreground">
          Treasury, reconciliation, VAT analytics, and AI investigations — composed
          as a premium fintech experience, not a dense ERP grid.
        </p>
        <div className="mt-12">
          <DashboardMockup className="mx-auto max-w-4xl" />
        </div>
      </div>
    </MotionSection>
  );
}
