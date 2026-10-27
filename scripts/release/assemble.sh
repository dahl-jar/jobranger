#!/usr/bin/env bash
set -euo pipefail

rids=(osx-arm64 linux-x64)
placeholder="__JOBRANGER_VERSION__"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$#" -ne 3 ]; then
  echo "usage: assemble.sh <version> <archives-dir> <out-dir>" >&2
  exit 2
fi
version="$1"
archives_dir="$2"
out_dir="$3"

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1"
  else
    shasum -a 256 "$1"
  fi | awk '{ print $1 }'
}

assets=()
for rid in "${rids[@]}"; do
  archive="jobranger-$version-$rid.tar.gz"
  if [ ! -f "$archives_dir/$archive" ]; then
    echo "missing archive $archive" >&2
    exit 1
  fi
  mkdir -p "$out_dir"
  cp "$archives_dir/$archive" "$out_dir/$archive"
  assets+=("$archive")
done

sed "s/$placeholder/$version/g" "$script_dir/install.sh" >"$out_dir/install.sh"
chmod 0755 "$out_dir/install.sh"
assets+=(install.sh)

: >"$out_dir/SHA256SUMS"
for asset in "${assets[@]}"; do
  digest="$(sha256 "$out_dir/$asset")"
  printf '%s  %s\n' "$digest" "$asset" >>"$out_dir/SHA256SUMS"
done
