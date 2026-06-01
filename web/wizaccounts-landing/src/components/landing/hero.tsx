"use client";

import { motion } from "framer-motion";
import { ArrowRight, Shield } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { DashboardMockup } from "@/components/landing/dashboard-mockup";

export function Hero() {
  return (
    <section className="relative overflow-hidden pt-28 pb-16 md:pt-36 md:pb-24">
      <div className="hero-glow pointer-events-none absolute inset-0" />
      <div className="mx-auto grid max-w-6xl items-center gap-12 px-4 md:grid-cols-2 md:gap-10 md:px-6">
        <div>
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5 }}
          >
            <Badge variant="accent" className="mb-6 gap-1.5 px-3 py-1">
              <Shield className="h-3 w-3" />
              Sage 200 Evolution · secure cloud bridge
            </Badge>
          </motion.div>

          <motion.h1
            className="text-4xl font-bold leading-[1.08] tracking-tight text-foreground md:text-5xl lg:text-[3.25rem]"
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.55, delay: 0.05 }}
          >
            The AI-powered finance intelligence platform
          </motion.h1>

          <motion.p
            className="mt-6 max-w-xl text-lg leading-relaxed text-muted-foreground md:text-xl"
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.55, delay: 0.12 }}
          >
            Connect on-prem WizConnector to live Sage data. Ask Insight AI in plain
            language — aggregation, top-N ranking, and reconciliation with cited,
            data-as-of answers. Approve writes through maker-checker workflows on
            web and mobile.
          </motion.p>

          <motion.div
            className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center"
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.55, delay: 0.2 }}
          >
            <Button size="lg" className="group">
              Book executive demo
              <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
            </Button>
            <Button variant="secondary" size="lg">
              Explore the platform
            </Button>
          </motion.div>

          <motion.p
            className="mt-8 text-sm text-muted-foreground"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.35 }}
          >
            Business meaning before table meaning — built for CFOs and controllers,
            not legacy ERP enquiry screens.
          </motion.p>
        </div>

        <div className="relative perspective-[1200px]">
          <DashboardMockup />
        </div>
      </div>
    </section>
  );
}
