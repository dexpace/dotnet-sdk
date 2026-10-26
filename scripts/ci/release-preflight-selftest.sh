#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# Proves the release preflight fails closed (NFR-16, NFR-12; phase 12, P12-4), as coverage-gate-selftest.sh does for coverage: runs
# scripts/ci/release-preflight.cs over altered copies of the packages a real `dotnet pack` left in <package-dir>, and requires
#   - the unaltered packages with no tag: exit 0 (or nothing below proves anything);
#   - a .nupkg removed: exit 1, naming the package; a .snupkg removed: exit 1, naming the .nupkg with none beside it;
#   - one package renamed to another version: exit 1, naming both versions (one version train);
#   - an expected token the assemblies do not carry: exit 1, naming the assembly and both tokens (an unsigned assembly shares this code path);
#   - with --tag: a tag that is not the packages' version, v0.1.0 for a pre-release, a tag without the v, a CHANGELOG.md without the section, a
#     non-empty PublicAPI.Unshipped.txt and a tag commit that is not an ancestor of main: each exit 1, naming the cause; a v<version> tag on main
#     over a settled copy: exit 0;
#   - without --tag, the two tag-dependent findings that can still be read print `would fail:` and the exit stays 0 (the dry run).
# File operations and a scratch git repository only (no zip, no network), so it runs on all three operating-system rows. Nothing is hard-coded but
# Dexpace.Sdk.Core: the version is read from the package names. Run from anywhere after the CI pack step. Exit 0 when every case holds, 1 otherwise.
#
#   scripts/ci/release-preflight-selftest.sh [package-dir]      (default: artifacts/packages)
set -uo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
packages="$(cd "${1:-$repo/artifacts/packages}" && pwd)"
dotnet="${DOTNET:-dotnet}"
work="$repo/artifacts/release-preflight-selftest"
cd "$repo" || exit 1

core=("$packages"/Dexpace.Sdk.Core.[0-9]*.nupkg)
if [ ${#core[@]} -ne 1 ]; then
  echo "release-preflight-selftest: need exactly one Dexpace.Sdk.Core package under $packages (pack Release first)" >&2
  exit 1
fi
version="$(basename "${core[0]}")"
version="${version#Dexpace.Sdk.Core.}"
version="${version%.nupkg}"

status=0
# expect <case> <exit-code> <output-pattern> <preflight-args...>   (stdout and stderr are both searched)
expect() {
  local name="$1" code="$2" pattern="$3" output actual
  shift 3
  output="$("$dotnet" run scripts/ci/release-preflight.cs -- "$@" 2>&1)"
  actual=$?
  output="${output//$'\r'/}"
  if [ "$actual" -eq "$code" ] && grep -Eq -- "$pattern" <<<"$output"; then
    echo "release-preflight-selftest: ok     $name (exit $actual)"
  else
    echo "release-preflight-selftest: FAILED $name: expected exit $code and /$pattern/, got exit $actual:" >&2
    echo "$output" >&2
    status=1
  fi
}

# A fresh scratch copy of the packages at $work/packages.
fresh_packages() {
  rm -rf "$work/packages" && mkdir -p "$work/packages"
  cp "$packages"/*.nupkg "$packages"/*.snupkg "$work/packages/"
}

# A scratch repository at $work/repo: the committed Directory.Build.props (for the key), a CHANGELOG.md with the version's section, an Unshipped
# file holding `#nullable enable` only, one commit on main tagged v<version>, and a second commit on a side branch tagged v<version>-side.
make_repo() {
  rm -rf "$work/repo" && mkdir -p "$work/repo/src/Dexpace.Sdk.Core"
  cp "$repo/Directory.Build.props" "$work/repo/"
  printf '## [Unreleased]\n\n## [%s] - 2026-01-01\n' "$version" >"$work/repo/CHANGELOG.md"
  printf '#nullable enable\n' >"$work/repo/src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt"
  local g=(git -C "$work/repo" -c user.name=selftest -c user.email=selftest@example.invalid -c commit.gpgsign=false -c tag.gpgsign=false)
  "${g[@]}" init -q -b main &&
    "${g[@]}" add -A &&
    "${g[@]}" commit -q -m settled &&
    "${g[@]}" tag "v$version" &&
    "${g[@]}" checkout -q -b side &&
    "${g[@]}" commit -q --allow-empty -m side &&
    "${g[@]}" tag "v$version-side" &&
    "${g[@]}" checkout -q main || {
    echo "release-preflight-selftest: could not build the scratch repository" >&2
    exit 1
  }
}

# The unaltered packages must pass, or the cases below prove nothing.
expect "all packages, no tag" 0 "release-preflight: ok" "$packages"

fresh_packages
rm -f "$work/packages"/Dexpace.Sdk.Core.[0-9]*.nupkg
expect "a package removed" 1 "Dexpace\.Sdk\.Core has no \.nupkg" "$work/packages"

fresh_packages
rm -f "$work/packages"/Dexpace.Sdk.Core.[0-9]*.snupkg
expect "a symbol package removed" 1 "Dexpace\.Sdk\.Core\.$version\.nupkg has no Dexpace\.Sdk\.Core\.$version\.snupkg beside it" "$work/packages"

fresh_packages
mv "$work/packages/Dexpace.Sdk.Core.$version.nupkg" "$work/packages/Dexpace.Sdk.Core.9.9.9.nupkg"
mv "$work/packages/Dexpace.Sdk.Core.$version.snupkg" "$work/packages/Dexpace.Sdk.Core.9.9.9.snupkg"
expect "one package at another version" 1 "more than one version.*$version.*9\.9\.9" "$work/packages"

expect "an assembly not carrying the expected key" 1 "lib/net10\.0/Dexpace\.Sdk\.Core\.dll in Dexpace\.Sdk\.Core\.$version\.nupkg is signed with token [0-9a-f]{16}, not the committed key's 0000000000000000" "$packages" --expected-token 0000000000000000

make_repo
expect "a v<version> tag on main over a settled copy" 0 "tag v$version on main" "$packages" --repo "$work/repo" --tag "v$version" --main-ref main
expect "a tag that is not the packages' version" 1 "the tag v9\.9\.9 names version 9\.9\.9, but the packages are at $version" "$packages" --repo "$work/repo" --tag v9.9.9 --main-ref main
expect "a tag without the v" 1 "the tag $version is not v<version>" "$packages" --repo "$work/repo" --tag "$version" --main-ref main
expect "a tag commit that is not on main" 1 "not an ancestor of main" "$packages" --repo "$work/repo" --tag "v$version-side" --main-ref main

if [[ "$version" == *-* ]]; then
  expect "the release version for a pre-release" 1 "the tag v${version%%-*} names version ${version%%-*}, but the packages are at $version" "$packages" --repo "$work/repo" --tag "v${version%%-*}" --main-ref main
fi

make_repo
printf '## [Unreleased]\n' >"$work/repo/CHANGELOG.md"
expect "a CHANGELOG.md without the version's section" 1 "CHANGELOG\.md has no \`## \[$version\]\` section" "$packages" --repo "$work/repo" --tag "v$version" --main-ref main

make_repo
printf '#nullable enable\nDexpace.Sdk.Core.Fake.Member() -> void\n' >"$work/repo/src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt"
expect "a non-empty PublicAPI.Unshipped.txt" 1 "PublicAPI\.Unshipped\.txt holds 1 line\(s\) beyond" "$packages" --repo "$work/repo" --tag "v$version" --main-ref main

# The dry run: the same two findings, without a tag, are reported and do not fail the run.
expect "a non-empty PublicAPI.Unshipped.txt, no tag" 0 "would fail: .*PublicAPI\.Unshipped\.txt holds 1 line\(s\)" "$packages" --repo "$work/repo"
make_repo
printf '## [Unreleased]\n' >"$work/repo/CHANGELOG.md"
expect "a CHANGELOG.md without the section, no tag" 0 "would fail: CHANGELOG\.md has no \`## \[$version\]\` section" "$packages" --repo "$work/repo"

rm -rf "$work"
exit "$status"
