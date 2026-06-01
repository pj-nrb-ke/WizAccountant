import { Platform } from "react-native";

/** Baked at APK build time via EXPO_PUBLIC_API_URL; override on login / Settings. */
export const DEFAULT_API_URL =
  process.env.EXPO_PUBLIC_API_URL ??
  (Platform.OS === "android" ? "http://10.0.2.2:5278" : "http://localhost:5278");

export const PREPARER_ID = "33333333-3333-3333-3333-333333333333";
export const APPROVER_ID = "22222222-2222-2222-2222-222222222222";
export const ADMIN_ID = "11111111-1111-1111-1111-111111111111";
