import { useQuery, useInfiniteQuery } from "@tanstack/react-query";
import { fetchSnapshots, fetchProfiles, fetchTabs } from "./api";

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
