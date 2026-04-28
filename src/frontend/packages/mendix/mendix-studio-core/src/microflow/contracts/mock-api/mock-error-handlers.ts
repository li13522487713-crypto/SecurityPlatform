import { HttpResponse, type HttpHandler, http } from "msw";

import { forbidden, notFound, publishBlocked, referenceBlocked, serviceUnavailable, unauthorized, validationFailed, versionConflict } from "./mock-api-response";
import type { MicroflowMockErrorScenario } from "./mock-api-types";

const ERROR_HEADER = "x-microflow-mock-error";
const ERROR_QUERY = "mockError";

export function getMockErrorScenario(request: Request): MicroflowMockErrorScenario | undefined {
  const header = request.headers.get(ERROR_HEADER);
  const query = new URL(request.url).searchParams.get(ERROR_QUERY);
  const value = header ?? query;
  switch (value) {
    case "unauthorized":
    case "forbidden":
    case "not-found":
    case "version-conflict":
    case "validation-failed":
    case "publish-blocked":
    case "reference-blocked":
    case "service-unavailable":
    case "network":
      return value;
    default:
      return undefined;
  }
}

export function mockErrorResponse(request: Request): HttpResponse | undefined {
  const scenario = getMockErrorScenario(request);
  if (!scenario) {
    return undefined;
  }
  if (scenario === "network") {
    return HttpResponse.error();
  }
  if (scenario === "unauthorized") {
    return HttpResponse.json(unauthorized("Mocked 401 unauthorized."), { status: 401 });
  }
  if (scenario === "forbidden") {
    return HttpResponse.json(forbidden("Mocked 403 forbidden."), { status: 403 });
  }
  if (scenario === "not-found") {
    return HttpResponse.json(notFound("Mocked 404 not found."), { status: 404 });
  }
  if (scenario === "version-conflict") {
    return HttpResponse.json(versionConflict("Mocked 409 version conflict."), { status: 409 });
  }
  if (scenario === "validation-failed") {
    return HttpResponse.json(validationFailed([], "Mocked 422 validation failed."), { status: 422 });
  }
  if (scenario === "publish-blocked") {
    return HttpResponse.json(publishBlocked({ message: "Mocked publish blocked." }), { status: 422 });
  }
  if (scenario === "reference-blocked") {
    return HttpResponse.json(referenceBlocked("Mocked reference blocked."), { status: 422 });
  }
  return HttpResponse.json(serviceUnavailable("Mocked 500 service unavailable."), { status: 500 });
}

export function withMockApiError<T>(request: Request, compute: () => T): T | HttpResponse {
  const error = mockErrorResponse(request);
  return error ?? compute();
}

export function createMicroflowMockErrorHandlers(): HttpHandler[] {
  return [
    http.all("*/api/microflow-mock/error/:scenario", ({ params }) => {
      const scenario = String(params.scenario);
      const request = new Request(`http://mock.local/api/microflow-mock/error?${ERROR_QUERY}=${encodeURIComponent(scenario)}`);
      return mockErrorResponse(request) ?? HttpResponse.json(serviceUnavailable("Unknown mock error scenario."), { status: 500 });
    }),
  ];
}
