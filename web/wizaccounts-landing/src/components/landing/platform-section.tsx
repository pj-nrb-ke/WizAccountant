import { MotionSection } from "@/components/landing/motion-section";
import { Badge } from "@/components/ui/badge";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { platformModules } from "@/content/landing";

export function PlatformSection() {
  return (
    <MotionSection
      id="platform"
      className="border-y border-border bg-surface/20 py-20 md:py-28"
    >
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="mx-auto max-w-2xl text-center">
          <Badge variant="accent" className="mb-4">
            Platform architecture
          </Badge>
          <h2 className="text-3xl font-bold tracking-tight md:text-4xl">
            On-prem connector · cloud control plane
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            WizConnector keeps Sage 200 Evolution as system of record. WizAccounts
            cloud delivers Insight, Act approvals, admin, and mobile — over outbound
            HTTPS with allowlisted jobs only.
          </p>
        </div>
        <div className="mt-14 grid gap-5 sm:grid-cols-2">
          {platformModules.map((mod) => (
            <Card key={mod.title}>
              <CardHeader>
                <span className="text-[10px] font-semibold uppercase tracking-widest text-primary">
                  {mod.phase}
                </span>
                <CardTitle className="mt-1">{mod.title}</CardTitle>
                <ul className="mt-3 list-none space-y-2">
                  {mod.points.map((point) => (
                    <li
                      key={point}
                      className="flex gap-2 text-sm leading-relaxed text-muted-foreground"
                    >
                      <span className="text-primary">·</span>
                      {point}
                    </li>
                  ))}
                </ul>
              </CardHeader>
            </Card>
          ))}
        </div>
      </div>
    </MotionSection>
  );
}
