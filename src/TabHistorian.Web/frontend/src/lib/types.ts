export interface Snapshot {
  id: number;
  timestamp: string;
  windowCount: number;
  tabCount: number;
}

export interface Profile {
  profileName: string;
  profileDisplayName: string;
}

export interface NavigationEntry {
  url: string;
  title: string;
  timestamp?: string;
  httpStatusCode: number;
  referrer?: string;
  originalRequestUrl?: string;
  transitionType?: number;
  hasPostData: boolean;
}

export interface Tab {
  snapshotId: number;
  snapshotTimestamp: string;
  windowId: number;
  profileName: string;
  profileDisplayName: string;
  windowIndex: number;
  tabId: number;
  tabIndex: number;
  currentUrl: string;
  title: string;
  pinned: boolean;
  lastActiveTime: string | null;
  navigationHistory: NavigationEntry[];
}

export interface TabsResponse {
  items: Tab[];
  page: number;
  pageSize: number;
  totalCount: number;
}
