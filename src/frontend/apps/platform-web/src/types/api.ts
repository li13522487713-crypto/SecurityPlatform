export * from "@atlas/shared-core";
export type {
  ApiResponse,
  AuthProfile,
  AuthTokenResult,
  PagedRequest,
  LicenseActivateResult,
  LicenseEdition,
  LicenseFingerprintResult,
  LicenseStatus,
  LicenseStatusCode
} from "@atlas/shared-core";

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
