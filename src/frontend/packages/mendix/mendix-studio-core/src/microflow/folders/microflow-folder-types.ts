export interface MicroflowFolder {
  id: string;
  workspaceId?: string;
  moduleId: string;
  parentFolderId?: string;
  name: string;
  path: string;
  depth: number;
  createdBy?: string;
  createdAt?: string;
  updatedBy?: string;
  updatedAt?: string;
}

export interface MicroflowFolderTreeNode extends MicroflowFolder {
  children: MicroflowFolderTreeNode[];
}

export interface ListMicroflowFoldersQuery {
  workspaceId?: string;
  moduleId: string;
}

export interface CreateMicroflowFolderInput {
  workspaceId?: string;
  moduleId: string;
  parentFolderId?: string;
  name: string;
}

export interface RenameMicroflowFolderInput {
  name: string;
}

export interface MoveMicroflowFolderInput {
  parentFolderId?: string;
}

export interface MoveMicroflowInput {
  targetFolderId?: string;
}
