#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# Proves the dependency audit's allowance for core is a FIXED SET (phase 11, task 1.3; SEAM-1, NFR-1; design §10 entry 1), not "whatever the logging
# facade pulls in": scripts/ci/dependency-audit.cs allows core the logging facade and the one package its own closure carries today
# (Microsoft.Extensions.DependencyInjection.Abstractions, band-matched), so a dependency the facade grows later fails the audit instead of passing it.
# Runs the audit, from a scratch tree of copied deps.json files over the real packed nuspecs, and requires
#   - the unaltered copies: exit 0 (or the cases below prove nothing);
#   - a package injected under the facade's dependencies in core's deps.json (Some.Facade.Dependency): exit 1, naming it;
#   - a package injected under DependencyInjection.Abstractions' dependencies: exit 1, naming it;
#   - a package injected into the libraries with no edge to it at all: exit 1, naming it.
# Run from anywhere after `dotnet build` and `dotnet pack` (the audit's own inputs). Exit 0 when every case holds, 1 otherwise.
#
#   scripts/ci/dependency-audit-selftest.sh [package-dir]      (default: artifacts/packages; the configuration is Release)
set -uo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
packages="$(cd "${1:-$repo/artifacts/packages}" && pwd)"
dotnet="${DOTNET:-dotnet}"
configuration="Release"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

status=0
core="Dexpace.Sdk.Core"
target="net10.0"

# tree <name> [<injected-package> <edge-owner-pattern>]: a scratch tree holding a copy of every library's deps.json at the path the audit reads. When an
# injected package is named, core's deps.json gets it as a package of the runtime closure, and (unless the owner pattern is empty) as a dependency of the
# target entry whose key matches the pattern.
tree() {
  local dir="$work/$1" injected="${2-}" owner="${3-}" library source
  for library in "$repo"/src/*/; do
    library="$(basename "$library")"
    source="$repo/src/$library/bin/$configuration/$target/$library.deps.json"
    if [ ! -f "$source" ]; then
      echo "dependency-audit-selftest: $source is missing; build $configuration first" >&2
      exit 2
    fi
    mkdir -p "$dir/src/$library/bin/$configuration/$target"
    cp "$source" "$dir/src/$library/bin/$configuration/$target/$library.deps.json"
  done
  if [ -n "$injected" ]; then
    local file="$dir/src/$core/bin/$configuration/$target/$core.deps.json"
    awk -v pkg="$injected" -v owner="$owner" '
      # The line after the owner target entry'"'"'s header: join its "dependencies" block, or open one when the entry (a leaf) has none.
      afterheader {
        afterheader = 0
        match($0, /^ */)
        indent = substr($0, 1, RLENGTH)
        if ($0 ~ /"dependencies": \{/) {
          print
          print indent "  \"" pkg "\": \"1.0.0\","
        } else {
          print indent "\"dependencies\": { \"" pkg "\": \"1.0.0\" },"
          print
        }
        next
      }
      # The package, as a library of type package (the audit reads the closure from "libraries") and as a target entry.
      /"targets": \{/ { intargets = 1 }
      /"libraries": \{/ {
        intargets = 0
        print
        print "    \"" pkg "/1.0.0\": { \"type\": \"package\", \"serviceable\": true, \"sha512\": \"sha512-x\", \"path\": \"p/1.0.0\", \"hashPath\": \"p.1.0.0.nupkg.sha512\" },"
        next
      }
      /"\.NETCoreApp,Version=v10\.0": \{/ {
        print
        print "      \"" pkg "/1.0.0\": { \"runtime\": { \"lib/net10.0/x.dll\": {} } },"
        next
      }
      # The edge: the injected package becomes a dependency of the target entry whose key starts with the owner.
      owner != "" && intargets && index($0, "\"" owner "/") > 0 && $0 ~ /": \{ *$/ { afterheader = 1 }
      { print }
    ' "$file" >"$file.new" && mv "$file.new" "$file"
  fi
  echo "$dir"
}

# expect <case> <exit-code> <output-pattern> <tree>
expect() {
  local name="$1" code="$2" pattern="$3" root="$4" output actual
  output="$(cd "$root" && "$dotnet" run "$repo/scripts/ci/dependency-audit.cs" -- "$configuration" "$packages" 2>&1)"
  actual=$?
  output="${output//$'\r'/}"
  if [ "$actual" -eq "$code" ] && grep -Eq -- "$pattern" <<<"$output"; then
    echo "dependency-audit-selftest: ok     $name (exit $actual)"
  else
    echo "dependency-audit-selftest: FAILED $name: wanted exit $code matching '$pattern', got exit $actual:" >&2
    echo "$output" | sed 's/^/    /' >&2
    status=1
  fi
}

expect "unaltered copies of every deps.json" 0 "libraries conform" "$(tree clean)"
expect "a package under the facade's dependencies" 1 "VIOLATION: .*Some\.Facade\.Dependency" "$(tree under-facade Some.Facade.Dependency Microsoft.Extensions.Logging.Abstractions)"
expect "a package under DependencyInjection.Abstractions' dependencies" 1 "VIOLATION: .*Some\.Deeper\.Dependency" "$(tree under-di-abstractions Some.Deeper.Dependency Microsoft.Extensions.DependencyInjection.Abstractions)"
expect "a package in the closure with no edge to it" 1 "VIOLATION: .*Some\.Unreached\.Dependency" "$(tree unreached Some.Unreached.Dependency '')"

exit $status
