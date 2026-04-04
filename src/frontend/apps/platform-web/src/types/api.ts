export type { ApiResponse, AuthProfile, AuthTokenResult, PagedRequest } from "@atlas/shared-core";

export type LicenseStatusCode = "None" | "Active" | "Expired" | "Invalid";
export type LicenseEdition = "Trial" | "Standard" | "Enterprise" | "Ultimate";

export interface LicenseStatus {
  status: LicenseStatusCode;
  edition: LicenseEdition;
  isPermanent: boolean;
  issuedAt: string | null;
  expiresAt: string | null;
  remainingDays: number | null;
  machineBound: boolean;
  machineMatched: boolean;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  tenantId?: string | null;
  tenantName?: string | null;
}

export interface LicenseActivateResult {
  message: string;
  edition: LicenseEdition;
  isPermanent: boolean;
  expiresAt: string | null;
  remainingDays: number | null;
}

export interface RouterVo {
  alwaysShow?: boolean;
  hidden?: boolean;
  name: string;
  path: string;
  redirect?: string;
  query?: string;
  component?: string;
  meta?: RouterMeta;
  children?: RouterVo[];
}

export interface RouterMeta {
  title?: string;
  titleKey?: string;
  icon?: string;
  noCache?: boolean;
  link?: string;
  permi?: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
  confirmPassword: string;
  captchaKey?: string;
  captchaCode?: string;
}

export interface CaptchaResult {
  captchaKey: string;
  captchaImage: string;
}
