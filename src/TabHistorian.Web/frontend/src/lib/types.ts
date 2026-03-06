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

export interface TabMachineProfile {
  profileName: string;
  displayName: string | null;
}

// Tab Machine types
export interface TabIdentity {
  id: number;
  profileName: string;
  profileDisplayName: string | null;
  firstUrl: string;
  firstTitle: string;
  firstSeen: string;
  lastUrl: string;
  lastTitle: string;
  lastSeen: string;
  lastActiveTime: string | null;
  firstActiveTime: string | null;
  lastNavigated: string | null;
  eventCount: number;
  isOpen: boolean;
  windowIndex: number;
  tabIndex: number;
}

export interface TabIdentityResponse {
  items: TabIdentity[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TabEvent {
  id: number;
  tabIdentityId: number;
  eventType: string;
  timestamp: string;
  stateDelta: string | null;
  url: string | null;
  title: string | null;
  profileName: string | null;
}

export interface TabEventsResponse {
  items: TabEvent[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TabMachineStats {
  totalTabs: number;
  openTabs: number;
  closedTabs: number;
  totalEvents: number;
  firstSeen: string | null;
  lastSeen: string | null;
}

export interface TimelineTab {
  tabIdentityId: number;
  currentUrl: string;
  title: string;
  pinned: boolean;
  lastActiveTime: string | null;
  tabIndex: number;
  windowIndex: number;
  profileName: string;
  profileDisplayName: string | null;
  navigationHistory: NavigationEntry[] | null;
  isOpen: boolean;
}
