"use client";

import { useEffect, useRef, useCallback } from "react";
import { useInfiniteTabs } from "@/lib/hooks";
import { TabCard } from "./tab-card";
import { Skeleton } from "@/components/ui/skeleton";

interface TabFeedProps {
  query?: string;
  snapshotId?: number;
  profile?: string;
}

export function TabFeed({ query, snapshotId, profile }: TabFeedProps) {
  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading,
    isError,
  } = useInfiniteTabs({ q: query, snapshotId, profile });

  const sentinelRef = useRef<HTMLDivElement>(null);

  const handleIntersect = useCallback(
    (entries: IntersectionObserverEntry[]) => {
      if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
        fetchNextPage();
      }
    },
    [hasNextPage, isFetchingNextPage, fetchNextPage]
  );

  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;
    const observer = new IntersectionObserver(handleIntersect, {
      threshold: 0.1,
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [handleIntersect]);

  const allTabs = data?.pages.flatMap((p) => p.items) ?? [];
  const totalCount = data?.pages[0]?.totalCount ?? 0;

  if (isLoading) {
    return (
      <div className="space-y-3 w-full">
        {Array.from({ length: 8 }).map((_, i) => (
          <Skeleton key={i} className="h-20 w-full rounded-lg" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-center text-muted-foreground py-12">
        Failed to load tabs. Is the API running on localhost:17000?
      </div>
    );
  }

  if (allTabs.length === 0) {
    return (
      <div className="text-center text-muted-foreground py-12">
        {query ? "No tabs match your search." : "No tabs found."}
      </div>
    );
  }

  return (
    <div className="w-full space-y-2">
      <div className="text-xs text-muted-foreground mb-2">
        {totalCount.toLocaleString()} result{totalCount !== 1 ? "s" : ""}
      </div>
      <div className="space-y-2">
        {allTabs.map((tab) => (
          <TabCard key={`${tab.snapshotId}-${tab.tabId}`} tab={tab} />
        ))}
      </div>
      <div ref={sentinelRef} className="h-8" />
      {isFetchingNextPage && (
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full rounded-lg" />
          ))}
        </div>
      )}
    </div>
  );
}
