import crypto from "node:crypto";
import type { APIRequestContext, APIResponse } from "@playwright/test";

interface ApiEnvelope<T> {
  success: boolean;
  message?: string;
  code?: string;
  data?: T;
}

type MaybeTokenData = {
  accessToken?: string;
  refreshToken?: string;
  AccessToken?: string;
  RefreshToken?: string;
};

type MaybeProfileData = {
  username?: string;
  displayName?: string;
  permissions?: string[];
  roles?: string[];
};

export interface AuthTokenData {
  accessToken: string;
  refreshToken: string;
}

export interface AuthProfileData {
  username: string;
  displayName?: string;
  permissions: string[];
  roles: string[];
}

export class SeedApiClient {
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private csrfToken: string | null = null;

  constructor(
    private readonly requestContext: APIRequestContext,
    private readonly apiBaseUrl: string,
    private readonly tenantId: string
  ) {}

  async login(username: string, password: string) {
    const response = await this.requestContext.post(`${this.apiBaseUrl}/auth/token`, {
      headers: {
        "Content-Type": "application/json",
        "X-Tenant-Id": this.tenantId
      },
      data: { username, password }
    });

    const envelope = await this.parse<MaybeTokenData>(response, "Login failed");
    const accessToken = envelope.data?.accessToken ?? envelope.data?.AccessToken;
    const refreshToken = envelope.data?.refreshToken ?? envelope.data?.RefreshToken;
    if (!accessToken || !refreshToken) {
      throw new Error("Login response does not contain access tokens");
    }

    this.accessToken = accessToken;
    this.refreshToken = refreshToken;
    return { accessToken, refreshToken } satisfies AuthTokenData;
  }

  async loadProfile() {
    const response = await this.requestContext.get(`${this.apiBaseUrl}/auth/me`, {
      headers: this.createHeaders()
    });

    const envelope = await this.parse<MaybeProfileData>(response, "Load profile failed");
    const profile = {
      username: envelope.data?.username ?? "",
      displayName: envelope.data?.displayName,
      permissions: envelope.data?.permissions ?? [],
      roles: envelope.data?.roles ?? []
    } satisfies AuthProfileData;

    if (!profile.username) {
      throw new Error("Profile response is empty");
    }

    return profile;
  }

  async loadAntiforgeryToken() {
    const response = await this.requestContext.get(`${this.apiBaseUrl}/secure/antiforgery`, {
      headers: this.createHeaders()
    });

    const envelope = await this.parse<{ token?: string; Token?: string }>(response, "Load CSRF token failed");
    this.csrfToken = envelope.data?.token ?? envelope.data?.Token ?? null;
    return this.csrfToken;
  }

  async get<T>(path: string, extraHeaders?: Record<string, string>) {
    const response = await this.requestContext.get(`${this.apiBaseUrl}${path}`, {
      headers: this.createHeaders(extraHeaders)
    });
    return this.parse<T>(response, `GET ${path} failed`);
  }

  async post<T>(path: string, data?: unknown, extraHeaders?: Record<string, string>) {
    await this.ensureWriteHeaders();
    const response = await this.requestContext.post(`${this.apiBaseUrl}${path}`, {
      headers: this.createHeaders(
        {
          ...extraHeaders,
          "Idempotency-Key": crypto.randomUUID(),
          "X-CSRF-TOKEN": this.csrfToken ?? ""
        },
        true
      ),
      data
    });
    return this.parse<T>(response, `POST ${path} failed`);
  }

  async put<T>(path: string, data?: unknown, extraHeaders?: Record<string, string>) {
    await this.ensureWriteHeaders();
    const response = await this.requestContext.put(`${this.apiBaseUrl}${path}`, {
      headers: this.createHeaders(
        {
          ...extraHeaders,
          "Idempotency-Key": crypto.randomUUID(),
          "X-CSRF-TOKEN": this.csrfToken ?? ""
        },
        true
      ),
      data
    });
    return this.parse<T>(response, `PUT ${path} failed`);
  }

  async patch<T>(path: string, data?: unknown, extraHeaders?: Record<string, string>) {
    await this.ensureWriteHeaders();
    const response = await this.requestContext.patch(`${this.apiBaseUrl}${path}`, {
      headers: this.createHeaders(
        {
          ...extraHeaders,
          "Idempotency-Key": crypto.randomUUID(),
          "X-CSRF-TOKEN": this.csrfToken ?? ""
        },
        true
      ),
      data
    });
    return this.parse<T>(response, `PATCH ${path} failed`);
  }

  async delete<T>(path: string, extraHeaders?: Record<string, string>) {
    await this.ensureWriteHeaders();
    const response = await this.requestContext.delete(`${this.apiBaseUrl}${path}`, {
      headers: this.createHeaders({
        ...extraHeaders,
        "Idempotency-Key": crypto.randomUUID(),
        "X-CSRF-TOKEN": this.csrfToken ?? ""
      })
    });
    return this.parse<T>(response, `DELETE ${path} failed`);
  }

  async storageState() {
    return this.requestContext.storageState();
  }

  getAccessToken() {
    return this.accessToken;
  }

  getRefreshToken() {
    return this.refreshToken;
  }

  getCsrfToken() {
    return this.csrfToken;
  }

  private createHeaders(extraHeaders?: Record<string, string>, forceJson = false) {
    const headers: Record<string, string> = {
      "X-Tenant-Id": this.tenantId,
      ...extraHeaders
    };

    if (this.accessToken) {
      headers.Authorization = `Bearer ${this.accessToken}`;
    }

    if (forceJson && !headers["Content-Type"]) {
      headers["Content-Type"] = "application/json";
    }

    return headers;
  }

  private async ensureWriteHeaders() {
    if (!this.accessToken) {
      throw new Error("Client is not logged in");
    }
    if (!this.csrfToken) {
      await this.loadAntiforgeryToken();
    }
  }

  private async parse<T>(response: APIResponse, fallbackMessage: string): Promise<ApiEnvelope<T>> {
    const text = await response.text();
    let envelope: ApiEnvelope<T>;

    try {
      envelope = JSON.parse(text) as ApiEnvelope<T>;
    } catch {
      throw new Error(`${fallbackMessage}: response is not valid JSON`);
    }

    if (!response.ok || envelope.success === false) {
      throw new Error(envelope.message || fallbackMessage);
    }

    return envelope;
  }
}
