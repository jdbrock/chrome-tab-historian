"use client";

import { useTabEvents } from "@/lib/hooks";
import { formatTimestamp } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import type { TabIdentity } from "@/lib/types";

const eventColors: Record<string, string> = {
  Opened: "bg-green-500/20 text-green-400",
  Closed: "bg-red-500/20 text-red-400",
  Navigated: "bg-blue-500/20 text-blue-400",
  TitleChanged: "bg-yellow-500/20 text-yellow-400",
  Pinned: "bg-purple-500/20 text-purple-400",
  Unpinned: "bg-purple-500/20 text-purple-400",
  Updated: "bg-zinc-500/20 text-zinc-400",
};

interface EventTimelineProps {
  identity: TabIdentity;
}

export function EventTimeline({ identity }: EventTimelineProps) {
  const { data, isLoading } = useTabEvents({
    tabIdentityId: identity.id,
    pageSize: 100,
  });

  if (isLoading) {
    return (
      <div className="space-y-3 p-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  const events = data?.items ?? [];

  if (events.length === 0) {
    return (
      <p className="text-sm text-muted-foreground p-4">No events recorded.</p>
    );
  }

  return (
    <div className="space-y-1">
      {events.map((event) => (
        <div
          key={event.id}
          className="flex items-start gap-3 px-4 py-2 hover:bg-accent/30 rounded-md"
        >
          <div className="mt-0.5 w-2 h-2 rounded-full bg-muted-foreground shrink-0" />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <Badge
                className={`text-xs font-normal ${eventColors[event.eventType] ?? ""}`}
              >
                {event.eventType}
              </Badge>
              <span className="text-xs text-muted-foreground">
                {formatTimestamp(event.timestamp)}
              </span>
            </div>
            {event.url && (
              <p className="text-xs text-muted-foreground truncate mt-1">
                {event.url}
              </p>
            )}
            {event.title && (
              <p className="text-xs truncate mt-0.5">{event.title}</p>
            )}
            {event.stateDelta && (
              <div className="mt-1.5 text-xs text-muted-foreground bg-muted/50 rounded p-2 font-mono overflow-x-auto">
                {Object.entries(event.stateDelta).map(([key, val]) => (
                  <div key={key}>
                    <span className="text-blue-400">{key}</span>:{" "}
                    {typeof val === "string" ? (
                      <span className="text-green-400 break-all">
                        {val.length > 200 ? val.slice(0, 200) + "..." : val}
                      </span>
                    ) : (
                      <span className="text-yellow-400">
                        {JSON.stringify(val)}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
