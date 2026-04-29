import { describe, expect, it, vi } from "vitest";

import { MicroflowApiClient } from "./microflow-api-client";
import { MicroflowApiException } from "./microflow-api-error";

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", "X-Trace-Id": "trace-http" },
  });
}

describe("MicroflowApiClient error envelope", () => {
  it.each([
    [401, "MICROFLOW_UNAUTHORIZED", "auth"],
    [403, "MICROFLOW_PERMISSION_DENIED", "permission"],
    [404, "MICROFLOW_NOT_FOUND", "notFound"],
    [409, "MICROFLOW_VERSION_CONFLICT", "conflict"],
    [422, "MICROFLOW_VALIDATION_FAILED", "validation"],
    [500, "MICROFLOW_SERVICE_UNAVAILABLE", "server"],
  ] as const)("maps HTTP %s to MicroflowApiError envelope", async (status, code, category) => {
    const onUnauthorized = vi.fn();
    const onForbidden = vi.fn();
    const onApiError = vi.fn();
    const client = new MicroflowApiClient({
      apiBaseUrl: "/api/v1",
      fetchImpl: vi.fn().mockResolvedValue(jsonResponse(status, {
        success: false,
        traceId: "trace-envelope",
        error: {
          code,
          message: `error-${status}`,
          httpStatus: status,
          details: status === 409 ? JSON.stringify({ remoteVersion: "0.2.0" }) : undefined,
        },
      })) as unknown as typeof fetch,
      onUnauthorized,
      onForbidden,
      onApiError,
    });

    await expect(client.get("/microflows/mf-1")).rejects.toBeInstanceOf(MicroflowApiException);
    const error = onApiError.mock.calls[0]?.[0];
    expect(error).toMatchObject({ code, category, httpStatus: status, traceId: "trace-envelope" });
    if (status === 401) {
      expect(onUnauthorized).toHaveBeenCalledTimes(1);
    }
    if (status === 403) {
      expect(onForbidden).toHaveBeenCalledTimes(1);
    }
  });
});
