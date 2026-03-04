"use client";

import { useState } from "react";
import { ChevronRight, Camera } from "lucide-react";
import { cn, formatTimestamp, type GroupedSnapshot } from "@/lib/utils";
import { ProfileSection } from "./profile-section";

interface SnapshotSectionProps {
  snapshot: GroupedSnapshot;
}

export function SnapshotSection({ snapshot }: SnapshotSectionProps) {
  const [open, setOpen] = useState(false);
  const profileCount = snapshot.profiles.size;
  const tabCount = Array.from(snapshot.profiles.values()).reduce(
    (sum, p) =>
      sum +
      Array.from(p.windows.values()).reduce((s, w) => s + w.tabs.length, 0),
    0
  );

  return (
    <div className="border border-border/50 rounded-lg">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center gap-3 p-3 hover:bg-accent/50 transition-colors rounded-lg text-left"
      >
        <ChevronRight
          className={cn(
            "h-4 w-4 text-muted-foreground transition-transform",
            open && "rotate-90"
          )}
        />
        <Camera className="h-4 w-4 text-muted-foreground" />
        <span className="font-medium text-sm">
          {formatTimestamp(snapshot.snapshotTimestamp)}
        </span>
        <span className="text-xs text-muted-foreground ml-auto">
          {profileCount} profile{profileCount !== 1 ? "s" : ""}, {tabCount} tab
          {tabCount !== 1 ? "s" : ""}
        </span>
      </button>
      {open && (
        <div className="pl-6 pr-3 pb-3 space-y-2">
          {Array.from(snapshot.profiles.values()).map((profile) => (
            <ProfileSection
              key={profile.profileName}
              profile={profile}
            />
          ))}
        </div>
      )}
    </div>
  );
}
