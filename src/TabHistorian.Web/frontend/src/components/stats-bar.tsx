"use client";

import { useSnapshots } from "@/lib/hooks";

export function StatsBar() {
  const { data: snapshots } = useSnapshots();

  const totalTabs = snapshots?.reduce((sum, s) => sum + s.tabCount, 0) ?? 0;

  if (!snapshots) return null;

  return (
    <div className="text-xs text-muted-foreground">
      {totalTabs.toLocaleString()} tabs tracked
    </div>
  );
}
