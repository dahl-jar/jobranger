import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { Ajv2020, _ } from 'ajv/dist/2020.js'
import addFormats from 'ajv-formats'
import standaloneCode from 'ajv/dist/standalone/index.js'

const VALIDATORS = [
  { name: 'validatePayload', schema: 'payload', type: 'Payload' },
  { name: 'validateEnvelope', schema: 'envelope', type: 'Envelope' },
]
const SCHEMAS = Object.fromEntries(VALIDATORS.map(({ name, schema }) => [name, schema]))
const REQUIRE_STATEMENT = /const (\w+) = require\("([^"]+)"\)\.(\w+)((?:\.\w+)*);/g
const USE_STRICT = '"use strict";'
const DECLARATIONS = `import type { ErrorObject } from 'ajv'
${VALIDATORS.map(({ schema, type }) => `import type { ${type} } from './${schema}.gen'\n`).join('')}
export interface StandaloneValidator<T> {
  (data: unknown): data is T
  errors?: ErrorObject[] | null
}

${VALIDATORS.map(({ name, type }) => `export declare const ${name}: StandaloneValidator<${type}>\n`).join('')}`

function readSchema(messages: string, name: string): object {
  return JSON.parse(readFileSync(join(messages, `${name}.schema.json`), 'utf8'))
}

const RESOLVE_EXPORT = `function resolveExport(namespace, member) {
  const direct = namespace[member];
  if (direct !== undefined && (member !== "default" || typeof direct === "function")) {
    return direct;
  }
  return namespace.default[member];
}`

function toImports(code: string): string {
  const imports: string[] = []
  const body = code.replace(REQUIRE_STATEMENT, (_statement, local: string, source: string, member: string, rest: string) => {
    const namespace = `${local}Module`
    imports.push(`import * as ${namespace} from ${JSON.stringify(source)};`)
    return `const ${local} = resolveExport(${namespace}, ${JSON.stringify(member)})${rest};`
  })
  return `${imports.join('\n')}\n${RESOLVE_EXPORT}\n${body.replace(USE_STRICT, '')}\n`
}

function generate(messages: string, out: string): void {
  const ajv = new Ajv2020({
    discriminator: true,
    strict: true,
    inlineRefs: false,
    code: { source: true, esm: true, lines: true, formats: _`require("ajv-formats/dist/formats").fullFormats` },
  })
  addFormats.default(ajv)
  for (const { schema } of VALIDATORS) {
    ajv.addSchema(readSchema(messages, schema), schema)
  }
  writeFileSync(`${out}.js`, toImports(standaloneCode.default(ajv, SCHEMAS)))
  writeFileSync(`${out}.d.ts`, DECLARATIONS)
}

const [messages, out] = process.argv.slice(2)
if (!messages || !out) {
  throw new Error('usage: generate-validators.ts <messages dir> <output path without extension>')
}
generate(messages, out)
