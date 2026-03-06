"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { History } from "lucide-react";
import { useProfiles } from "@/lib/hooks";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchBox } from "./search-box";
import { StatsBar } from "./stats-bar";
import { TabFeed } from "./tab-feed";

export function HomePage() {
  const [query, setQuery] = useState("");
  const [profile, setProfile] = useState<string | undefined>(undefined);
  const { data: profiles } = useProfiles();

  const handleQueryChange = useCallback((q: string) => setQuery(q), []);

  return (
    <div className="min-h-screen flex flex-col">
      <div className="flex-1 flex flex-col items-center px-4 pt-16 sm:pt-24 pb-8">
        <div className="mb-8 text-center">
          <h1 className="text-lg font-medium text-muted-foreground tracking-wide">
            Full Snapshots
          </h1>
          <div className="flex items-center gap-3 mt-1">
            <Link
              href="/snapshots/explore"
              className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              <History className="h-3 w-3" />
              Explore All Data
            </Link>
            <Link
              href="/"
              className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              Tab Machine
            </Link>
          </div>
        </div>

        <div className="w-full max-w-2xl mb-8">
          <SearchBox onQueryChange={handleQueryChange} />
        </div>

        <div className="flex items-center gap-4 mb-8">
          {profiles && profiles.length > 1 && (
            <Select
              value={profile ?? "all"}
              onValueChange={(v) => setProfile(v === "all" ? undefined : v)}
            >
              <SelectTrigger size="sm">
                <SelectValue placeholder="All profiles" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All profiles</SelectItem>
                {profiles.map((p) => (
                  <SelectItem key={p.profileName} value={p.profileName}>
                    {p.profileDisplayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <StatsBar />
        </div>

        <div className="w-full max-w-2xl flex-1">
          <TabFeed query={query || undefined} profile={profile} />
        </div>
      </div>

    </div>
  );
}
