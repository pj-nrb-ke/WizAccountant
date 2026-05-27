import { useState } from "react";
import { Alert, ScrollView } from "react-native";
import { Btn, Input, Muted, Screen } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import { saveSession } from "../../src/lib/storage";

export default function SettingsScreen() {
  const { session, signOut } = useApp();
  const [apiUrl, setApiUrl] = useState(session?.apiBaseUrl ?? "");

  return (
    <ScrollView style={{ flex: 1, backgroundColor: "#0f172a" }} contentContainerStyle={{ padding: 16 }}>
      <Screen title="Settings">
        <Muted>Signed in: {session?.displayName}</Muted>
        <Muted>Role: {session?.role}</Muted>
        <Muted>Site: {session?.siteName ?? "not selected"}</Muted>
        <Muted>API URL (save & re-login to apply)</Muted>
        <Input value={apiUrl} onChangeText={setApiUrl} autoCapitalize="none" />
        <Btn
          label="Save API URL"
          variant="secondary"
          onPress={async () => {
            if (!session) return;
            const next = { ...session, apiBaseUrl: apiUrl };
            await saveSession(JSON.stringify(next));
            Alert.alert("Saved", "Sign out and sign in again to use the new API URL.");
          }}
        />
        <Btn label="Sign out" variant="danger" onPress={signOut} />
        <Muted>Push notifications (FCM/APNs) — Phase 4.</Muted>
        <Muted>On a physical phone use your PC LAN IP (e.g. http://192.168.1.10:5278).</Muted>
      </Screen>
    </ScrollView>
  );
}
