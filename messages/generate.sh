#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python_out="$root/cli-python/src/jobranger/contract"
typescript_out="$root/frontend/src/features/relay/contract"
uv_run=(uv run --project "$root/cli-python" --locked)
codegen=("${uv_run[@]}" datamodel-codegen
  --input-file-type jsonschema
  --output-model-type pydantic_v2.BaseModel
  --target-python-version 3.12
  --use-annotated
  --enum-field-as-literal all
  --collapse-root-models
  --extra-fields ignore
  --strict-types str int float bool
  --output-datetime-class AwareDatetime
  --formatters builtin
  --disable-timestamp)

mkdir -p "$python_out" "$typescript_out"
rm -f "$typescript_out"/*.gen.* "$python_out"/*.schema.json "$python_out/payload.py" "$python_out/envelope.py"
for name in payload envelope; do
  schema="$root/messages/$name.schema.json"
  "${codegen[@]}" --input "$schema" --output "$python_out/$name.py"
  pnpm --dir "$root/frontend" exec json2ts \
    --input "$schema" \
    --output "$typescript_out/$name.gen.ts" \
    --additionalProperties false \
    --bannerComment "" \
    --maxItems -1
done
node "$root/frontend/scripts/generate-validators.ts" "$root/messages" "$typescript_out/validate.gen"
"${uv_run[@]}" python "$root/messages/scripts/postprocess.py" "$root"
"${uv_run[@]}" ruff format --quiet "$python_out"
"${uv_run[@]}" ruff check --fix --quiet "$python_out"
