type ClientType = "WebH5" | "Mobile" | "Backend";
type ClientPlatform = "Web" | "Android" | "iOS";
type ClientChannel = "Browser" | "App";
type ClientAgent = "Chrome" | "Edge" | "Safari" | "Firefox" | "Other";

interface ClientContextHeaders {
  "X-Client-Type": ClientType;
  "X-Client-Platform": ClientPlatform;
  "X-Client-Channel": ClientChannel;
  "X-Client-Agent": ClientAgent;
}

function detectPlatform(userAgent: string): ClientPlatform {
  const ua = userAgent.toLowerCase();
  if (ua.includes("android")) {
    return "Android";
  }
  if (ua.includes("iphone") || ua.includes("ipad") || ua.includes("ipod")) {
    return "iOS";
  }
  return "Web";
}

function detectAgent(userAgent: string): ClientAgent {
  const ua = userAgent.toLowerCase();
  if (ua.includes("edg/")) {
    return "Edge";
  }
  if (ua.includes("chrome/") && !ua.includes("edg/")) {
    return "Chrome";
  }
  if (ua.includes("firefox/")) {
    return "Firefox";
  }
  if (ua.includes("safari/") && !ua.includes("chrome/")) {
    return "Safari";
  }
  return "Other";
}

export function getClientContextHeaders(): ClientContextHeaders {
  const userAgent = typeof navigator === "undefined" ? "" : navigator.userAgent;
  const platform = detectPlatform(userAgent);
  const agent = detectAgent(userAgent);

  return {
    "X-Client-Type": "WebH5",
    "X-Client-Platform": platform,
    "X-Client-Channel": "Browser",
    "X-Client-Agent": agent
  };
}
