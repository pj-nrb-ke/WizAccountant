import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors",
  {
    variants: {
      variant: {
        default:
          "border-border bg-surface text-foreground/80 dark:bg-surface/60 dark:text-muted-foreground",
        accent:
          "border-primary/40 bg-primary/15 text-blue-800 dark:border-primary/30 dark:bg-primary/10 dark:text-blue-100",
        glow:
          "border-cyan-600/35 bg-cyan-500/15 text-cyan-900 dark:border-cyan-500/30 dark:bg-cyan-500/10 dark:text-cyan-100",
      },
    },
    defaultVariants: { variant: "default" },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  );
}

export { Badge, badgeVariants };
