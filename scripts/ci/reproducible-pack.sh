#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# NFR-12's build half (design §9.2): pack every library twice from a clean src/ tree, with SOURCE_DATE_EPOCH
# pinned to the commit time, and require byte-identical packages (.nupkg, and .snupkg once symbols are packed). The compiler is already
# deterministic (Deterministic + ContinuousIntegrationBuild); SOURCE_DATE_EPOCH fixes the zip-entry timestamps
# of the package containers. Exit 0 when both packs match, 1 when any file differs.
#
#   scripts/ci/reproducible-pack.sh [output-dir]      (default: artifacts/reproducible)
set -euo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="${1:-$repo/artifacts/reproducible}"
dotnet="${DOTNET:-dotnet}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git -C "$repo" log -1 --format=%ct)}"
echo "reproducible-pack: SOURCE_DATE_EPOCH=$SOURCE_DATE_EPOCH"

rm -rf "$out"
for run in 1 2; do
  rm -rf "$repo"/src/*/bin "$repo"/src/*/obj
  "$dotnet" restore "$repo/Dexpace.Sdk.sln" --locked-mode
  "$dotnet" pack "$repo/Dexpace.Sdk.sln" --configuration Release --no-restore \
    -p:ContinuousIntegrationBuild=true --output "$out/$run"
done

status=0
count=0
for first in "$out"/1/*.nupkg "$out"/1/*.snupkg; do
  name="$(basename "$first")"
  second="$out/2/$name"
  count=$((count + 1))
  if [[ ! -f "$second" ]]; then
    echo "reproducible-pack: $name exists only in the first pack"
    status=1
  elif ! cmp -s "$first" "$second"; then
    echo "reproducible-pack: $name differs between the two packs"
    status=1
  else
    echo "reproducible-pack: $name identical ($(sha256sum "$first" | cut -d' ' -f1))"
  fi
done

if [[ "$(ls "$out/1" | wc -l)" -ne "$(ls "$out/2" | wc -l)" ]]; then
  echo "reproducible-pack: the two packs produced different file sets"
  status=1
fi

if [[ $count -eq 0 ]]; then
  echo "reproducible-pack: the first pack produced no package"
  status=1
fi

if [[ $status -eq 0 ]]; then
  echo "reproducible-pack: $count file(s) byte-identical across both packs"
fi
exit $status
