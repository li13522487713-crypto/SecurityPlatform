import type { MicroflowResource, MicroflowResourcePermissions } from "../resource/resource-types";

export const defaultMicroflowPermissions: MicroflowResourcePermissions = {
  canEdit: true,
  canDelete: true,
  canPublish: true,
  canArchive: true,
  canDuplicate: true
};

export function resolveMicroflowPermissions(resource?: MicroflowResource, readonly?: boolean): MicroflowResourcePermissions {
  const base = resource?.permissions ?? defaultMicroflowPermissions;
  if (!readonly) {
    return resource?.archived ? { ...base, canPublish: false } : base;
  }
  return {
    canEdit: false,
    canDelete: false,
    canPublish: false,
    canArchive: false,
    canDuplicate: false
  };
}
