import React, { createContext, useCallback, useContext, useEffect, useState } from "react";
import { registerPushToken } from "../lib/pushNotifications";
import { router } from "expo-router";
import type { Session, Site } from "../types";
import { api, health, login } from "../lib/api";
import { DEFAULT_API_URL } from "../lib/config";
import { clearSession, loadSession, saveSession } from "../lib/storage";

type AppContextValue = {
  session: Session | null;
  sites: Site[];
  loading: boolean;
  signIn: (email: string, password: string, apiUrl: string) => Promise<void>;
  signOut: () => Promise<void>;
  refreshSites: () => Promise<void>;
  selectSite: (site: Site) => Promise<void>;
};

const AppContext = createContext<AppContextValue | null>(null);

export function AppProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [sites, setSites] = useState<Site[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      const raw = await loadSession();
      if (raw) {
        try {
          const s = JSON.parse(raw) as Session;
          setSession(s);
          if (s.siteId) router.replace("/(main)/dashboard");
          else router.replace("/(main)/sites");
        } catch {
          await clearSession();
        }
      }
      setLoading(false);
    })();
  }, []);

  const persist = async (s: Session) => {
    setSession(s);
    await saveSession(JSON.stringify(s));
  };

  const signIn = useCallback(async (email: string, password: string, apiUrl: string) => {
    const ok = await health(apiUrl);
    if (!ok) throw new Error("Cannot reach API. Check URL and that WizAccountant.Api is running.");

    const res = await login(apiUrl, email, password);
    const s: Session = {
      token: res.token,
      tenantId: res.tenantId,
      userId: res.userId,
      displayName: res.displayName,
      role: res.role,
      apiBaseUrl: apiUrl,
    };
    await persist(s);
    router.replace("/(main)/sites");
    // M4: register Expo push token in background
    registerPushToken(s).catch(() => {});
  }, []);

  const signOut = useCallback(async () => {
    await clearSession();
    setSession(null);
    setSites([]);
    router.replace("/");
  }, []);

  const refreshSites = useCallback(async () => {
    if (!session) return;
    const list = await api.sites(session);
    setSites(list);
  }, [session]);

  const selectSite = useCallback(
    async (site: Site) => {
      if (!session) return;
      const next = { ...session, siteId: site.siteId, siteName: site.siteName };
      await persist(next);
      router.replace("/(main)/dashboard");
    },
    [session]
  );

  return (
    <AppContext.Provider
      value={{
        session,
        sites,
        loading,
        signIn,
        signOut,
        refreshSites,
        selectSite,
      }}
    >
      {children}
    </AppContext.Provider>
  );
}

export function useApp() {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error("useApp outside AppProvider");
  return ctx;
}
