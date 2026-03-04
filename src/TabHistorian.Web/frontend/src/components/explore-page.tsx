"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Loader2 } from "lucide-react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useSnapshots, useProfiles, useInfiniteTabs } from "@/lib/hooks";
import { groupTabsByHierarchy } from "@/lib/utils";
import { SnapshotSection } from "./explore/snapshot-section";
import { useEffect } from "react";

export function ExplorePage() {
  const { data: snapshots, isLoading: loadingSnaps } = useSnapshots();
  const { data: profiles, isLoading: loadingProfiles } = useProfiles();
  const [selectedSnapshot, setSelectedSnapshot] = useState<string>("");
  const [selectedProfile, setSelectedProfile] = useState<string>("");

  const snapshotId = selectedSnapshot ? Number(selectedSnapshot) : undefined;
  const profileName = selectedProfile || undefined;

  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading: loadingTabs,
  } = useInfiniteTabs({
    snapshotId,
    profile: profileName,
    pageSize: 200,
  });

  // Auto-fetch all pages for tree view
  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const allTabs = useMemo(
    () => data?.pages.flatMap((p) => p.items) ?? [],
    [data]
  );

  const grouped = useMemo(() => groupTabsByHierarchy(allTabs), [allTabs]);

  const handleClear = () => {
    setSelectedSnapshot("");
    setSelectedProfile("");
  };

  return (
    <div className="min-h-screen flex flex-col">
      <header className="sticky top-0 z-10 bg-background/80 backdrop-blur-sm border-b border-border/50">
        <div className="max-w-4xl mx-auto px-4 py-3 flex items-center gap-4">
          <Link
            href="/"
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <h1 className="text-lg font-medium">Explorer</h1>

          <div className="flex items-center gap-2 ml-auto">
            {loadingSnaps ? (
              <Skeleton className="h-9 w-48" />
            ) : (
              <Select
                value={selectedSnapshot}
                onValueChange={setSelectedSnapshot}
              >
                <SelectTrigger className="w-52">
                  <SelectValue placeholder="All Snapshots" />
                </SelectTrigger>
                <SelectContent>
                  {snapshots?.map((s) => (
                    <SelectItem key={s.id} value={String(s.id)}>
                      {new Date(s.timestamp).toLocaleString()} ({s.tabCount}{" "}
                      tabs)
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            {loadingProfiles ? (
              <Skeleton className="h-9 w-40" />
            ) : (
              <Select
                value={selectedProfile}
                onValueChange={setSelectedProfile}
              >
                <SelectTrigger className="w-44">
                  <SelectValue placeholder="All Profiles" />
                </SelectTrigger>
                <SelectContent>
                  {profiles?.map((p) => (
                    <SelectItem key={p.profileName} value={p.profileName}>
                      {p.profileDisplayName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            {(selectedSnapshot || selectedProfile) && (
              <Button variant="ghost" size="sm" onClick={handleClear}>
                Clear
              </Button>
            )}
          </div>
        </div>
      </header>

      <main className="flex-1 max-w-4xl mx-auto w-full px-4 py-6">
        {loadingTabs ? (
          <div className="space-y-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-14 w-full rounded-lg" />
            ))}
          </div>
        ) : allTabs.length === 0 ? (
          <div className="text-center text-muted-foreground py-12">
            No data found.
          </div>
        ) : (
          <div className="space-y-3">
            {Array.from(grouped.values()).map((snapshot) => (
              <SnapshotSection
                key={snapshot.snapshotId}
                snapshot={snapshot}
              />
            ))}
            {isFetchingNextPage && (
              <div className="flex justify-center py-4">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
