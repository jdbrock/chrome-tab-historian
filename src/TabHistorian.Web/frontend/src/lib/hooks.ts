import { useQuery, useInfiniteQuery } from "@tanstack/react-query";
import {
  fetchSnapshots, fetchProfiles, fetchTabs,
  fetchTabMachineStats, fetchTabMachineProfiles, searchTabMachine, fetchTabEvents, fetchTabCurrentState, fetchTimeline,
} from "./api";

export function useSnapshots() {
  return useQuery({
    queryKey: ["snapshots"],
    queryFn: fetchSnapshots,
  });
}

export function useProfiles() {
  return useQuery({
    queryKey: ["profiles"],
    queryFn: fetchProfiles,
  });
}

export function useInfiniteTabs(params: {
  q?: string;
  snapshotId?: number;
  profile?: string;
  pageSize?: number;
}) {
  const pageSize = params.pageSize ?? 50;

  return useInfiniteQuery({
    queryKey: ["tabs", params.q, params.snapshotId, params.profile, pageSize],
    queryFn: ({ pageParam }) =>
      fetchTabs({ ...params, page: pageParam, pageSize }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const fetched = lastPage.page * lastPage.pageSize;
      return fetched < lastPage.totalCount ? lastPage.page + 1 : undefined;
    },
  });
}

// Tab Machine hooks
export function useTabMachineStats() {
  return useQuery({
    queryKey: ["tabmachine", "stats"],
    queryFn: fetchTabMachineStats,
  });
}

export function useTabMachineProfiles() {
  return useQuery({
    queryKey: ["tabmachine", "profiles"],
    queryFn: fetchTabMachineProfiles,
  });
}

export function useTabMachineSearch(params: {
  q?: string;
  profile?: string;
  isOpen?: boolean;
  sort?: string;
  pageSize?: number;
}) {
  const pageSize = params.pageSize ?? 50;
  return useInfiniteQuery({
    queryKey: ["tabmachine", "search", params.q, params.profile, params.isOpen, params.sort, pageSize],
    queryFn: ({ pageParam }) =>
      searchTabMachine({ ...params, page: pageParam, pageSize }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const fetched = lastPage.page * lastPage.pageSize;
      return fetched < lastPage.totalCount ? lastPage.page + 1 : undefined;
    },
  });
}

export function useTabEvents(params: {
  tabIdentityId?: number;
  eventType?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ["tabmachine", "events", params.tabIdentityId, params.eventType, params.page],
    queryFn: () => fetchTabEvents(params),
    enabled: !!params.tabIdentityId,
  });
}

export function useTabCurrentState(tabIdentityId: number | undefined) {
  return useQuery({
    queryKey: ["tabmachine", "tab", tabIdentityId],
    queryFn: () => fetchTabCurrentState(tabIdentityId!),
    enabled: !!tabIdentityId,
  });
}

export function useTimeline(timestamp: string | undefined, profile?: string) {
  return useQuery({
    queryKey: ["tabmachine", "timeline", timestamp, profile],
    queryFn: () => fetchTimeline(timestamp!, profile),
    enabled: !!timestamp,
  });
}
