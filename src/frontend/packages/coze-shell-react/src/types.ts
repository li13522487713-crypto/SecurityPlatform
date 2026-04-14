import type { ReactNode } from "react";

export interface CozeNavItem {
  key: string;
  label: string;
  path: string;
  icon?: ReactNode;
  badge?: string;
  testId?: string;
}

export interface CozeNavSection {
  key: string;
  title: string;
  items: CozeNavItem[];
  overflowItems?: CozeNavItem[];
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
