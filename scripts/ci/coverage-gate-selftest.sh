#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# Proves NFR-5's gate fails closed (issue #33): runs scripts/ci/coverage-gate.cs over altered copies of the reports a
# real test run left in <results-dir>, and requires
#   - each test project's report removed in turn: exit 3, naming that test project;
#   - one report per test project that measures Dexpace.Sdk.Core alone: exit 3, naming a library with no data;
#   - a second, stale report beside each test project's own in turn: exit 3, naming that test project;
#   - a floor above any measurement (100.01%): exit 1;
#   - an empty results directory: exit 2.
# Nothing is hard-coded but Dexpace.Sdk.Core: the test projects are read from the report names, so a new suite or
# library is covered as it lands. Run from anywhere after the CI test step. Exit 0 when every case holds, 1 otherwise.
#
#   scripts/ci/coverage-gate-selftest.sh [results-dir]      (default: artifacts/test-results)
set -uo pipefail
shopt -s nullglob
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
results="$(cd "${1:-$repo/artifacts/test-results}" && pwd)"
dotnet="${DOTNET:-dotnet}"
work="$repo/artifacts/coverage-gate-selftest"
cd "$repo" || exit 1

reports=("$results"/*.coverage.cobertura.*.xml)
if [ ${#reports[@]} -lt 2 ]; then
  echo "coverage-gate-selftest: need the reports of at least two test projects under $results" >&2
  exit 1
fi

status=0
# expect <case> <exit-code> <stderr-pattern> <gate-args...>
expect() {
  local name="$1" code="$2" pattern="$3"
  shift 3
  local output actual
  output="$("$dotnet" run scripts/ci/coverage-gate.cs -- "$@" 2>&1)"
  actual=$?
  output="${output//$'\r'/}"
  if [ "$actual" -eq "$code" ] && grep -Eq -- "$pattern" <<<"$output"; then
    echo "coverage-gate-selftest: ok     $name (exit $actual)"
  else
    echo "coverage-gate-selftest: FAILED $name: expected exit $code and /$pattern/, got exit $actual:" >&2
    echo "$output" >&2
    status=1
  fi
}

# The unaltered reports must pass, or the cases below prove nothing.
expect "all reports present" 0 ": ok$" "$results" 80

for report in "${reports[@]}"; do
  project="$(basename "$report")"
  project="${project%%.coverage.cobertura.*}"
  rm -rf "$work" && mkdir -p "$work"
  for other in "${reports[@]}"; do
    [ "$other" = "$report" ] || cp "$other" "$work/"
  done
  expect "without $project's report" 3 "test project $project left no report" "$work" 80
done

for report in "${reports[@]}"; do
  project="$(basename "$report")"
  project="${project%%.coverage.cobertura.*}"
  rm -rf "$work" && mkdir -p "$work"
  cp "${reports[@]}" "$work/"
  cp "$report" "$work/$project.coverage.cobertura.000000000000000.xml"
  expect "a stale second report of $project" 3 "test project $project has 2 reports, expected one" "$work" 80
done

rm -rf "$work" && mkdir -p "$work"
for report in "${reports[@]}"; do
  project="$(basename "$report")"
  project="${project%%.coverage.cobertura.*}"
  cat >"$work/$project.coverage.cobertura.0.xml" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<coverage><sources><source>src/Dexpace.Sdk.Core/</source></sources><packages>
<package name="Dexpace.Sdk.Core"><classes><class filename="Fixture.cs"><lines>
<line number="1" hits="1" /></lines></class></classes></package></packages></coverage>
XML
done
expect "a library with no coverage data" 3 "library [^ ]+ has no coverage data" "$work" 80

expect "floor above the measurement" 1 ": FAILED$" "$results" 100.01

rm -rf "$work" && mkdir -p "$work"
expect "empty results directory" 2 "no \*coverage\.cobertura\*\.xml under" "$work" 80

rm -rf "$work"
exit "$status"
