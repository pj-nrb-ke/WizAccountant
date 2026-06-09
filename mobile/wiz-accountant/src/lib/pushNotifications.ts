/**
 * WizAccountant Mobile — Push Notifications  (M4)
 *
 * Registers the device for Expo push notifications, returns the push token,
 * and registers it with the WizAccountant API so the server can send
 * job-completed / approval-required / alert-threshold-hit alerts.
 *
 * Usage (in AppContext after login):
 *   import { registerPushToken } from '../lib/pushNotifications';
 *   await registerPushToken(session);
 */

import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import type { Session } from '../types';

// ── Handler config ────────────────────────────────────────────────────────────
// Show notification banner + play sound even when app is in foreground
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

// ── Registration ──────────────────────────────────────────────────────────────

/**
 * Requests permission, gets the Expo push token, and POSTs it to the API.
 * Safe to call multiple times — server upserts the token.
 */
export async function registerPushToken(session: Session): Promise<string | null> {
  if (!Device.isDevice) {
    console.info('[Push] Skipped — not a physical device (simulator/emulator).');
    return null;
  }

  // Request permission
  const { status: existing } = await Notifications.getPermissionsAsync();
  let finalStatus = existing;
  if (existing !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }
  if (finalStatus !== 'granted') {
    console.warn('[Push] Permission denied.');
    return null;
  }

  // Android notification channel
  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('wiz-alerts', {
      name: 'WizAccountant Alerts',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#1a1a2e',
    });
  }

  const token = (await Notifications.getExpoPushTokenAsync()).data;

  // Register with API
  try {
    const base = session.apiBaseUrl.replace(/\/$/, '');
    await fetch(`${base}/api/push-tokens`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${session.token}`,
      },
      body: JSON.stringify({
        token,
        platform: Platform.OS,
        userId: session.userId,
      }),
    });
  } catch (err) {
    console.warn('[Push] Failed to register token with API:', err);
  }

  return token;
}

// ── Notification event listeners ──────────────────────────────────────────────

export type WizPushEvent = {
  event: string;
  siteId?: string;
  jobId?: string;
  proposalId?: string;
  message?: string;
  timestampUtc?: string;
};

type PushHandler = (evt: WizPushEvent) => void;

/**
 * Subscribe to incoming push notifications.
 * Returns a cleanup function — call it on component unmount.
 */
export function subscribeToPush(onNotification: PushHandler): () => void {
  const sub = Notifications.addNotificationReceivedListener(notification => {
    const data = notification.request.content.data as WizPushEvent | undefined;
    if (data) onNotification(data);
  });
  return () => Notifications.removeNotificationSubscription(sub);
}

/**
 * Subscribe to notification taps (user opened notification).
 */
export function subscribeToTap(onTap: PushHandler): () => void {
  const sub = Notifications.addNotificationResponseReceivedListener(response => {
    const data = response.notification.request.content.data as WizPushEvent | undefined;
    if (data) onTap(data);
  });
  return () => Notifications.removeNotificationSubscription(sub);
}
