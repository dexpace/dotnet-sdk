#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# NFR-12's build half (design §9.2; phase 10, P10-14): pack every library twice from two checkouts of HEAD at DIFFERENT paths, with
# SOURCE_DATE_EPOCH pinned to the commit time, and require byte-identical packages (.nupkg, and .snupkg once symbols are packed). The compiler is
# already deterministic (Deterministic + ContinuousIntegrationBuild); SOURCE_DATE_EPOCH fixes the zip-entry timestamps of the package containers; and
# the two different paths prove that no absolute path leaks into an artefact (the assembly embeds a pdb path when nothing maps it), which packing
# twice in one directory cannot see. Reproducibility across paths holds for a git checkout, which CI always is; a source tarball with no .git is not
# a reproducible input. The checkouts are `git worktree`s of HEAD, so what is packed is HEAD, not uncommitted edits (a dirty tree is warned about).
# The release half (a published artifact rebuilt from its tag) is phase 12's. Exit 0 when both packs match, 1 when any file differs.
#
#   scripts/ci/reproducible-pack.sh [output-dir]      (default: artifacts/reproducible)
set -euo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="${1:-$repo/artifacts/reproducible}"
# Each pack runs from inside its checkout, so a relative output directory is anchored to the caller's directory first.
[[ "$out" = /* ]] || out="$PWD/$out"
dotnet="${DOTNET:-dotnet}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git -C "$repo" log -1 --format=%ct)}"
echo "reproducible-pack: SOURCE_DATE_EPOCH=$SOURCE_DATE_EPOCH"

if [[ -n "$(git -C "$repo" status --porcelain)" ]]; then
  echo "reproducible-pack: warning: the working tree has uncommitted changes; the packs are of HEAD, not of those changes"
fi

# Two different depths (V3): a path that leaks into an artefact differs between them.
checkouts=("$out/wt1/repo" "$out/wt2/yy/zz/repo2")

cleanup() {
  for checkout in "${checkouts[@]}"; do
    git -C "$repo" worktree remove --force "$checkout" 2>/dev/null || true
  done
  git -C "$repo" worktree prune
}
trap cleanup EXIT

rm -rf "$out"
git -C "$repo" worktree prune
run=0
for checkout in "${checkouts[@]}"; do
  run=$((run + 1))
  mkdir -p "$(dirname "$checkout")"
  git -C "$repo" worktree add --detach "$checkout" HEAD
  (
    cd "$checkout"
    "$dotnet" restore Dexpace.Sdk.sln --locked-mode
    "$dotnet" pack Dexpace.Sdk.sln --configuration Release --no-restore \
      -p:ContinuousIntegrationBuild=true --output "$out/$run"
  )
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
  echo "reproducible-pack: $count file(s) byte-identical across both packs, built at different paths"
fi
exit $status
