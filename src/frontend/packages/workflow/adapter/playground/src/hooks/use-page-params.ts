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

import { useMemo } from 'react';

import { useMount } from 'ahooks';
import { type WorkflowPlaygroundProps } from '@coze-workflow/playground';
import { OperateType } from '@coze-workflow/base/api';
import { I18n } from '@coze-arch/i18n';
import { Toast } from '@coze-arch/coze-design';
import type { WorkflowPageProps } from '../page';

/** Process details page parameters */
interface SearchParams {
  workflow_id: string;
  space_id: string;
  /** Process version, when multiple people cooperate, there is a version concept, and the corresponding version process can be previewed when set separately */
  version?: string;
  /** Whether to restore to the target version, if set, the process draft will be automatically set to the corresponding version */
  set_version?: string;
  /** Corresponding version of the operation type */
  opt_type?: string;
  /** Process page open source */
  from?: WorkflowPlaygroundProps['from'];
  /** The node id configuration will automatically locate to the corresponding node. */
  node_id?: string;
  /** The execution id configuration will display the corresponding execution result. */
  execute_id?: string;
  /** subprocess execution id */
  sub_execute_id?: string;
  /** Atlas host return url */
  return_url?: string;
}

export function usePageParams(overrides?: WorkflowPageProps) {
  const searchParams = useMemo(() => {
    const target: SearchParams = {
      workflow_id: overrides?.workflowId ?? '',
      space_id: overrides?.spaceId ?? '',
    };
    const params = new URLSearchParams(window.location.search);
    params.forEach((value, key) => {
      target[key] = value;
    });

    if (overrides?.workflowId) {
      target.workflow_id = overrides.workflowId;
    }
    if (overrides?.spaceId) {
      target.space_id = overrides.spaceId;
    }
    if (overrides?.version) {
      target.version = overrides.version;
    }
    if (typeof overrides?.setVersion === 'boolean') {
      target.set_version = overrides.setVersion ? '1' : undefined;
    }
    if (typeof overrides?.optType === 'number') {
      target.opt_type = String(overrides.optType);
    }
    if (overrides?.from) {
      target.from = overrides.from as WorkflowPlaygroundProps['from'];
    }
    if (overrides?.nodeId) {
      target.node_id = overrides.nodeId;
    }
    if (overrides?.executeId) {
      target.execute_id = overrides.executeId;
    }
    if (overrides?.subExecuteId) {
      target.sub_execute_id = overrides.subExecuteId;
    }
    if (overrides?.returnUrl) {
      target.return_url = overrides.returnUrl;
    }

    return target;
  }, [
    overrides?.executeId,
    overrides?.from,
    overrides?.nodeId,
    overrides?.optType,
    overrides?.returnUrl,
    overrides?.setVersion,
    overrides?.spaceId,
    overrides?.subExecuteId,
    overrides?.version,
    overrides?.workflowId,
  ]);

  const {
    space_id: spaceId,
    workflow_id: workflowId,
    version,
    set_version: setVersion,
    opt_type,
    from,
    node_id: nodeId,
    execute_id: executeId,
    sub_execute_id: subExecuteId,
    return_url: returnUrl,
  } = searchParams;

  const optType = opt_type
    ? (Number(opt_type) as OperateType)
    : OperateType.SubmitOperate;

  useMount(() => {
    const newSearchParams = new URLSearchParams(window.location.search);
    let needUpdateSearch = false;
    if (from === 'createSuccess') {
      Toast.success(I18n.t('Create_success'));
      newSearchParams.delete('from');
      needUpdateSearch = true;
    } else if (from === 'dupSuccess') {
      Toast.success(I18n.t('Duplicate_success'));
      newSearchParams.delete('from');
      needUpdateSearch = true;
    }

    // Force setting of historical version
    if (setVersion) {
      newSearchParams.delete('set_version');
      newSearchParams.delete('version');
      newSearchParams.delete('opt_type');
      needUpdateSearch = true;
    }

    if (needUpdateSearch) {
      history.replaceState(
        {},
        '',
        `${location.origin + location.pathname}?${newSearchParams.toString()}`,
      );
    }
  });

  return {
    spaceId,
    workflowId,
    version,
    setVersion: Boolean(searchParams.set_version),
    optType,
    from,
    nodeId,
    executeId,
    subExecuteId,
    returnUrl,
  };
}
