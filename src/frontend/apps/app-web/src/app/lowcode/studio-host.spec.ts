import { beforeEach, describe, expect, it, vi } from "vitest";
import { createAppWebLowcodeStudioHost } from "./studio-host";

const {
  requestApiMock,
  getAccessTokenMock,
  getTenantIdMock,
  getAuthProfileMock
} = vi.hoisted(() => ({
  requestApiMock: vi.fn(),
  getAccessTokenMock: vi.fn(),
  getTenantIdMock: vi.fn(),
  getAuthProfileMock: vi.fn()
}));

vi.mock("../../services/api-core", () => ({
  requestApi: requestApiMock
}));

vi.mock("@atlas/shared-react-core/utils", () => ({
  getAccessToken: getAccessTokenMock,
  getAuthProfile: getAuthProfileMock,
  getTenantId: getTenantIdMock
}));

describe("createAppWebLowcodeStudioHost", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAccessTokenMock.mockReturnValue("token-1");
    getTenantIdMock.mockReturnValue("tenant-1");
    getAuthProfileMock.mockReturnValue({ id: "user-1" });
  });

  it("auth factories 复用 app-web 现有鉴权存储", () => {
    const host = createAppWebLowcodeStudioHost();

    expect(host.auth.accessTokenFactory()).toBe("token-1");
    expect(host.auth.tenantIdFactory()).toBe("tenant-1");
    expect(host.auth.userIdFactory()).toBe("user-1");
  });

  it("REST 请求经 app-web requestApi 发送并解包 data", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: {
        components: [],
        overrides: []
      }
    });

    const host = createAppWebLowcodeStudioHost();
    const result = await host.api.components.registry("web");

    expect(result).toEqual({ components: [], overrides: [] });
    expect(requestApiMock).toHaveBeenCalledWith(
      "/lowcode/components/registry?renderer=web",
      expect.objectContaining({ method: "GET", body: undefined })
    );
  });
});
