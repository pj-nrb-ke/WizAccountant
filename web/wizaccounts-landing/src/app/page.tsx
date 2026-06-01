import { Navbar } from "@/components/landing/navbar";
import { Hero } from "@/components/landing/hero";
import { TrustBar } from "@/components/landing/trust-bar";
import { PlatformSection } from "@/components/landing/platform-section";
import {
  BusinessProcessIntelligence,
  CoreAiCapabilities,
  FinanceIntelligence,
  ReconciliationIntelligence,
  TreasuryIntelligence,
} from "@/components/landing/intelligence-sections";
import { DashboardPreview } from "@/components/landing/dashboard-preview";
import { FeatureHighlights } from "@/components/landing/feature-highlights";
import { Testimonials } from "@/components/landing/testimonials";
import { PricingPreview } from "@/components/landing/pricing-preview";
import { CtaSection } from "@/components/landing/cta-section";
import { Footer } from "@/components/landing/footer";

export default function Home() {
  return (
    <div className="bg-background text-foreground">
      <Navbar />
      <main>
        <Hero />
        <TrustBar />
        <PlatformSection />
        <CoreAiCapabilities />
        <FinanceIntelligence />
        <ReconciliationIntelligence />
        <TreasuryIntelligence />
        <BusinessProcessIntelligence />
        <DashboardPreview />
        <FeatureHighlights />
        <Testimonials />
        <PricingPreview />
        <CtaSection />
      </main>
      <Footer />
    </div>
  );
}
