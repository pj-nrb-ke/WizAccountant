import { useState } from "react";
import { FlatList, KeyboardAvoidingView, Platform, Text, View } from "react-native";
import { Btn, Input, Muted, Screen, colors } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import { api } from "../../src/lib/api";

type Msg = { role: "user" | "assistant"; text: string };

export default function ChatScreen() {
  const { session } = useApp();
  const [messages, setMessages] = useState<Msg[]>([
    { role: "assistant", text: "Read-only assistant. Try: list customers, open items, show dashboard." },
  ]);
  const [input, setInput] = useState("");
  const [conversationId, setConversationId] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);

  if (!session?.siteId) {
    return (
      <Screen title="AI Assistant">
        <Muted>Select a site first.</Muted>
      </Screen>
    );
  }

  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: colors.bg }}
      behavior={Platform.OS === "ios" ? "padding" : undefined}
      keyboardVerticalOffset={80}
    >
      <Screen title="AI Assistant">
        <Muted>Phase 2 — read-only tools</Muted>
        <FlatList
          style={{ flex: 1, marginBottom: 8 }}
          data={messages}
          keyExtractor={(_, i) => String(i)}
          renderItem={({ item }) => (
            <View
              style={{
                alignSelf: item.role === "user" ? "flex-end" : "flex-start",
                backgroundColor: item.role === "user" ? colors.accent2 : colors.card,
                padding: 10,
                borderRadius: 8,
                marginVertical: 4,
                maxWidth: "85%",
              }}
            >
              <Text style={{ color: colors.text }}>{item.text}</Text>
            </View>
          )}
        />
        <Input
          placeholder="Ask about customers, suppliers…"
          value={input}
          onChangeText={setInput}
        />
        <Btn
          label="Send"
          disabled={busy || !input.trim()}
          onPress={async () => {
            const msg = input.trim();
            setInput("");
            setMessages((m) => [...m, { role: "user", text: msg }]);
            setBusy(true);
            try {
              const res = await api.chat(session, msg, conversationId);
              setConversationId(res.conversationId);
              setMessages((m) => [...m, { role: "assistant", text: res.reply }]);
            } catch (e) {
              setMessages((m) => [
                ...m,
                { role: "assistant", text: e instanceof Error ? e.message : "Error" },
              ]);
            } finally {
              setBusy(false);
            }
          }}
        />
      </Screen>
    </KeyboardAvoidingView>
  );
}
