"use client";

import { useMemo, useState } from "react";
import { CalendarIcon, ExternalLink } from "lucide-react";
import { useTimeline, useTabMachineProfiles, useTabMachineStats } from "@/lib/hooks";
import { faviconUrl, domainFromUrl, cleanTitle } from "@/lib/utils";
import type { TimelineTab } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Slider } from "@/components/ui/slider";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export function TimeTravel() {
  const [selectedDate, setSelectedDate] = useState<Date>(new Date());
  const [timeMinutes, setTimeMinutes] = useState(
    new Date().getHours() * 60 + new Date().getMinutes()
  );
  const [profile, setProfile] = useState<string | undefined>(undefined);
  const { data: profiles } = useTabMachineProfiles();
  const { data: stats } = useTabMachineStats();

  const timestamp = useMemo(() => {
    const d = new Date(selectedDate);
    d.setHours(Math.floor(timeMinutes / 60), timeMinutes % 60, 0, 0);
    return d.toISOString();
  }, [selectedDate, timeMinutes]);

  const { data: tabs, isLoading } = useTimeline(timestamp, profile);

  const hours = Math.floor(timeMinutes / 60);
  const minutes = timeMinutes % 60;
  const timeLabel = `${hours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}`;

  // Group by profile → window
  const grouped = useMemo(() => {
    if (!tabs) return new Map<string, Map<number, TimelineTab[]>>();
    const m = new Map<string, Map<number, TimelineTab[]>>();
    for (const tab of tabs) {
      const pName = tab.profileDisplayName ?? tab.profileName;
      if (!m.has(pName)) m.set(pName, new Map<number, TimelineTab[]>());
      const windows = m.get(pName)!;
      if (!windows.has(tab.windowIndex)) windows.set(tab.windowIndex, [] as TimelineTab[]);
      windows.get(tab.windowIndex)!.push(tab);
    }
    return m;
  }, [tabs]);

  // Determine date range from stats
  const minDate = stats?.firstSeen ? new Date(stats.firstSeen) : undefined;

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="outline" size="sm" className="gap-2">
              <CalendarIcon className="h-4 w-4" />
              {selectedDate.toLocaleDateString(undefined, {
                month: "short",
                day: "numeric",
                year: "numeric",
              })}
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-auto p-0" align="start">
            <Calendar
              mode="single"
              selected={selectedDate}
              onSelect={(d) => d && setSelectedDate(d)}
              disabled={(d) =>
                d > new Date() || (minDate ? d < minDate : false)
              }
            />
          </PopoverContent>
        </Popover>

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
                <SelectItem key={p.profileName} value={p.profileName}>
                  {p.displayName ?? p.profileName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        <span className="text-sm font-mono text-muted-foreground">
          {timeLabel}
        </span>

        {tabs && (
          <span className="text-xs text-muted-foreground">
            {tabs.length.toLocaleString()} tab{tabs.length !== 1 ? "s" : ""}
          </span>
        )}
      </div>

      <div className="px-1">
        <Slider
          value={[timeMinutes]}
          onValueChange={([v]) => setTimeMinutes(v)}
          min={0}
          max={1439}
          step={1}
        />
        <div className="flex justify-between text-xs text-muted-foreground mt-1">
          <span>00:00</span>
          <span>12:00</span>
          <span>23:59</span>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      ) : tabs && tabs.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-8">
          No tabs were open at this time.
        </p>
      ) : (
        <div className="space-y-4">
          {[...grouped.entries()].map(([profileName, windows]) => (
            <div key={profileName}>
              <h3 className="text-sm font-medium text-muted-foreground mb-2">
                {profileName}{" "}
                <span className="text-xs">
                  ({[...windows.values()].reduce((s, t) => s + t.length, 0)} tabs
                  in {windows.size} window{windows.size !== 1 ? "s" : ""})
                </span>
              </h3>
              {[...windows.entries()].map(([windowIndex, windowTabs]) => (
                <div key={windowIndex} className="mb-3">
                  <div className="text-xs text-muted-foreground mb-1 pl-1">
                    Window {windowIndex} ({windowTabs.length} tab
                    {windowTabs.length !== 1 ? "s" : ""})
                  </div>
                  <div className="space-y-1">
                    {windowTabs.map((tab) => (
                      <Card
                        key={tab.tabIdentityId}
                        className="p-3 border-border/50"
                      >
                        <div className="flex items-center gap-2">
                          {/* eslint-disable-next-line @next/next/no-img-element */}
                          <img
                            src={faviconUrl(tab.currentUrl)}
                            alt=""
                            width={16}
                            height={16}
                            className="rounded-sm shrink-0"
                          />
                          <a
                            href={tab.currentUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-sm truncate flex-1 hover:underline"
                          >
                            {cleanTitle(tab.title || "Untitled")}
                          </a>
                          {tab.pinned && (
                            <Badge
                              variant="secondary"
                              className="text-xs font-normal"
                            >
                              Pinned
                            </Badge>
                          )}
                          <span className="text-xs text-muted-foreground truncate max-w-48">
                            {domainFromUrl(tab.currentUrl)}
                          </span>
                          <a
                            href={tab.currentUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-muted-foreground hover:text-foreground shrink-0"
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                          </a>
                        </div>
                      </Card>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
