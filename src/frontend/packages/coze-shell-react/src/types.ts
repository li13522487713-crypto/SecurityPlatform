import type { ReactNode } from "react";

export interface CozePrimaryNavItem {
  key: string;
  label: string;
  icon: ReactNode;
  path: string;
  activePrefixes?: string[];
  badge?: string;
  testId?: string;
}

export interface CozeSecondaryNavItem {
  key: string;
  label: string;
  path: string;
  icon?: ReactNode;
  badge?: string;
  testId?: string;
}

export interface CozeSecondaryNavSection {
  key: string;
  title: string;
  items: CozeSecondaryNavItem[];
  overflowItems?: CozeSecondaryNavItem[];
  overflowLabel?: string;
  overflowTestId?: string;
}

export interface CozeHeaderAction {
  key: string;
  label: string;
  icon?: ReactNode;
  onClick: () => void;
  testId?: string;
}
