import fs from 'node:fs';
import path from 'node:path';
import OriginPkgRootWebpackPlugin from '@coze-arch/pkg-root-webpack-plugin-origin';

type PackageAtAliasResolverPluginOptions = Record<string, unknown> & {
  workspaceRoot?: string;
};

const defaultWorkspaceScopes = ['packages', 'apps', 'config', 'infra'];
const ignoredFolders = new Set([
  '.git',
  '.turbo',
  '.yarn',
  'coverage',
  'dist',
  'lib',
  'node_modules',
]);
const packageDirCache = new Map<string, string[]>();

function hasPackageMarker(directory: string) {
  const packageJsonPath = path.join(directory, 'package.json');
  const tsconfigBuildPath = path.join(directory, 'tsconfig.build.json');
  const tsconfigPath = path.join(directory, 'tsconfig.json');

  return (
    fs.existsSync(packageJsonPath) &&
    (fs.existsSync(tsconfigBuildPath) || fs.existsSync(tsconfigPath))
  );
}

function collectWorkspacePackageDirs(workspaceRoot: string) {
  const normalizedWorkspaceRoot = path.resolve(workspaceRoot);
  const cached = packageDirCache.get(normalizedWorkspaceRoot);
  if (cached) {
    return cached;
  }

  const packageDirs: string[] = [];

  const visit = (directory: string) => {
    const entryName = path.basename(directory);
    if (ignoredFolders.has(entryName)) {
      return;
    }

    if (hasPackageMarker(directory)) {
      packageDirs.push(path.relative(normalizedWorkspaceRoot, directory));
      return;
    }

    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      if (!entry.isDirectory()) {
        continue;
      }
      visit(path.join(directory, entry.name));
    }
  };

  for (const scope of defaultWorkspaceScopes) {
    const scopeRoot = path.join(normalizedWorkspaceRoot, scope);
    if (fs.existsSync(scopeRoot) && fs.statSync(scopeRoot).isDirectory()) {
      visit(scopeRoot);
    }
  }

  packageDirCache.set(normalizedWorkspaceRoot, packageDirs);
  return packageDirs;
}

export class PackageAtAliasResolverPlugin extends OriginPkgRootWebpackPlugin {
  constructor(options: Partial<PackageAtAliasResolverPluginOptions> = {}) {
    const workspaceRoot = options.workspaceRoot
      ? path.resolve(String(options.workspaceRoot))
      : path.resolve(__dirname, '..', '..', '..');
    const { workspaceRoot: _ignored, ...restOptions } = options;
    const packagesDirs = collectWorkspacePackageDirs(workspaceRoot);

    super({
      ...restOptions,
      root: '@',
      packagesDirs,
      excludeFolders: [],
    });
  }
}
