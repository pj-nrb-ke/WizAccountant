import { useState } from "react";
import { ScrollView } from "react-native";
import { Btn, Card, Loading, Mono, Muted, Screen } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import { api } from "../../src/lib/api";

export default function DashboardScreen() {
  const { session } = useApp();
  const [result, setResult] = useState("");
  const [busy, setBusy] = useState(false);

  if (!session?.siteId) {
    return (
      <Screen title="Dashboard">
        <Muted>Select a site first (Sites tab).</Muted>
      </Screen>
    );
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: "#0f172a" }} contentContainerStyle={{ padding: 16 }}>
      <Screen title={session.siteName ?? "Dashboard"}>
        <Muted>Phase 2 — live KPIs from Sage</Muted>
        <Btn
          label="Refresh KPIs"
          onPress={async () => {
            if (!session) return;
            setBusy(true);
            try {
              const job = await api.dashboard(session);
              setResult(job.resultJson ?? JSON.stringify(job));
            } catch (e) {
              setResult(e instanceof Error ? e.message : "Error");
            } finally {
              setBusy(false);
            }
          }}
          disabled={busy}
        />
        {busy ? <Loading /> : result ? <Card><Mono>{result}</Mono></Card> : null}
      </Screen>
    </ScrollView>
  );
}
