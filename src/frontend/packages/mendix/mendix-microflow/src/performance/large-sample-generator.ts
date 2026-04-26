import { createObjectFromRegistry } from "../adapters";
import { microflowNodeRegistryByKey } from "../node-registry";
import { createSequenceFlow } from "../adapters";
import { sampleMicroflowSchema } from "../schema/sample";
import type { MicroflowSchema } from "../schema/types";

export function createLargeMicroflowSample(nodeCount = 120): MicroflowSchema {
  const schema = {
    ...(JSON.parse(JSON.stringify(sampleMicroflowSchema)) as MicroflowSchema),
    id: "large-performance-sample",
    name: "LargePerformanceSample",
    displayName: "Large Performance Sample",
  };
  const action = microflowNodeRegistryByKey.get("activity:logMessage") ?? microflowNodeRegistryByKey.get("activity:objectCreate");
  if (!action) {
    return schema;
  }

  const objects = [...schema.objectCollection.objects];
  const rootFlows = [...(schema.flows ?? [])];
  let previousObjectId = objects.at(-1)?.id;

  for (let index = 0; index < nodeCount; index += 1) {
    const row = Math.floor(index / 12);
    const column = index % 12;
    const object = createObjectFromRegistry(action, {
      x: 260 + column * 220,
      y: 160 + row * 150,
    }, `large-node-${index}`);
    objects.push(object);
    if (previousObjectId) {
      rootFlows.push(createSequenceFlow({
        originObjectId: previousObjectId,
        destinationObjectId: object.id,
      }));
    }
    previousObjectId = object.id;
  }

  return {
    ...schema,
    objectCollection: {
      ...schema.objectCollection,
      objects
    },
    flows: rootFlows
  };
}
