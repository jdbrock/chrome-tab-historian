"use client";

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { ExternalLink, Pin, Clock, Globe } from "lucide-react";
import { faviconUrl, formatTimestamp, domainFromUrl } from "@/lib/utils";
import type { Tab } from "@/lib/types";

interface TabDetailDialogProps {
  tab: Tab;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function TabDetailDialog({
  tab,
  open,
  onOpenChange,
}: TabDetailDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-3">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={faviconUrl(tab.currentUrl)}
              alt=""
              width={24}
              height={24}
              className="rounded-sm shrink-0"
            />
            <span className="truncate">{tab.title || "Untitled"}</span>
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 flex-1 min-h-0">
          <div className="space-y-2">
            <a
              href={tab.currentUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm text-blue-400 hover:text-blue-300 flex items-center gap-1 break-all"
            >
              {tab.currentUrl}
              <ExternalLink className="h-3 w-3 shrink-0" />
            </a>

            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">{tab.profileDisplayName}</Badge>
              <Badge variant="outline">
                Window {tab.windowIndex + 1}, Tab {tab.tabIndex + 1}
              </Badge>
              {tab.pinned && (
                <Badge variant="outline">
                  <Pin className="h-3 w-3 mr-1" />
                  Pinned
                </Badge>
              )}
            </div>

            <div className="flex gap-4 text-xs text-muted-foreground">
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                Snapshot: {formatTimestamp(tab.snapshotTimestamp)}
              </span>
              {tab.lastActiveTime && (
                <span className="flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  Last active: {formatTimestamp(tab.lastActiveTime)}
                </span>
              )}
            </div>
          </div>

          {tab.navigationHistory.length > 0 && (
            <>
              <Separator />
              <div>
                <h3 className="text-sm font-medium mb-3 flex items-center gap-2">
                  <Globe className="h-4 w-4" />
                  Navigation History ({tab.navigationHistory.length})
                </h3>
                <ScrollArea className="max-h-[40vh]">
                  <div className="space-y-2 pr-4">
                    {tab.navigationHistory.map((entry, i) => (
                      <div
                        key={i}
                        className="text-sm border-l-2 border-border pl-3 py-1"
                      >
                        <a
                          href={entry.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-blue-400 hover:text-blue-300 break-all text-xs"
                        >
                          {entry.title || domainFromUrl(entry.url)}
                        </a>
                        <div className="text-xs text-muted-foreground mt-0.5 flex gap-3">
                          <span>{domainFromUrl(entry.url)}</span>
                          {entry.httpStatusCode > 0 && (
                            <span>HTTP {entry.httpStatusCode}</span>
                          )}
                          {entry.timestamp && (
                            <span>{formatTimestamp(entry.timestamp)}</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </ScrollArea>
              </div>
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
