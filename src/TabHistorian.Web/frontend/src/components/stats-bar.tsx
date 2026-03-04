"use client";

import { useSnapshots, useProfiles } from "@/lib/hooks";
import { Skeleton } from "@/components/ui/skeleton";

export function StatsBar() {
  const { data: snapshots, isLoading: loadingSnaps } = useSnapshots();
  const { data: profiles, isLoading: loadingProfiles } = useProfiles();

  const totalTabs = snapshots?.reduce((sum, s) => sum + s.tabCount, 0) ?? 0;

  const stats = [
    { label: "Snapshots", value: snapshots?.length ?? 0 },
    { label: "Tabs Tracked", value: totalTabs },
    { label: "Profiles", value: profiles?.length ?? 0 },
  ];

  if (loadingSnaps || loadingProfiles) {
    return (
      <div className="flex gap-6 justify-center">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-12 w-32 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="flex gap-6 justify-center flex-wrap">
      {stats.map((stat) => (
        <div key={stat.label} className="text-center">
          <div className="text-2xl font-bold tabular-nums">
            {stat.value.toLocaleString()}
          </div>
          <div className="text-xs text-muted-foreground uppercase tracking-wider">
            {stat.label}
          </div>
        </div>
      ))}
    </div>
  );
}
