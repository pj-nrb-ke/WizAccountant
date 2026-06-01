"use client";

import { motion } from "framer-motion";
import { ArrowRight } from "lucide-react";
import { Button } from "@/components/ui/button";

export function CtaSection() {
  return (
    <section className="py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <motion.div
          className="relative overflow-hidden rounded-3xl border border-primary/25 bg-gradient-to-br from-primary/10 via-surface to-cyan-500/10 px-8 py-16 text-center dark:border-primary/30 dark:from-primary/20 dark:via-surface/80 md:px-16"
          initial={{ opacity: 0, scale: 0.98 }}
          whileInView={{ opacity: 1, scale: 1 }}
          viewport={{ once: true }}
          transition={{ duration: 0.5 }}
        >
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-primary/20 via-transparent to-transparent" />
          <h2 className="relative text-3xl font-bold tracking-tight md:text-4xl">
            Ready for the future of finance operations?
          </h2>
          <p className="relative mx-auto mt-4 max-w-xl text-lg text-muted-foreground">
            Join finance leaders who treat reconciliation, treasury, and
            explainability as a single intelligent platform.
          </p>
          <div className="relative mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            <Button size="lg" className="group">
              Schedule executive demo
              <ArrowRight className="h-4 w-4 group-hover:translate-x-0.5 transition-transform" />
            </Button>
            <Button variant="secondary" size="lg">
              Contact sales
            </Button>
          </div>
        </motion.div>
      </div>
    </section>
  );
}
