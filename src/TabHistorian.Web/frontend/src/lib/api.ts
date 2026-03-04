import type { Snapshot, Profile, TabsResponse } from "./types";

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
