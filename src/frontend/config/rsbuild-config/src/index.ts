/*
 * Copyright 2025 coze-dev Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import path from 'path';

import { pluginSvgr } from '@rsbuild/plugin-svgr';
import { pluginSass } from '@rsbuild/plugin-sass';
import { pluginReact } from '@rsbuild/plugin-react';
import { pluginLess } from '@rsbuild/plugin-less';
import { type RsbuildConfig, mergeRsbuildConfig } from '@rsbuild/core';
import { SemiRspackPlugin } from '@douyinfe/semi-rspack-plugin';
import { GLOBAL_ENVS } from '@coze-arch/bot-env';
import { PackageAtAliasResolverPlugin } from './package-at-alias-resolver-plugin';

// 通过环境变量门控 Rspack 实验性持久化缓存：默认开启，遇到缓存类异常可临时关闭
// 不直接升级 @rsbuild/core 是因为仓库内 30+ 个包都锁定 1.1.13，强升风险过大；
// 这里走 tools.rspack 直通 @rspack/core 1.1.8 原生 API（与 webpack 5 cache 等价）。
const persistentCacheEnabled =
  (process.env.ATLAS_RSBUILD_PERSISTENT_CACHE ?? 'true').toLowerCase() !==
  'false';

const getDefine = () => {
  const define = {};
  Object.keys(GLOBAL_ENVS).forEach(key => {
    // In the definition of rspack, strings need to be enclosed in double quotes before they can be used as strings in code.
    if (typeof GLOBAL_ENVS[key] === 'string') {
      define[key] = `"${GLOBAL_ENVS[key]}"`;
    } else {
      define[key] = GLOBAL_ENVS[key];
    }
  });
  return define;
};

export const overrideBrowserslist = [
  'chrome >= 51',
  'edge >= 15',
  'firefox >= 54',
  'safari >= 10',
  'ios_saf >= 10',
];

const generateCdnPrefix = () => {
  if (process.env.CDN_INNER_CN) {
    return `https://${process.env.CDN_INNER_CN}/${
      process.env.CDN_PATH_PREFIX ? `${process.env.CDN_PATH_PREFIX}/` : ''
    }`;
  }
  return '/';
};

export const defineConfig = (options: Partial<RsbuildConfig>) => {
  const cdnPrefix = generateCdnPrefix();
  const configuredPort =
    typeof options.server === 'object' && options.server?.port
      ? Number(options.server.port)
      : 8080;
  const workspaceRoot = path.resolve(__dirname, '..', '..', '..');
  const commonAssertsUrl = path.dirname(
    require.resolve('@coze-common/assets/package.json'),
  );

  const config: RsbuildConfig = {
    dev: {
      client: {
        port: configuredPort,
        host: '127.0.0.1',
        protocol: 'ws',
      },
    },
    server: {
      port: configuredPort,
    },
    plugins: [
      pluginReact(),
      pluginSvgr({
        mixedImport: true,
        svgrOptions: {
          exportType: 'named',
        },
      }),
      pluginLess({
        lessLoaderOptions: {
          additionalData: `@import "${path.resolve(
            commonAssertsUrl,
            'style/variables.less',
          )}";`,
        },
      }),
      pluginSass({
        sassLoaderOptions: {
          sassOptions: {
            silenceDeprecations: ['mixed-decls', 'import', 'function-units'],
          },
        },
      }),
    ],
    output: {
      filenameHash: true,
      assetPrefix: cdnPrefix,
      injectStyles: true,
      cssModules: {
        auto: true,
      },
      sourceMap: {
        js: 'source-map',
      },
      overrideBrowserslist,
    },
    source: {
      define: getDefine(),
      alias: {
        '@coze-arch/semi-theme-hand01': path.dirname(
          require.resolve('@coze-arch/semi-theme-hand01/package.json'),
        ),
      },
      include: [
        // The following packages contain undegraded ES 2022 syntax (private methods) that need to be packaged
        /\/node_modules\/(marked|@dagrejs|@tanstack)\//,
      ],
    },
    tools: {
      postcss: (opts, { addPlugins }) => {
        addPlugins([
          // eslint-disable-next-line @typescript-eslint/no-require-imports
          require('tailwindcss/nesting')(require('postcss-nesting')),
          require('tailwindcss'),
        ]);
      },
      rspack: (rspackConfig, { appendPlugins, mergeConfig }) => {
        appendPlugins([
          new PackageAtAliasResolverPlugin({
            workspaceRoot,
          }),
          new SemiRspackPlugin({
            theme: '@coze-arch/semi-theme-hand01',
          }),
        ]);

        if (!persistentCacheEnabled) {
          return rspackConfig;
        }

        // 启用 Rspack 持久化缓存：cold start / cold build 后续重跑显著加速
        // (rspack 1.x 实验特性，等价于 rsbuild 1.2.5+ 的 performance.buildCache)
        return mergeConfig(rspackConfig, {
          cache: true,
          experiments: {
            cache: {
              type: 'persistent',
              buildDependencies: [__filename],
            },
          },
        });
      },
    },
  };

  return mergeRsbuildConfig(config, options);
};
