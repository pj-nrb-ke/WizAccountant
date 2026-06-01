import { Check } from "lucide-react";
import { MotionSection } from "@/components/landing/motion-section";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { formatUsd, pricingTiers } from "@/content/landing";

export function PricingPreview() {
  return (
    <MotionSection id="pricing" className="border-t border-border py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="text-center">
          <Badge variant="accent" className="mb-4">
            Pricing · USD
          </Badge>
          <h2 className="text-3xl font-bold tracking-tight md:text-4xl">
            Plans that scale with your finance function
          </h2>
          <p className="mx-auto mt-4 max-w-xl text-muted-foreground">
            All prices shown in US dollars per month. Enterprise volume and
            practice licensing available on request.
          </p>
        </div>
        <div className="mt-14 grid gap-6 lg:grid-cols-3">
          {pricingTiers.map((tier) => (
            <Card
              key={tier.name}
              className={
                tier.highlighted
                  ? "relative border-primary/50 bg-gradient-to-b from-primary/10 to-surface/40 shadow-[0_0_48px_-16px_var(--glow)]"
                  : ""
              }
            >
              {tier.highlighted && (
                <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-primary px-3 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-primary-foreground">
                  Most popular
                </span>
              )}
              <CardHeader>
                <CardTitle>{tier.name}</CardTitle>
                <p className="flex items-baseline gap-1">
                  <span className="text-3xl font-bold tracking-tight">
                    {formatUsd(tier.priceUsd)}
                  </span>
                  <span className="text-sm text-muted-foreground">
                    / {tier.period}
                  </span>
                </p>
                <p className="text-sm text-muted-foreground">{tier.description}</p>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3">
                  {tier.features.map((f) => (
                    <li key={f} className="flex gap-2 text-sm text-muted-foreground">
                      <Check className="h-4 w-4 shrink-0 text-cyan-500 dark:text-cyan-400" />
                      {f}
                    </li>
                  ))}
                </ul>
                <Button
                  className="mt-8 w-full"
                  variant={tier.highlighted ? "default" : "secondary"}
                >
                  Talk to sales
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
        <p className="mt-8 text-center text-xs text-muted-foreground">
          Prices in USD. Annual billing and multi-site discounts available.
        </p>
      </div>
    </MotionSection>
  );
}
