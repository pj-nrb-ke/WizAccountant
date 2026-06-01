import { Zap } from "lucide-react";

const columns = [
  {
    title: "Product",
    links: ["Capabilities", "Reconciliation", "Treasury", "Security"],
  },
  {
    title: "Company",
    links: ["About", "Careers", "Contact", "Partners"],
  },
  {
    title: "Resources",
    links: ["Documentation", "API", "Status", "Privacy"],
  },
];

export function Footer() {
  return (
    <footer className="border-t border-border bg-surface/30 py-16">
      <div className="mx-auto max-w-6xl px-4 md:px-6">
        <div className="grid gap-12 md:grid-cols-4">
          <div>
            <a href="#" className="flex items-center gap-2 font-semibold">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-cyan-400 text-background">
                <Zap className="h-4 w-4" />
              </span>
              WizAccounts
            </a>
            <p className="mt-4 text-sm leading-relaxed text-muted-foreground">
              AI-powered finance intelligence for enterprise teams.
            </p>
          </div>
          {columns.map((col) => (
            <div key={col.title}>
              <p className="text-sm font-semibold text-foreground">{col.title}</p>
              <ul className="mt-4 space-y-2">
                {col.links.map((link) => (
                  <li key={link}>
                    <a
                      href="#"
                      className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                    >
                      {link}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="mt-12 flex flex-col items-center justify-between gap-4 border-t border-border pt-8 text-xs text-muted-foreground md:flex-row">
          <p>© {new Date().getFullYear()} WizAccounts. All rights reserved.</p>
          <p>AscendBooks · Finance intelligence platform</p>
        </div>
      </div>
    </footer>
  );
}
