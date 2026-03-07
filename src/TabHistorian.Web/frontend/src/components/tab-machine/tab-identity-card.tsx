"use client";

import { useMemo, useState } from "react";
import { ExternalLink, Clock, ChevronDown, ChevronRight, History } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { faviconUrl, domainFromUrl, formatTimestamp, cleanTitle } from "@/lib/utils";
import { useTabCurrentState } from "@/lib/hooks";
import { EventTimeline } from "./event-timeline";
import type { TabIdentity, NavigationEntry } from "@/lib/types";

function mostRecentTime(identity: TabIdentity): string {
  if (identity.lastNavigated && identity.firstSeen)
    return identity.lastNavigated > identity.firstSeen ? identity.lastNavigated : identity.firstSeen;
  return identity.lastNavigated ?? identity.firstSeen;
}

interface TabIdentityCardProps {
  identity: TabIdentity;
  searchQuery?: string;
}

export function TabIdentityCard({ identity, searchQuery }: TabIdentityCardProps) {
  const [expanded, setExpanded] = useState(false);
  const { data: currentState, isLoading } = useTabCurrentState(
    expanded ? identity.id : undefined
  );

  const navHistory: NavigationEntry[] = expanded && currentState?.navigationHistory
    ? (typeof currentState.navigationHistory === "string"
        ? JSON.parse(currentState.navigationHistory)
        : currentState.navigationHistory) ?? []
    : [];

  // When searching, find nav history entries that match but title/url don't
  const navMatchEntries = useMemo(() => {
    if (!searchQuery || !identity.navigationHistory) return [];
    const q = searchQuery.toLowerCase();
    // Check if the match is already visible in the card's title/url fields
    const visibleMatch =
      identity.lastUrl.toLowerCase().includes(q) ||
      identity.lastTitle.toLowerCase().includes(q) ||
      identity.firstUrl.toLowerCase().includes(q) ||
      identity.firstTitle.toLowerCase().includes(q);
    if (visibleMatch) return [];
    // Parse nav history and filter to matching entries
    try {
      const entries: NavigationEntry[] = JSON.parse(identity.navigationHistory);
      return entries.filter(
        (e) => e.url.toLowerCase().includes(q) || (e.title && e.title.toLowerCase().includes(q))
      );
    } catch { return []; }
  }, [searchQuery, identity.navigationHistory, identity.lastUrl, identity.lastTitle, identity.firstUrl, identity.firstTitle]);

  return (
    <Card className="border-border/50 overflow-hidden py-0 gap-0">
      <div
        className="px-3 py-1 cursor-pointer hover:bg-accent/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2.5">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={faviconUrl(identity.lastUrl)}
            alt=""
            width={16}
            height={16}
            className="rounded-sm shrink-0"
          />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <span className="font-medium truncate text-sm text-foreground/75">
                {cleanTitle(identity.lastTitle || identity.firstTitle || "Untitled")}
              </span>
              <span className="text-xs text-muted-foreground/40 shrink-0">
                {domainFromUrl(identity.lastUrl)}
              </span>
              {!identity.isOpen && (
                <Badge
                  variant="secondary"
                  className="text-[10px] leading-none py-0.5 px-1.5 font-normal bg-zinc-500/20 text-zinc-400 shrink-0"
                >
                  Closed
                </Badge>
              )}
            </div>
            <div className="flex items-center gap-3 text-[11px] text-muted-foreground/60">
              <span>{identity.profileDisplayName ?? identity.profileName}</span>
              <span className="flex items-center gap-1">
                <Clock className="h-2.5 w-2.5" />
                {formatTimestamp(mostRecentTime(identity))}
              </span>
              {identity.firstUrl !== identity.lastUrl && (
                <span className="truncate max-w-36">
                  <span className="text-muted-foreground/50">From</span>{" "}
                  {domainFromUrl(identity.firstUrl)}
                </span>
              )}
            </div>
          </div>
          <div className="flex items-center gap-1 shrink-0">
            <a
              href={identity.lastUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => e.stopPropagation()}
              className="text-muted-foreground hover:text-foreground"
            >
              <ExternalLink className="h-3.5 w-3.5" />
            </a>
            {expanded ? (
              <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            ) : (
              <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
            )}
          </div>
        </div>
      </div>
      {navMatchEntries.length > 0 && !expanded && (
        <div className="border-t border-border/30 px-3 py-1.5">
          <div className="flex items-center gap-1.5 mb-1">
            <History className="h-3 w-3 text-muted-foreground/50" />
            <span className="text-[11px] text-muted-foreground/50 uppercase tracking-wider font-medium">
              Matched in history
            </span>
          </div>
          <div className="space-y-0.5">
            {navMatchEntries.map((entry, i) => (
              <div key={i} className="flex items-center gap-2 py-0.5 text-sm">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={faviconUrl(entry.url)}
                  alt=""
                  width={12}
                  height={12}
                  className="rounded-sm shrink-0"
                />
                <span className="truncate text-foreground/60">
                  {cleanTitle(entry.title) || domainFromUrl(entry.url)}
                </span>
                <span className="text-[11px] text-muted-foreground/40 truncate shrink-0 max-w-48">
                  {domainFromUrl(entry.url)}
                </span>
                <a
                  href={entry.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={(e) => e.stopPropagation()}
                  className="text-muted-foreground/40 hover:text-foreground shrink-0"
                >
                  <ExternalLink className="h-3 w-3" />
                </a>
              </div>
            ))}
          </div>
        </div>
      )}
      {expanded && (
        <div className="border-t border-border/50">
          {isLoading ? (
            <div className="p-4 space-y-2">
              <Skeleton className="h-6 w-full" />
              <Skeleton className="h-6 w-3/4" />
            </div>
          ) : (
            <>
              <NavigationHistorySection entries={navHistory} />
              <EventsSection identity={identity} />
            </>
          )}
        </div>
      )}
    </Card>
  );
}

function NavigationHistorySection({ entries }: { entries: NavigationEntry[] }) {
  if (entries.length === 0) return null;

  // Show in reverse chronological order (most recent first)
  const reversed = [...entries].reverse();

  return (
    <div className="px-4 pt-3 pb-2">
      <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
        Navigation History
      </h4>
      <div className="space-y-1">
        {reversed.map((entry, i) => (
          <div key={i} className="flex items-start gap-2 py-1">
            <div className="mt-1 w-1.5 h-1.5 rounded-full bg-muted-foreground/40 shrink-0" />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={faviconUrl(entry.url)}
                  alt=""
                  width={12}
                  height={12}
                  className="rounded-sm shrink-0"
                />
                <span className="text-sm truncate">
                  {cleanTitle(entry.title) || domainFromUrl(entry.url)}
                </span>
                <a
                  href={entry.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={(e) => e.stopPropagation()}
                  className="text-muted-foreground hover:text-foreground shrink-0"
                >
                  <ExternalLink className="h-3 w-3" />
                </a>
              </div>
              <div className="flex items-center gap-2 text-[11px] text-muted-foreground/60 pl-[20px]">
                <span className="truncate">{entry.url}</span>
                {entry.timestamp && (
                  <span className="shrink-0">{formatTimestamp(entry.timestamp)}</span>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function EventsSection({ identity }: { identity: TabIdentity }) {
  const [showEvents, setShowEvents] = useState(false);

  return (
    <div className="border-t border-border/30">
      <button
        className="flex items-center gap-1.5 px-4 py-2 text-xs text-muted-foreground hover:text-foreground transition-colors w-full"
        onClick={(e) => { e.stopPropagation(); setShowEvents(!showEvents); }}
      >
        {showEvents ? (
          <ChevronDown className="h-3 w-3" />
        ) : (
          <ChevronRight className="h-3 w-3" />
        )}
        <span className="uppercase tracking-wider font-medium">Events</span>
        {identity.eventCount > 0 && (
          <span className="text-muted-foreground/50">{identity.eventCount}</span>
        )}
      </button>
      {showEvents && <EventTimeline identity={identity} />}
    </div>
  );
}
