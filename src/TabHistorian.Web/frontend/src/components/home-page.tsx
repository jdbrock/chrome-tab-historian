"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { History } from "lucide-react";
import { SearchBox } from "./search-box";
import { StatsBar } from "./stats-bar";
import { TabFeed } from "./tab-feed";

export function HomePage() {
  const [query, setQuery] = useState("");

  const handleQueryChange = useCallback((q: string) => setQuery(q), []);

  return (
    <div className="min-h-screen flex flex-col">
      <div className="flex-1 flex flex-col items-center px-4 pt-16 sm:pt-24 pb-8">
        <div className="mb-8 text-center">
          <h1 className="text-lg font-medium text-muted-foreground tracking-wide">
            Tab Historian
          </h1>
        </div>

        <div className="w-full max-w-2xl mb-8">
          <SearchBox onQueryChange={handleQueryChange} />
        </div>

        <div className="mb-8">
          <StatsBar />
        </div>

        <div className="w-full max-w-2xl flex-1">
          <TabFeed query={query || undefined} />
        </div>
      </div>

      <footer className="py-4 text-center">
        <Link
          href="/explore"
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          <History className="h-4 w-4" />
          Explore All Data
        </Link>
      </footer>
    </div>
  );
}
