import { Tabs } from "expo-router";
import { colors } from "../../src/components/ui";

export default function MainLayout() {
  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.card },
        headerTintColor: colors.text,
        tabBarStyle: { backgroundColor: colors.card, borderTopColor: colors.border },
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.muted,
      }}
    >
      <Tabs.Screen name="sites" options={{ title: "Sites", tabBarLabel: "Sites" }} />
      <Tabs.Screen name="dashboard" options={{ title: "Dashboard", tabBarLabel: "Home" }} />
      <Tabs.Screen name="ar" options={{ title: "Customers", tabBarLabel: "AR" }} />
      <Tabs.Screen name="ap" options={{ title: "Suppliers", tabBarLabel: "AP" }} />
      <Tabs.Screen name="approvals" options={{ title: "Approvals", tabBarLabel: "Approve" }} />
      <Tabs.Screen name="chat" options={{ title: "AI", tabBarLabel: "AI" }} />
      <Tabs.Screen name="settings" options={{ title: "Settings", tabBarLabel: "More" }} />
    </Tabs>
  );
}
