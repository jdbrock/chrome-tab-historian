import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"
import type { Tab } from "./types"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

export function cleanTitle(title: string): string {
  return title.replace(/💤\s?/g, "");
}

export function faviconUrl(pageUrl: string): string {
  try {
    const domain = new URL(pageUrl).hostname;
    return `https://www.google.com/s2/favicons?domain=${domain}&sz=32`;
  } catch {
    return "";
  }
}

export function domainFromUrl(url: string): string {
  try {
    return new URL(url).hostname;
  } catch {
    return url;
  }
}

export interface GroupedSnapshot {
  snapshotId: number;
  snapshotTimestamp: string;
  profiles: Map<string, GroupedProfile>;
}

export interface GroupedProfile {
  profileName: string;
  profileDisplayName: string;
  windows: Map<number, GroupedWindow>;
}

export interface GroupedWindow {
  windowId: number;
  windowIndex: number;
  tabs: Tab[];
}

export function groupTabsByHierarchy(tabs: Tab[]): Map<number, GroupedSnapshot> {
  const map = new Map<number, GroupedSnapshot>();

  for (const tab of tabs) {
    let snapshot = map.get(tab.snapshotId);
    if (!snapshot) {
      snapshot = {
        snapshotId: tab.snapshotId,
        snapshotTimestamp: tab.snapshotTimestamp,
        profiles: new Map(),
      };
      map.set(tab.snapshotId, snapshot);
    }

    let profile = snapshot.profiles.get(tab.profileName);
    if (!profile) {
      profile = {
        profileName: tab.profileName,
        profileDisplayName: tab.profileDisplayName,
        windows: new Map(),
      };
      snapshot.profiles.set(tab.profileName, profile);
    }

    let window = profile.windows.get(tab.windowId);
    if (!window) {
      window = {
        windowId: tab.windowId,
        windowIndex: tab.windowIndex,
        tabs: [],
      };
      profile.windows.set(tab.windowId, window);
    }

    window.tabs.push(tab);
  }

  return map;
}
