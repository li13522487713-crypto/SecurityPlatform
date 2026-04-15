# Coze Workflow Host

该目录承载 Atlas 的原生 Coze Workflow Host。

当前采用两条路径：

1. `rsbuild` 本地子空间：
   - 用于对齐 `coze-studio-main/frontend/apps/coze-studio` 的原生入口结构
   - 依赖 `src/frontend/packages`、`src/frontend/config`、`src/frontend/infra` 中同步过来的 Coze 真源包

2. `rush deploy` 上游产物：
   - 用于从架构上规避 Coze 源 workspace 的开发依赖污染
   - 标准入口脚本：`scripts/prepare-coze-workflow-host.ps1`
   - 默认会对上游 `D:\Code\coze-studio-main` 执行：
     - `rush install --subspace default --to @coze-studio/app`
     - 将 `frontend/apps/coze-studio/dist` 重定向到 `C:\Users\kuo13\AppData\Local\Temp\atlas-coze-studio-dist`
     - 直接运行 `frontend/apps/coze-studio/node_modules/.bin/rsbuild.CMD build`
     - `rush deploy --project @coze-studio/app --target-folder C:\Users\kuo13\AppData\Local\Temp\atlas-coze-workflow-host-deploy --overwrite`

推荐优先使用 `rush deploy` 产物作为可运行 Host 闭包，因为它会排除本地项目的 `devDependencies`，能避免 `space-bot` 闭包在源码级 workspace 安装时被 `eslint`、`less`、`reactflow` 等开发依赖拖垮。

标准用法：

1. 构建并部署原生 Host 闭包
   - `pnpm --dir src/coze-workflow-host run build`
2. 启动本地预览服务
   - `pnpm --dir src/coze-workflow-host run preview:upstream`
3. Atlas 宿主默认跳转地址
   - `http://127.0.0.1:5182`
