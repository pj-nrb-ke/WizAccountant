"use client";

import { motion } from "framer-motion";
import { cn } from "@/lib/utils";

const fadeUp = {
  hidden: { opacity: 0, y: 28 },
  visible: { opacity: 1, y: 0 },
};

type MotionSectionProps = {
  children: React.ReactNode;
  className?: string;
  id?: string;
  delay?: number;
};

export function MotionSection({
  children,
  className,
  id,
  delay = 0,
}: MotionSectionProps) {
  return (
    <motion.section
      id={id}
      className={cn(className)}
      initial="hidden"
      whileInView="visible"
      viewport={{ once: true, margin: "-80px" }}
      transition={{ duration: 0.55, delay, ease: [0.22, 1, 0.36, 1] }}
      variants={fadeUp}
    >
      {children}
    </motion.section>
  );
}
