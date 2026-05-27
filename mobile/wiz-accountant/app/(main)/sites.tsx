import { useFocusEffect } from "expo-router";
import { useCallback, useState } from "react";
import { FlatList, RefreshControl, Text } from "react-native";
import { Btn, Card, Loading, Muted, Screen, colors } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import type { Site } from "../../src/types";

export default function SitesScreen() {
  const { sites, refreshSites, selectSite, session } = useApp();
  const [refreshing, setRefreshing] = useState(false);
  const [initial, setInitial] = useState(true);

  const load = useCallback(async () => {
    setRefreshing(true);
    try {
      await refreshSites();
    } finally {
      setRefreshing(false);
      setInitial(false);
    }
  }, [refreshSites]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  if (initial && refreshing) {
    return (
      <Screen>
        <Loading />
      </Screen>
    );
  }

  return (
    <Screen title="Choose site">
      <Muted>Signed in as {session?.displayName} ({session?.role})</Muted>
      <FlatList
        data={sites}
        keyExtractor={(item) => item.siteId}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.accent} />}
        renderItem={({ item }: { item: Site }) => (
          <Card>
            <Text style={{ color: colors.text, fontWeight: "600", fontSize: 16 }}>{item.siteName}</Text>
            <Muted>{item.isOnline ? "● Online" : "○ Offline"}</Muted>
            <Btn label="Use this site" onPress={() => selectSite(item)} />
          </Card>
        )}
        ListEmptyComponent={<Muted>No sites — pair a connector in Admin first.</Muted>}
      />
    </Screen>
  );
}
