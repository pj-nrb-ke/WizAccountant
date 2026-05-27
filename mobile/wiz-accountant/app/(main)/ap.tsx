import { useState } from "react";
import { ScrollView } from "react-native";
import { Btn, Card, Loading, Mono, Muted, Screen } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import { api } from "../../src/lib/api";

export default function ApScreen() {
  const { session } = useApp();
  const [result, setResult] = useState("");
  const [busy, setBusy] = useState(false);

  if (!session?.siteId) {
    return (
      <Screen title="Suppliers">
        <Muted>Select a site first.</Muted>
      </Screen>
    );
  }

  async function run(op: string) {
    if (!session) return;
    setBusy(true);
    try {
      const job = await api.runJob(session, op, { top: "50" });
      setResult(job.resultJson ?? JSON.stringify(job));
    } catch (e) {
      setResult(e instanceof Error ? e.message : "Error");
    } finally {
      setBusy(false);
    }
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: "#0f172a" }} contentContainerStyle={{ padding: 16 }}>
      <Screen title="Accounts payable">
        <Btn label="All suppliers" onPress={() => run("supplier.list")} disabled={busy} />
        <Btn label="Open items" onPress={() => run("supplier.openitems")} disabled={busy} />
        {busy ? <Loading /> : null}
        {result ? (
          <Card>
            <Mono>{result.slice(0, 4000)}</Mono>
          </Card>
        ) : null}
      </Screen>
    </ScrollView>
  );
}
