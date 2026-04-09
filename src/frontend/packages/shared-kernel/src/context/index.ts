export type ClientType = "WebH5" | "Mobile" | "Backend";
export type ClientPlatform = "Web" | "Android" | "iOS";
export type ClientChannel = "Browser" | "App";
export type ClientAgent = "Chrome" | "Edge" | "Safari" | "Firefox" | "Other";

import type { InjectionKey, Ref } from "vue";
import { inject, provide, readonly, ref } from "vue";

export interface ClientContext {
  clientType: ClientType;
  clientPlatform: ClientPlatform;
  clientChannel: ClientChannel;
  clientAgent: ClientAgent;
}

export interface TenantContext {
  tenantId: string;
}

export interface AppContext {
  appId?: string;
  appKey?: string;
  appInstanceId?: string;
}

export interface UserContext {
  userId: string;
  username: string;
  displayName: string;
}

export type HostMode = "platform" | "app";

export interface CapabilityHostContext {
  hostMode: HostMode;
  tenantId?: string;
  appId?: string;
  appKey?: string;
  appInstanceId?: string;
  permissionSet?: readonly string[];
}

export interface CapabilityHostContextProvision {
  context: Readonly<Ref<CapabilityHostContext>>;
  setContext: (value: CapabilityHostContext) => void;
  patchContext: (value: Partial<CapabilityHostContext>) => void;
}

const CAPABILITY_HOST_CONTEXT_KEY: InjectionKey<CapabilityHostContextProvision> =
  Symbol("CapabilityHostContext");

export function createCapabilityHostContextProvision(
  initial: CapabilityHostContext
): CapabilityHostContextProvision {
  const context = ref<CapabilityHostContext>({ ...initial });
  return {
    context: readonly(context),
    setContext(value: CapabilityHostContext) {
      context.value = { ...value };
    },
    patchContext(value: Partial<CapabilityHostContext>) {
      context.value = {
        ...context.value,
        ...value
      };
    }
  };
}

export function provideCapabilityHostContext(
  initial: CapabilityHostContext
): CapabilityHostContextProvision {
  const provision = createCapabilityHostContextProvision(initial);
  provide(CAPABILITY_HOST_CONTEXT_KEY, provision);
  return provision;
}

export function useCapabilityHostContext(): CapabilityHostContextProvision {
  const injected = inject(CAPABILITY_HOST_CONTEXT_KEY);
  if (!injected) {
    throw new Error(
      "[CapabilityHostContext] useCapabilityHostContext() must be called within a provider tree."
    );
  }

  return injected;
}
