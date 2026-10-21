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
# The toolchain is part of the input (phase 11, task 1.1, P11-22): global.json says "latestPatch", so a machine that also has a newer 10.0.4xx patch
# installed packs with that one, and a pack task's defaults can differ between patches. Each checkout therefore gets a job-local global.json with
# "rollForward": "disable" (global.json is not packed; the committed file is untouched) and the script prints `dotnet --version` from inside the
# checkout, exiting 1 naming both versions when it is not the one global.json pins. The CI log of the run that failed at the phase 10 merge was
# not available when this was written, so this is the evidence-free hypothesis (a) of the phase 11 design ("The reproducible-pack finding"); the
# per-entry and zip-metadata diagnostics below stay, and name the cause if the job is still red.
#
# --self-test proves the compare fails closed, as coverage-gate-selftest.sh does for coverage: it packs once, copies the pack, requires the
# identical copy to compare clean, flips one byte in one .nupkg of the copy and requires the compare to exit 1 naming that package, and requires
# the toolchain check to refuse a version other than the one pinned.
#
#   scripts/ci/reproducible-pack.sh [--self-test] [output-dir]      (default: artifacts/reproducible, or artifacts/reproducible-selftest)
set -euo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
self_test=0
if [[ "${1:-}" == "--self-test" ]]; then
  self_test=1
  shift
fi

default_out="$repo/artifacts/reproducible"
[[ $self_test -eq 1 ]] && default_out="$repo/artifacts/reproducible-selftest"
out="${1:-$default_out}"
# Each pack runs from inside its checkout, so a relative output directory is anchored to the caller's directory first.
[[ "$out" = /* ]] || out="$PWD/$out"
dotnet="${DOTNET:-dotnet}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git -C "$repo" log -1 --format=%ct)}"
echo "reproducible-pack: SOURCE_DATE_EPOCH=$SOURCE_DATE_EPOCH"

# The one SDK version global.json pins (the file has a single "version" key).
pinned="$(sed -n 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$repo/global.json" | head -n 1)"
if [[ -z "$pinned" ]]; then
  echo "reproducible-pack: global.json pins no sdk.version"
  exit 1
fi

if [[ -n "$(git -C "$repo" status --porcelain)" ]]; then
  echo "reproducible-pack: warning: the working tree has uncommitted changes; the packs are of HEAD, not of those changes"
fi

# Two different depths (V3): a path that leaks into an artefact differs between them. The self-test needs only the first.
checkouts=("$out/wt1/repo" "$out/wt2/yy/zz/repo2")
[[ $self_test -eq 1 ]] && checkouts=("$out/wt1/repo")

cleanup() {
  for checkout in "${checkouts[@]}"; do
    git -C "$repo" worktree remove --force "$checkout" 2>/dev/null || true
  done
  git -C "$repo" worktree prune
}
trap cleanup EXIT

# Rewrites the checkout's global.json (and only that copy) to "rollForward": "disable", so the SDK that packs is exactly the pinned one.
pin_toolchain() {
  local file="$1/global.json"
  sed 's/"rollForward"[[:space:]]*:[[:space:]]*"[A-Za-z]*"/"rollForward": "disable"/' "$file" > "$file.pinned"
  mv "$file.pinned" "$file"
  if ! grep -q '"rollForward": "disable"' "$file"; then
    echo "reproducible-pack: could not set rollForward to disable in $file"
    return 1
  fi
}

# Prints the SDK `dotnet` resolves from inside the checkout and fails, naming both versions, when it is not $2.
check_toolchain() {
  local checkout="$1" expected="$2" actual
  if ! actual="$(cd "$checkout" && "$dotnet" --version 2>&1)"; then
    echo "reproducible-pack: dotnet --version failed in $checkout, where global.json pins $expected with rollForward disable: ${actual//$'\n'/ }"
    return 1
  fi
  echo "reproducible-pack: toolchain: dotnet $actual in $checkout (global.json pins $expected)"
  if [[ "$actual" != "$expected" ]]; then
    echo "reproducible-pack: toolchain mismatch: dotnet $actual is not the pinned $expected; a pack is a function of its toolchain"
    return 1
  fi
}

# Adds a worktree of HEAD at $1, pins its toolchain, restores locked and packs into $out/$2.
pack_checkout() {
  local checkout="$1" run="$2"
  mkdir -p "$(dirname "$checkout")"
  git -C "$repo" worktree add --detach "$checkout" HEAD
  pin_toolchain "$checkout"
  check_toolchain "$checkout" "$pinned"
  (
    cd "$checkout"
    "$dotnet" restore Dexpace.Sdk.sln --locked-mode
    "$dotnet" pack Dexpace.Sdk.sln --configuration Release --no-restore \
      -p:ContinuousIntegrationBuild=true --output "$out/$run"
  )
}

# On a mismatch, name the package entries that differ and, for each, the printable strings found in only one of the two (a leaked path shows here).
# Best effort: it is called in an `||` list, where errexit is off, so a package the byte flip of the self-test made unreadable does not abort it.
explain() {
  local a="$out/explain/a" b="$out/explain/b" entry
  rm -rf "$out/explain"
  mkdir -p "$a" "$b"
  unzip -q -o "$1" -d "$a" || echo "reproducible-pack:   $(basename "$1") (first pack) did not unzip cleanly"
  unzip -q -o "$2" -d "$b" || echo "reproducible-pack:   $(basename "$2") (second pack) did not unzip cleanly"
  (cd "$a" && find . -type f | sort) | while read -r entry; do
    if ! cmp -s "$a/$entry" "$b/$entry"; then
      echo "reproducible-pack:   entry $entry differs"
      diff <(strings -n 8 "$a/$entry" | sort -u) <(strings -n 8 "$b/$entry" | sort -u) | grep '^[<>]' | head -n 10 | sed 's/^/reproducible-pack:     /' || true
    fi
  done
  if [[ "$(unzip -Z1 "$1" | sort)" != "$(unzip -Z1 "$2" | sort)" ]]; then
    echo "reproducible-pack:   the entry lists differ"
  fi
  if [[ "$(unzip -Zv "$1" | grep -E 'file last modified|minimum software' | sort | uniq -c)" != "$(unzip -Zv "$2" | grep -E 'file last modified|minimum software' | sort | uniq -c)" ]]; then
    echo "reproducible-pack:   the zip metadata (timestamps or versions) differ"
    diff <(unzip -Zv "$1") <(unzip -Zv "$2") | grep '^[<>]' | head -n 12 | sed 's/^/reproducible-pack:     /' || true
  fi
}

# The compare loop: every package of $1 must exist in $2 byte for byte, and the two directories must hold the same files. Returns 1 on any difference.
compare_packs() {
  local first_dir="$1" second_dir="$2" first second name status=0 count=0
  for first in "$first_dir"/*.nupkg "$first_dir"/*.snupkg; do
    name="$(basename "$first")"
    second="$second_dir/$name"
    count=$((count + 1))
    if [[ ! -f "$second" ]]; then
      echo "reproducible-pack: $name exists only in the first pack"
      status=1
    elif ! cmp -s "$first" "$second"; then
      echo "reproducible-pack: $name differs between the two packs"
      explain "$first" "$second" || true
      status=1
    else
      echo "reproducible-pack: $name identical ($(sha256sum "$first" | cut -d' ' -f1))"
    fi
  done

  if [[ "$(ls "$first_dir" | wc -l)" -ne "$(ls "$second_dir" | wc -l)" ]]; then
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
  return $status
}

# The self-test: see the header. Each case prints ok or FAILED; the exit code is 0 only when every case held.
run_self_test() {
  local failed=0 first_package flipped size offset byte replacement output rc
  first_package="$(ls "$out/1"/*.nupkg | head -n 1)"
  flipped="$(basename "$first_package")"

  output="$(compare_packs "$out/1" "$out/2" 2>&1)" && rc=0 || rc=$?
  if [[ $rc -eq 0 ]]; then
    echo "reproducible-pack: self-test ok     an identical copy compares clean"
  else
    echo "reproducible-pack: self-test FAILED an identical copy did not compare clean (exit $rc):"
    echo "$output"
    failed=1
  fi

  # Flip one byte in the middle of one package of the copy.
  size="$(wc -c < "$out/2/$flipped")"
  offset=$((size / 2))
  byte="$(dd if="$out/2/$flipped" bs=1 skip="$offset" count=1 2>/dev/null | od -An -tu1 | tr -d ' ')"
  replacement="$(printf '\\x%02x' $(((byte + 1) % 256)))"
  # shellcheck disable=SC2059
  printf "$replacement" | dd of="$out/2/$flipped" bs=1 seek="$offset" count=1 conv=notrunc 2>/dev/null

  output="$(compare_packs "$out/1" "$out/2" 2>&1)" && rc=0 || rc=$?
  if [[ $rc -eq 1 ]] && grep -Fq "reproducible-pack: $flipped differs between the two packs" <<<"$output"; then
    echo "reproducible-pack: self-test ok     one flipped byte in $flipped fails the compare (exit 1, named)"
  else
    echo "reproducible-pack: self-test FAILED one flipped byte in $flipped should exit 1 naming it; got exit $rc:"
    echo "$output"
    failed=1
  fi

  output="$(check_toolchain "${checkouts[0]}" "0.0.0-not-the-pinned-sdk" 2>&1)" && rc=0 || rc=$?
  if [[ $rc -eq 1 ]] && grep -Fq "toolchain mismatch" <<<"$output" && grep -Fq "0.0.0-not-the-pinned-sdk" <<<"$output"; then
    echo "reproducible-pack: self-test ok     a toolchain other than the pinned one is refused (exit 1, both versions named)"
  else
    echo "reproducible-pack: self-test FAILED a toolchain other than the pinned one should exit 1 naming both versions; got exit $rc:"
    echo "$output"
    failed=1
  fi

  return $failed
}

rm -rf "$out"
git -C "$repo" worktree prune

if [[ $self_test -eq 1 ]]; then
  pack_checkout "${checkouts[0]}" 1
  cp -R "$out/1" "$out/2"
  if run_self_test; then
    echo "reproducible-pack: self-test passed"
    exit 0
  fi
  echo "reproducible-pack: self-test failed"
  exit 1
fi

run=0
for checkout in "${checkouts[@]}"; do
  run=$((run + 1))
  pack_checkout "$checkout" "$run"
done

status=0
compare_packs "$out/1" "$out/2" || status=1
exit $status
