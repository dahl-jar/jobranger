import type { JsonPath } from './scan'

export type SchemaNode = Readonly<Record<string, unknown>>

function isNode(value: unknown): value is SchemaNode {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function ownChild(container: unknown, name: string | number): unknown {
  const owns = typeof container === 'object' && container !== null && Object.hasOwn(container, name)
  return owns ? Reflect.get(container, name) : undefined
}

function nodes(list: unknown): SchemaNode[] {
  return Array.isArray(list) ? list.filter(isNode) : []
}

function resolve(node: SchemaNode, root: SchemaNode): SchemaNode {
  const ref = node.$ref
  if (typeof ref !== 'string') {
    return node
  }
  const target = ref.split('/').slice(1).reduce<unknown>(ownChild, root)
  return isNode(target) ? resolve(target, root) : {}
}

function taggedBranches(node: SchemaNode, value: unknown, root: SchemaNode): SchemaNode[] {
  const tag = ownChild(node.discriminator, 'propertyName')
  if (typeof tag !== 'string') {
    return []
  }
  const wanted = ownChild(value, tag)
  return nodes(node.oneOf).filter((branch) => ownChild(ownChild(resolve(branch, root).properties, tag), 'const') === wanted)
}

function schemasAt(node: SchemaNode, value: unknown, path: JsonPath, root: SchemaNode): SchemaNode[] {
  const resolved = resolve(node, root)
  const branches = [...nodes(resolved.anyOf), ...taggedBranches(resolved, value, root)]
  if (branches.length > 0) {
    return branches.flatMap((branch) => schemasAt(branch, value, path, root))
  }
  const [head, ...rest] = path
  if (head === undefined) {
    return [resolved]
  }
  const child = typeof head === 'number' ? resolved.items : ownChild(resolved.properties, head)
  return isNode(child) ? schemasAt(child, ownChild(value, head), rest, root) : []
}

function wantsInteger(node: SchemaNode): boolean {
  return node.type === 'integer' || Number.isInteger(node.const)
}

export function isIntegerPath(schema: SchemaNode, value: unknown, path: JsonPath): boolean {
  return schemasAt(schema, value, path, schema).some(wantsInteger)
}
