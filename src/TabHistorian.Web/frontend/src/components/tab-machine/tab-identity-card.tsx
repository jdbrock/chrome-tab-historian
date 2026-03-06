"use client";

import { useState } from "react";
import { ExternalLink, Clock, ChevronDown, ChevronRight } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { faviconUrl, domainFromUrl, formatTimestamp } from "@/lib/utils";
import { EventTimeline } from "./event-timeline";
import type { TabIdentity } from "@/lib/types";

interface TabIdentityCardProps {
  identity: TabIdentity;
}

export function TabIdentityCard({ identity }: TabIdentityCardProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Card className="border-border/50 overflow-hidden">
      <div
        className="p-4 cursor-pointer hover:bg-accent/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-start gap-3">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={faviconUrl(identity.lastUrl)}
            alt=""
            width={20}
            height={20}
            className="mt-0.5 rounded-sm shrink-0"
          />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <span className="font-medium truncate text-sm">
                {identity.lastTitle || identity.firstTitle || "Untitled"}
              </span>
              {identity.isOpen ? (
                <Badge
                  variant="secondary"
                  className="text-xs font-normal bg-green-500/20 text-green-400"
                >
                  Open
                </Badge>
              ) : (
                <Badge
                  variant="secondary"
                  className="text-xs font-normal bg-zinc-500/20 text-zinc-400"
                >
                  Closed
                </Badge>
              )}
            </div>
            <div className="text-xs text-muted-foreground truncate mt-0.5">
              {domainFromUrl(identity.lastUrl)}
            </div>
            <div className="flex items-center gap-3 mt-2 flex-wrap">
              <Badge variant="secondary" className="text-xs font-normal">
                {identity.profileName}
              </Badge>
              <span className="text-xs text-muted-foreground flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {formatTimestamp(identity.lastSeen)}
              </span>
              <span className="text-xs text-muted-foreground">
                {identity.eventCount} event{identity.eventCount !== 1 ? "s" : ""}
              </span>
              {identity.firstUrl !== identity.lastUrl && (
                <span className="text-xs text-muted-foreground truncate max-w-48">
                  from {domainFromUrl(identity.firstUrl)}
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
              <ExternalLink className="h-4 w-4" />
            </a>
            {expanded ? (
              <ChevronDown className="h-4 w-4 text-muted-foreground" />
            ) : (
              <ChevronRight className="h-4 w-4 text-muted-foreground" />
            )}
          </div>
        </div>
      </div>
      {expanded && (
        <div className="border-t border-border/50">
          <EventTimeline identity={identity} />
        </div>
      )}
    </Card>
  );
}
