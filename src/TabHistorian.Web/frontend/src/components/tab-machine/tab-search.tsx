"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useTabMachineSearch, useTabMachineProfiles } from "@/lib/hooks";
import { SearchBox } from "@/components/search-box";
import { TabIdentityCard } from "./tab-identity-card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export function TabSearch() {
  const [query, setQuery] = useState("");
  const [profile, setProfile] = useState<string | undefined>(undefined);
  const [isOpen, setIsOpen] = useState<boolean | undefined>(undefined);

  const { data: profiles } = useTabMachineProfiles();

  const { data, isLoading, hasNextPage, fetchNextPage, isFetchingNextPage } =
    useTabMachineSearch({
      q: query || undefined,
      profile,
      isOpen,
    });

  const handleQueryChange = useCallback((q: string) => setQuery(q), []);

  // Infinite scroll sentinel
  const sentinelRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!sentinelRef.current) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { rootMargin: "200px" }
    );
    observer.observe(sentinelRef.current);
    return () => observer.disconnect();
  }, [hasNextPage, fetchNextPage, isFetchingNextPage]);

  const allItems = data?.pages.flatMap((p) => p.items) ?? [];
  const totalCount = data?.pages[0]?.totalCount ?? 0;

  return (
    <div className="space-y-4">
      <SearchBox onQueryChange={handleQueryChange} />

      <div className="flex items-center gap-3 flex-wrap">
        {profiles && profiles.length > 1 && (
          <Select
            value={profile ?? "all"}
            onValueChange={(v) => setProfile(v === "all" ? undefined : v)}
          >
            <SelectTrigger size="sm" className="w-auto">
              <SelectValue placeholder="All profiles" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All profiles</SelectItem>
              {profiles.map((p) => (
                <SelectItem key={p} value={p}>
                  {p}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        <Select
          value={isOpen === undefined ? "all" : isOpen ? "open" : "closed"}
          onValueChange={(v) =>
            setIsOpen(v === "all" ? undefined : v === "open")
          }
        >
          <SelectTrigger size="sm" className="w-auto">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All tabs</SelectItem>
            <SelectItem value="open">Open</SelectItem>
            <SelectItem value="closed">Closed</SelectItem>
          </SelectContent>
        </Select>
        {!isLoading && (
          <span className="text-xs text-muted-foreground">
            {totalCount.toLocaleString()} tab{totalCount !== 1 ? "s" : ""}
          </span>
        )}
      </div>

      <div className="space-y-2">
        {isLoading
          ? Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full" />
            ))
          : allItems.map((identity) => (
              <TabIdentityCard key={identity.id} identity={identity} />
            ))}
        {allItems.length === 0 && !isLoading && (
          <p className="text-sm text-muted-foreground text-center py-8">
            No tabs found.
          </p>
        )}
        <div ref={sentinelRef} />
        {isFetchingNextPage && <Skeleton className="h-24 w-full" />}
      </div>
    </div>
  );
}
