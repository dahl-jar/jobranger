#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python_out="$root/cli/src/jobranger/link/contracts"
typescript_out="$root/web/src/types/contracts"

for name in payload; do
  schema="$root/contracts/$name.schema.json"
  if [[ ! -f "$schema" ]]; then
    continue
  fi
  mkdir -p "$python_out" "$typescript_out"
  uvx --from datamodel-code-generator==0.83.0 datamodel-codegen \
    --input "$schema" \
    --input-file-type jsonschema \
    --output-model-type pydantic_v2.BaseModel \
    --disable-timestamp \
    --output "$python_out/$name.py"
  pnpm --dir "$root/web" dlx --package=json-schema-to-typescript@16.0.0 json2ts \
    --input "$schema" \
    --output "$typescript_out/$name.gen.ts"
done
