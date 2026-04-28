import { HttpResponse, type HttpHandler, http } from "msw";

import { mockErrorResponse } from "./mock-error-handlers";

export type MockResolver = (input: { request: Request; params: Record<string, string> }) => Promise<HttpResponse> | HttpResponse;

function pathAliases(path: string): string[] {
  return [path, `/api${path}`];
}

function normalizeParams(params: Record<string, string | readonly string[] | undefined>): Record<string, string> {
  return Object.fromEntries(Object.entries(params).map(([key, value]) => [key, Array.isArray(value) ? value[0] ?? "" : value ?? ""]));
}

function route(method: "get" | "post" | "put" | "patch" | "delete", path: string, resolver: MockResolver): HttpHandler[] {
  return pathAliases(path).map(alias => http[method](alias, async ({ request, params }) => {
    const forcedError = mockErrorResponse(request);
    if (forcedError) {
      return forcedError;
    }
    return resolver({ request, params: normalizeParams(params) });
  }));
}

export function mockGet(path: string, resolver: MockResolver): HttpHandler[] {
  return route("get", path, resolver);
}

export function mockPost(path: string, resolver: MockResolver): HttpHandler[] {
  return route("post", path, resolver);
}

export function mockPut(path: string, resolver: MockResolver): HttpHandler[] {
  return route("put", path, resolver);
}

export function mockPatch(path: string, resolver: MockResolver): HttpHandler[] {
  return route("patch", path, resolver);
}

export function mockDelete(path: string, resolver: MockResolver): HttpHandler[] {
  return route("delete", path, resolver);
}
