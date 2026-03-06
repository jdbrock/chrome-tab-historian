import type {
  Snapshot, Profile, TabsResponse,
  TabIdentityResponse, TabEventsResponse, TabMachineStats, TabMachineProfile, TimelineTab,
} from "./types";

const BASE = "/api";

export async function fetchSnapshots(): Promise<Snapshot[]> {
  const res = await fetch(`${BASE}/snapshots`);
  if (!res.ok) throw new Error(`Failed to fetch snapshots: ${res.status}`);
  return res.json();
}

export async function fetchProfiles(): Promise<Profile[]> {
  const res = await fetch(`${BASE}/profiles`);
  if (!res.ok) throw new Error(`Failed to fetch profiles: ${res.status}`);
  return res.json();
}

export async function fetchTabs(params: {
  q?: string;
  snapshotId?: number;
  profile?: string;
  page?: number;
  pageSize?: number;
}): Promise<TabsResponse> {
  const searchParams = new URLSearchParams();
  if (params.q) searchParams.set("q", params.q);
  if (params.snapshotId) searchParams.set("snapshotId", String(params.snapshotId));
  if (params.profile) searchParams.set("profile", params.profile);
  if (params.page) searchParams.set("page", String(params.page));
  if (params.pageSize) searchParams.set("pageSize", String(params.pageSize));

  const res = await fetch(`${BASE}/tabs?${searchParams}`);
  if (!res.ok) throw new Error(`Failed to fetch tabs: ${res.status}`);
  return res.json();
}

// Tab Machine API
export async function fetchTabMachineStats(): Promise<TabMachineStats> {
  const res = await fetch(`${BASE}/tabmachine/stats`);
  if (!res.ok) throw new Error(`Failed to fetch stats: ${res.status}`);
  return res.json();
}

export async function fetchTabMachineProfiles(): Promise<TabMachineProfile[]> {
  const res = await fetch(`${BASE}/tabmachine/profiles`);
  if (!res.ok) throw new Error(`Failed to fetch profiles: ${res.status}`);
  return res.json();
}

export async function searchTabMachine(params: {
  q?: string;
  profile?: string;
  isOpen?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
}): Promise<TabIdentityResponse> {
  const sp = new URLSearchParams();
  if (params.q) sp.set("q", params.q);
  if (params.profile) sp.set("profile", params.profile);
  if (params.isOpen !== undefined) sp.set("isOpen", String(params.isOpen));
  if (params.sort) sp.set("sort", params.sort);
  if (params.page) sp.set("page", String(params.page));
  if (params.pageSize) sp.set("pageSize", String(params.pageSize));

  const res = await fetch(`${BASE}/tabmachine/search?${sp}`);
  if (!res.ok) throw new Error(`Failed to search tabs: ${res.status}`);
  return res.json();
}

export async function fetchTabEvents(params: {
  tabIdentityId?: number;
  eventType?: string;
  before?: string;
  after?: string;
  page?: number;
  pageSize?: number;
}): Promise<TabEventsResponse> {
  const sp = new URLSearchParams();
  if (params.tabIdentityId) sp.set("tabIdentityId", String(params.tabIdentityId));
  if (params.eventType) sp.set("eventType", params.eventType);
  if (params.before) sp.set("before", params.before);
  if (params.after) sp.set("after", params.after);
  if (params.page) sp.set("page", String(params.page));
  if (params.pageSize) sp.set("pageSize", String(params.pageSize));

  const res = await fetch(`${BASE}/tabmachine/events?${sp}`);
  if (!res.ok) throw new Error(`Failed to fetch events: ${res.status}`);
  return res.json();
}

export async function fetchTimeline(timestamp: string, profile?: string): Promise<TimelineTab[]> {
  const sp = new URLSearchParams({ timestamp });
  if (profile) sp.set("profile", profile);

  const res = await fetch(`${BASE}/tabmachine/timeline?${sp}`);
  if (!res.ok) throw new Error(`Failed to fetch timeline: ${res.status}`);
  return res.json();
}
