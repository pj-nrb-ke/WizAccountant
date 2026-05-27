import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from "react-native";

export const colors = {
  bg: "#0f172a",
  card: "#1e293b",
  text: "#f1f5f9",
  muted: "#94a3b8",
  accent: "#38bdf8",
  accent2: "#818cf8",
  ok: "#86efac",
  bad: "#fca5a5",
  border: "#334155",
};

export function Screen({ children, title }: { children: React.ReactNode; title?: string }) {
  return (
    <View style={styles.screen}>
      {title ? <Text style={styles.title}>{title}</Text> : null}
      {children}
    </View>
  );
}

export function Card({ children }: { children: React.ReactNode }) {
  return <View style={styles.card}>{children}</View>;
}

export function Btn({
  label,
  onPress,
  variant = "primary",
  disabled,
}: {
  label: string;
  onPress: () => void;
  variant?: "primary" | "secondary" | "danger";
  disabled?: boolean;
}) {
  const bg =
    variant === "primary" ? colors.accent : variant === "danger" ? "#dc2626" : "#475569";
  return (
    <Pressable
      style={[styles.btn, { backgroundColor: bg, opacity: disabled ? 0.5 : 1 }]}
      onPress={onPress}
      disabled={disabled}
    >
      <Text style={[styles.btnText, variant === "primary" && { color: "#0f172a" }]}>{label}</Text>
    </Pressable>
  );
}

export function Input(props: TextInputProps) {
  return <TextInput {...props} style={[styles.input, props.style]} placeholderTextColor={colors.muted} />;
}

export function Muted({ children }: { children: React.ReactNode }) {
  return <Text style={styles.muted}>{children}</Text>;
}

export function Mono({ children }: { children: string }) {
  return <Text style={styles.mono}>{children}</Text>;
}

export function Loading() {
  return <ActivityIndicator color={colors.accent} style={{ marginVertical: 24 }} />;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg, padding: 16 },
  title: { fontSize: 22, fontWeight: "700", color: colors.text, marginBottom: 12 },
  card: {
    backgroundColor: colors.card,
    borderRadius: 10,
    padding: 14,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: colors.border,
  },
  btn: { paddingVertical: 12, paddingHorizontal: 16, borderRadius: 8, alignItems: "center", marginVertical: 4 },
  btnText: { color: "#fff", fontWeight: "600", fontSize: 15 },
  input: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: 12,
    color: colors.text,
    marginBottom: 10,
    fontSize: 16,
  },
  muted: { color: colors.muted, fontSize: 14, marginBottom: 8 },
  mono: { color: colors.text, fontFamily: "monospace", fontSize: 12 },
});
