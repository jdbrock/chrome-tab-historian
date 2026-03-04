"use client";

import { useState } from "react";
import { Pin, ExternalLink } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { faviconUrl, domainFromUrl, formatTimestamp } from "@/lib/utils";
import { TabDetailDialog } from "./tab-detail-dialog";
import type { Tab } from "@/lib/types";

interface TabCardProps {
  tab: Tab;
}

export function TabCard({ tab }: TabCardProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Card
        className="p-4 cursor-pointer hover:bg-accent/50 transition-colors border-border/50"
        onClick={() => setOpen(true)}
      >
        <div className="flex items-start gap-3">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={faviconUrl(tab.currentUrl)}
            alt=""
            width={20}
            height={20}
            className="mt-0.5 rounded-sm shrink-0"
          />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <span className="font-medium truncate text-sm">
                {tab.title || "Untitled"}
              </span>
              {tab.pinned && (
                <Pin className="h-3 w-3 text-muted-foreground shrink-0" />
              )}
            </div>
            <div className="text-xs text-muted-foreground truncate mt-0.5">
              {domainFromUrl(tab.currentUrl)}
            </div>
            <div className="flex items-center gap-2 mt-2">
              <Badge variant="secondary" className="text-xs font-normal">
                {tab.profileDisplayName}
              </Badge>
              <span className="text-xs text-muted-foreground">
                {formatTimestamp(tab.snapshotTimestamp)}
              </span>
              {tab.navigationHistory.length > 1 && (
                <span className="text-xs text-muted-foreground">
                  {tab.navigationHistory.length} pages
                </span>
              )}
            </div>
          </div>
          <a
            href={tab.currentUrl}
            target="_blank"
            rel="noopener noreferrer"
            onClick={(e) => e.stopPropagation()}
            className="text-muted-foreground hover:text-foreground shrink-0"
          >
            <ExternalLink className="h-4 w-4" />
          </a>
        </div>
      </Card>
      <TabDetailDialog tab={tab} open={open} onOpenChange={setOpen} />
    </>
  );
}
