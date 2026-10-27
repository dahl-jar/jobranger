#!/bin/sh
set -eu

version="__JOBRANGER_VERSION__"
release_url="https://github.com/dahl-jar/jobranger/releases/download/cli-v$version"
download_url="${JOBRANGER_DOWNLOAD_URL:-$release_url}"
install_dir="${JOBRANGER_INSTALL_DIR:-$HOME/.local/bin}"

fail() {
  echo "jobranger install: $1" >&2
  exit 1
}

detect_rid() {
  platform="$(uname -s) $(uname -m)"
  case "$platform" in
    "Darwin arm64") echo osx-arm64 ;;
    "Linux x86_64" | "Linux amd64") echo linux-x64 ;;
    *) fail "no build for $platform, builds exist for macOS arm64 and Linux x64" ;;
  esac
}

download() {
  curl -fsSL --proto '=https,file' --proto-redir '=https' -o "$2" "$1"
}

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1"
  else
    shasum -a 256 "$1"
  fi | awk '{ print $1 }'
}

command -v curl >/dev/null 2>&1 || fail "curl is required"

rid="$(detect_rid)"
archive="jobranger-$version-$rid.tar.gz"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

download "$download_url/$archive" "$tmp/$archive"
download "$download_url/SHA256SUMS" "$tmp/SHA256SUMS"

expected="$(awk -v name="$archive" '$2 == name { print $1 }' "$tmp/SHA256SUMS")"
[ -n "$expected" ] || fail "SHA256SUMS has no entry for $archive"
[ "$(sha256 "$tmp/$archive")" = "$expected" ] || fail "checksum mismatch for $archive"

tar -xzf "$tmp/$archive" -C "$tmp" jobranger
mkdir -p "$install_dir"
cp "$tmp/jobranger" "$install_dir/.jobranger.new"
chmod 0755 "$install_dir/.jobranger.new"
mv -f "$install_dir/.jobranger.new" "$install_dir/jobranger"

echo "jobranger $version installed to $install_dir/jobranger"
case ":$PATH:" in
  *":$install_dir:"*) ;;
  *) echo "add $install_dir to PATH to run jobranger" ;;
esac
