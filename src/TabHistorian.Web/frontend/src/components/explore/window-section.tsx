"use client";

import { useState } from "react";
import { ChevronRight, AppWindow } from "lucide-react";
import { cn, type GroupedWindow } from "@/lib/utils";
import { TabCard } from "../tab-card";

interface WindowSectionProps {
  window: GroupedWindow;
}

export function WindowSection({ window }: WindowSectionProps) {
  const [open, setOpen] = useState(false);

  return (
    <div className="border border-border/20 rounded-md">
      <button
        onClick={() => setOpen(!open)}
        className="w-full flex items-center gap-3 p-2 hover:bg-accent/20 transition-colors rounded-md text-left"
      >
        <ChevronRight
          className={cn(
            "h-3 w-3 text-muted-foreground transition-transform",
            open && "rotate-90"
          )}
        />
        <AppWindow className="h-3.5 w-3.5 text-muted-foreground" />
        <span className="text-sm">Window {window.windowIndex + 1}</span>
        <span className="text-xs text-muted-foreground ml-auto">
          {window.tabs.length} tab{window.tabs.length !== 1 ? "s" : ""}
        </span>
      </button>
      {open && (
        <div className="pl-4 pr-2 pb-2 space-y-1">
          {window.tabs.map((tab) => (
            <TabCard key={tab.tabId} tab={tab} />
          ))}
        </div>
      )}
    </div>
  );
}
