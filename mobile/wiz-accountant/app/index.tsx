import { useRouter } from "expo-router";
import { useEffect, useState } from "react";
import { KeyboardAvoidingView, Platform, ScrollView } from "react-native";
import { Btn, Input, Muted, Screen, colors } from "../src/components/ui";
import { useApp } from "../src/context/AppContext";
import { DEFAULT_API_URL } from "../src/lib/config";

export default function LoginScreen() {
  const { signIn, loading, session } = useApp();
  const router = useRouter();
  const [email, setEmail] = useState("preparer@pilot.local");
  const [password, setPassword] = useState("pilot");
  const [apiUrl, setApiUrl] = useState(DEFAULT_API_URL);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && session) {
      router.replace(session.siteId ? "/(main)/dashboard" : "/(main)/sites");
    }
  }, [loading, session, router]);

  if (loading) return <Screen><Muted>Loading…</Muted></Screen>;
  if (session) return <Screen><Muted>Signing in…</Muted></Screen>;

  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: colors.bg }}
      behavior={Platform.OS === "ios" ? "padding" : undefined}
    >
      <ScrollView contentContainerStyle={{ padding: 16, flexGrow: 1, justifyContent: "center" }}>
        <Screen title="WizAccountant">
          <Muted>Mobile — Phases 1–3 (read + approvals)</Muted>
          <Input placeholder="API URL" value={apiUrl} onChangeText={setApiUrl} autoCapitalize="none" />
          <Input
            placeholder="Email"
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            keyboardType="email-address"
          />
          <Input placeholder="Password" value={password} onChangeText={setPassword} secureTextEntry />
          {error ? <Muted>{error}</Muted> : null}
          <Btn
            label="Sign in"
            onPress={async () => {
              setBusy(true);
              setError("");
              try {
                await signIn(email, password, apiUrl);
              } catch (e) {
                setError(e instanceof Error ? e.message : "Login failed");
              } finally {
                setBusy(false);
              }
            }}
            disabled={busy}
          />
          <Muted>Pilot: preparer@pilot.local / approver@pilot.local — password pilot</Muted>
        </Screen>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
