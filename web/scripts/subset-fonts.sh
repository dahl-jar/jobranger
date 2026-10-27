#!/usr/bin/env bash
set -euo pipefail

nerd_fonts_version="v3.5.1"
archive_sha256="04d5e8f903693f9dd13e16f867e994834e681eb3c72c0d337a770dcda09010cf"
archive_url="https://github.com/ryanoasis/nerd-fonts/releases/download/$nerd_fonts_version/JetBrainsMono.tar.xz"
unicodes="U+0020-007E,U+00A0-00FF,U+2013,U+2019,U+201C,U+201D,U+2022,U+2026,U+2039,U+203A,U+2212,U+2713,U+00B7"
weights=(Regular Medium SemiBold Bold)

web="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
committed="$web/public/fonts"
work="$(mktemp -d)"
trap 'rm -r "$work"' EXIT
out="$committed"
if [[ "${1:-}" == "--check" ]]; then
  out="$work/fonts"
fi

curl -fsSL --output "$work/fonts.tar.xz" "$archive_url"
printf '%s  %s\n' "$archive_sha256" "$work/fonts.tar.xz" | shasum -a 256 --check --status
tar -xJf "$work/fonts.tar.xz" -C "$work"
mkdir -p "$out"

for weight in "${weights[@]}"; do
  uvx --from 'fonttools[woff]==4.66.1' pyftsubset "$work/JetBrainsMonoNerdFont-$weight.ttf" \
    --unicodes="$unicodes" \
    --flavor=woff2 \
    --layout-features='*' \
    --drop-tables+=PfEd \
    --output-file="$out/JetBrainsMonoNerdFont-$weight.woff2"
done

cp "$work/OFL.txt" "$out/OFL.txt"

if [[ "$out" != "$committed" ]]; then
  diff -r "$out" "$committed"
fi
