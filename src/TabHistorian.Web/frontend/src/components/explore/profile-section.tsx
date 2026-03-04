"use client";

import { useState } from "react";
import { ChevronRight, User } from "lucide-react";
import { cn, type GroupedProfile } from "@/lib/utils";
import { WindowSection } from "./window-section";

interface ProfileSectionProps {
  profile: GroupedProfile;
}

export function ProfileSection({ profile }: ProfileSectionProps) {
  const [open, setOpen] = useState(false);
  const windowCount = profile.windows.size;
  const tabCount = Array.from(profile.windows.values()).reduce(
    (sum, w) => sum + w.tabs.length,
    0
  );

  return (
    <div className="border border-border/30 rounded-md">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center gap-3 p-2.5 hover:bg-accent/30 transition-colors rounded-md text-left"
      >
        <ChevronRight
          className={cn(
            "h-3.5 w-3.5 text-muted-foreground transition-transform",
            open && "rotate-90"
          )}
        />
        <User className="h-3.5 w-3.5 text-muted-foreground" />
        <span className="text-sm">{profile.profileDisplayName}</span>
        <span className="text-xs text-muted-foreground ml-auto">
          {windowCount} window{windowCount !== 1 ? "s" : ""}, {tabCount} tab
          {tabCount !== 1 ? "s" : ""}
        </span>
      </button>
      {open && (
        <div className="pl-6 pr-2 pb-2 space-y-2">
          {Array.from(profile.windows.values()).map((window) => (
            <WindowSection key={window.windowId} window={window} />
          ))}
        </div>
      )}
    </div>
  );
}
