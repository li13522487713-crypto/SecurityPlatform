[CmdletBinding()]
param(
    [string]$UpstreamRepo = "D:\Code\coze-studio-main",
    [string]$ProjectName = "@coze-studio/app",
    [string]$Subspace = "default",
    [string]$TargetFolder = "C:\Users\kuo13\AppData\Local\Temp\atlas-coze-workflow-host-deploy",
    [string]$BuildOutputFolder = "C:\Users\kuo13\AppData\Local\Temp\atlas-coze-studio-dist",
    [switch]$SkipInstall,
    [switch]$SkipBuild,
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

function Invoke-Rush {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    Push-Location $UpstreamRepo
    try {
        & node (Join-Path $UpstreamRepo "common\scripts\install-run-rush.js") @Args
        if ($LASTEXITCODE -ne 0) {
            throw "Rush command failed: $($Args -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-UpstreamAppBuild {
    $appFolder = Join-Path $UpstreamRepo "frontend\apps\coze-studio"
    $rsbuild = Join-Path $appFolder "node_modules\.bin\rsbuild.CMD"
    $overrideConfig = Join-Path $appFolder "rsbuild.atlas.override.ts"
    if (-not (Test-Path $rsbuild)) {
        throw "找不到 rsbuild 可执行文件: $rsbuild"
    }

    if (-not (Test-Path $BuildOutputFolder)) {
        New-Item -ItemType Directory -Path $BuildOutputFolder -Force | Out-Null
    }

    @"
import path from 'path';
import { defineConfig } from '@coze-arch/rsbuild-config';
import { GLOBAL_ENVS } from '@coze-arch/bot-env';

const apiTarget = process.env.WEB_SERVER_PORT
  ? 'http://localhost:' + process.env.WEB_SERVER_PORT + '/'
  : 'http://localhost:5002/';

export default defineConfig({
  server: {
    strictPort: true,
    proxy: [
      { context: ['/api'], target: apiTarget, secure: false, changeOrigin: true },
      { context: ['/v1'], target: apiTarget, secure: false, changeOrigin: true }
    ]
  },
  html: {
    title: 'Atlas Coze Workflow Host',
    template: './index.html',
    crossorigin: 'anonymous'
  },
  tools: {
    postcss: (_opts, { addPlugins }) => {
      addPlugins([
        require('tailwindcss')('./tailwind.config.ts')
      ]);
    },
    rspack(config, { addRules, mergeConfig }) {
      addRules([
        {
          test: /\.(css|less|jsx|tsx|ts|js)$/,
          exclude: [/node_modules/],
          use: '@coze-arch/import-watch-loader'
        }
      ]);

      return mergeConfig(config, {
        module: {
          parser: {
            javascript: {
              exportsPresence: false
            }
          }
        },
        resolve: {
          fallback: {
            path: require.resolve('path-browserify')
          }
        },
        watchOptions: { poll: true },
        ignoreWarnings: [
          /Critical dependency: the request of a dependency is an expression/,
          warning => true
        ]
      });
    }
  },
  source: {
    define: {
      'process.env.IS_REACT18': JSON.stringify(true),
      'process.env.ARCOSITE_SDK_REGION': JSON.stringify(GLOBAL_ENVS.IS_OVERSEA ? 'VA' : 'CN'),
      'process.env.ARCOSITE_SDK_SCOPE': JSON.stringify(GLOBAL_ENVS.IS_RELEASE_VERSION ? 'PUBLIC' : 'INSIDE'),
      'process.env.TARO_PLATFORM': JSON.stringify('web'),
      'process.env.SUPPORT_TARO_POLYFILL': JSON.stringify('disabled'),
      'process.env.RUNTIME_ENTRY': JSON.stringify('@coze-dev/runtime'),
      'process.env.TARO_ENV': JSON.stringify('h5'),
      ENABLE_COVERAGE: JSON.stringify(false),
    },
    include: [
      path.resolve(__dirname, '../../packages'),
      path.resolve(__dirname, '../../infra/flags-devtool'),
      /\/node_modules\/(marked|@dagrejs|@tanstack)\//,
    ],
    alias: {
      '@coze-arch/foundation-sdk': require.resolve('@coze-foundation/foundation-sdk'),
      'react-router-dom': require.resolve('react-router-dom'),
    },
    decorators: { version: 'legacy' }
  },
  output: {
    distPath: {
      root: process.env.ATLAS_COZE_BUILD_OUTPUT || 'C:\\Users\\kuo13\\AppData\\Local\\Temp\\atlas-coze-studio-dist'
    },
    sourceMap: {
      js: false
    }
  }
});
"@ | Set-Content -Path $overrideConfig

    $env:IS_OPEN_SOURCE = "true"
    $env:ATLAS_COZE_BUILD_OUTPUT = $BuildOutputFolder
    Push-Location $appFolder
    try {
        & $rsbuild build -c $overrideConfig
    }
    finally {
        Pop-Location
    }
    if ($LASTEXITCODE -ne 0) {
        throw "上游 app rsbuild build 失败"
    }
}

if (-not (Test-Path $UpstreamRepo)) {
    throw "上游仓库不存在: $UpstreamRepo"
}

if (-not $SkipInstall) {
    Write-Host "[coze-host] rush install --subspace $Subspace --to $ProjectName"
    Invoke-Rush -Args @("install", "--subspace", $Subspace, "--to", $ProjectName)
}

if (-not $SkipBuild) {
    Write-Host "[coze-host] upstream app rsbuild build"
    Invoke-UpstreamAppBuild
}

if (-not $SkipDeploy) {
    Write-Host "[coze-host] rush deploy --project $ProjectName --target-folder $TargetFolder --overwrite"
    Invoke-Rush -Args @(
        "deploy",
        "--project", $ProjectName,
        "--target-folder", $TargetFolder,
        "--overwrite"
    )
}

Write-Host "[coze-host] 完成"
