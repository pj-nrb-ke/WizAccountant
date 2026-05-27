import { useFocusEffect } from "expo-router";
import { useCallback, useState } from "react";
import { Alert, FlatList, RefreshControl, Text } from "react-native";
import { Btn, Card, Loading, Muted, Screen, colors } from "../../src/components/ui";
import { useApp } from "../../src/context/AppContext";
import { api } from "../../src/lib/api";
import type { ApprovalProposal } from "../../src/types";

export default function ApprovalsScreen() {
  const { session } = useApp();
  const [items, setItems] = useState<ApprovalProposal[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!session?.siteId) return;
    setRefreshing(true);
    try {
      setItems(await api.proposals(session, 0));
    } catch (e) {
      Alert.alert("Error", e instanceof Error ? e.message : "Failed to load");
    } finally {
      setRefreshing(false);
    }
  }, [session]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  if (!session?.siteId) {
    return (
      <Screen title="Approvals">
        <Muted>Select a site first.</Muted>
      </Screen>
    );
  }

  const canApprove = session.role === "Approver" || session.role === "Admin";

  return (
    <Screen title="Approval inbox">
      <Muted>Phase 3 — {canApprove ? "You can approve" : "Preparer: use web Act to propose"}</Muted>
      <Btn label="Refresh" onPress={load} variant="secondary" />
      <FlatList
        data={items}
        keyExtractor={(p) => p.proposalId}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.accent} />}
        renderItem={({ item }) => (
          <Card>
            <Text style={{ color: colors.text, fontWeight: "600" }}>{item.title}</Text>
            <Muted>
              {item.proposalType} · {item.preparedByName}
            </Muted>
            {canApprove ? (
              <>
                <Btn
                  label="Approve & post"
                  disabled={busyId === item.proposalId}
                  onPress={() => {
                    Alert.alert("Post to Sage?", "This cannot be auto-reversed.", [
                      { text: "Cancel", style: "cancel" },
                      {
                        text: "Approve",
                        onPress: async () => {
                          setBusyId(item.proposalId);
                          try {
                            await api.approve(session, item.proposalId);
                            await load();
                          } catch (e) {
                            Alert.alert("Failed", e instanceof Error ? e.message : "Error");
                          } finally {
                            setBusyId(null);
                          }
                        },
                      },
                    ]);
                  }}
                />
                <Btn
                  label="Reject"
                  variant="danger"
                  onPress={() => {
                    Alert.alert("Reject proposal?", item.title, [
                      { text: "Cancel", style: "cancel" },
                      {
                        text: "Reject",
                        style: "destructive",
                        onPress: async () => {
                          setBusyId(item.proposalId);
                          try {
                            await api.reject(session, item.proposalId, "Rejected from mobile");
                            await load();
                          } catch (e) {
                            Alert.alert("Failed", e instanceof Error ? e.message : "Error");
                          } finally {
                            setBusyId(null);
                          }
                        },
                      },
                    ]);
                  }}
                />
              </>
            ) : null}
            {busyId === item.proposalId ? <Loading /> : null}
          </Card>
        )}
        ListEmptyComponent={<Muted>No pending approvals.</Muted>}
      />
    </Screen>
  );
}
