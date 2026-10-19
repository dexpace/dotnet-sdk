#!/usr/bin/env bash
# Copyright (c) 2026 dexpace and Omar Aljarrah.
# Licensed under the MIT License. See LICENSE in the repository root for details.
#
# Proves the appendix-C coverage check fails closed (phase 10, P10-20, plan reading R9): scripts/ci/requirement-coverage.cs has no test project, so this
# builds fixture trees (a miniature appendix C and two checklists) in a temporary directory and requires
#   - a clean fixture: exit 0;
#   - an appendix-C MUST with no owning row: exit 1 naming it; a SHOULD with none: reported, exit 0;
#   - an owning row naming an unknown ID: exit 1; a row whose level disagrees: exit 1; a split level ("MUST (+SHOULD log)") that agrees: exit 0;
#   - an owning row with no legend mark: exit 1;
#   - a carried ID whose earlier row has no hourglass: exit 1; with one: exit 0;
#   - a row in a contributed-evidence table (header "ID (owner)") owns nothing;
#   - a missing appendix, no checklist, or a bad flag: exit 2; --report over a broken fixture: exit 0.
# Run from anywhere. Exit 0 when every case holds, 1 otherwise.
#
#   scripts/ci/requirement-coverage-selftest.sh
set -uo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet="${DOTNET:-dotnet}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$repo" || exit 1

appendix="docs/product-spec/appendix-c-consolidated-normative-requirement-index.md"
status=0

# fixture <name> [a-rows] [b-rows]: a tree with a miniature appendix C and two checklists (phase 2a, phase 3a).
# The default rows own AAA-1, AAA-2 (a) and BBB-1, BBB-2 (b).
fixture() {
  local dir="$work/$1" a="${2-}" b="${3-}"
  mkdir -p "$dir/docs/product-spec" "$dir/docs/work/mvp/phase2/phase2a" "$dir/docs/work/mvp/phase3/phase3a"
  {
    echo '| ID | Level | Topic | Text |'
    echo '|---|---|---|---|'
    echo '| AAA-1 | MUST | t | one |'
    echo '| AAA-2 | MUST | t | two |'
    echo '| BBB-1 | SHOULD | t | three |'
    echo '| BBB-2 | MUST | t | four |'
  } >"$dir/$appendix"
  [ -z "$a" ] && a='| `AAA-1` | MUST | ✅ | built |
| `AAA-2` | MUST | ✅ | built |'
  [ -z "$b" ] && b='| `BBB-1` | SHOULD | ✅ | built |
| `BBB-2` | MUST | ✅ | built |'
  printf '%s\n%s\n%s\n' '| ID | Level | Mark | Evidence |' '|---|---|---|---|' "$a" >"$dir/docs/work/mvp/phase2/phase2a/a-checklist.md"
  printf '%s\n%s\n%s\n' '| ID | Level | Mark | Evidence |' '|---|---|---|---|' "$b" >"$dir/docs/work/mvp/phase3/phase3a/b-checklist.md"
  echo "$dir"
}

# expect <case> <exit-code> <output-pattern> <root> <script-args...>
expect() {
  local name="$1" code="$2" pattern="$3" root="$4"
  shift 4
  local output actual
  output="$("$dotnet" run scripts/ci/requirement-coverage.cs -- "$@" --root "$root" 2>&1)"
  actual=$?
  output="${output//$'\r'/}"
  if [ "$actual" -eq "$code" ] && grep -Eq -- "$pattern" <<<"$output"; then
    echo "requirement-coverage-selftest: ok     $name (exit $actual)"
  else
    echo "requirement-coverage-selftest: FAILED $name: wanted exit $code matching '$pattern', got exit $actual"
    echo "$output" | sed 's/^/    /'
    status=1
  fi
}

expect "a clean fixture" 0 "4 of 4 IDs owned" "$(fixture clean)" --check
expect "a MUST with no owning row" 1 "unowned +BBB-2" "$(fixture unowned-must '' '| `BBB-1` | SHOULD | ✅ | built |')" --check
expect "a SHOULD with no owning row is reported but does not fail" 0 "unowned +BBB-1" "$(fixture unowned-should '' '| `BBB-2` | MUST | ✅ | built |')" --check
expect "an owning row naming an unknown ID" 1 "unknown +ZZZ-9" "$(fixture unknown '| `AAA-1` | MUST | ✅ | built |
| `AAA-2` | MUST | ✅ | built |
| `ZZZ-9` | MUST | ✅ | built |')" --check
expect "a level cell that disagrees with appendix C" 1 "level +AAA-2" "$(fixture level '| `AAA-1` | MUST | ✅ | built |
| `AAA-2` | SHOULD | ✅ | built |')" --check
expect "a split level that agrees" 0 "4 of 4 IDs owned" "$(fixture split '| `AAA-1` | MUST (+SHOULD log) | ✅ | built |
| `AAA-2` | MUST | ✅ | built |')" --check
expect "an owning row with no legend mark" 1 "mark +AAA-1" "$(fixture mark '| `AAA-1` | MUST | built | built |
| `AAA-2` | MUST | ✅ | built |')" --check
expect "a carried ID whose earlier row has no hourglass" 1 "carried +AAA-1" "$(fixture carried '' '| `AAA-1` | MUST | ✅ | carried |
| `BBB-1` | SHOULD | ✅ | built |
| `BBB-2` | MUST | ✅ | built |')" --check
expect "a carried ID whose earlier row defers" 0 "4 of 4 IDs owned" "$(fixture carried-ok '| `AAA-1` | MUST | ⏳ 3a | later |
| `AAA-2` | MUST | ✅ | built |' '| `AAA-1` | MUST | ✅ | carried |
| `BBB-1` | SHOULD | ✅ | built |
| `BBB-2` | MUST | ✅ | built |')" --check
contributed="$(fixture contributed '| `AAA-2` | MUST | ✅ | built |')"
printf '\n%s\n%s\n%s\n' '| ID (owner) | Work | Evidence |' '|---|---|---|' '| `AAA-1` (a) | helped | x |' >>"$contributed/docs/work/mvp/phase2/phase2a/a-checklist.md"
expect "a contributed-evidence table owns nothing" 1 "unowned +AAA-1" "$contributed" --check
nothing="$(fixture no-appendix)"
rm "$nothing/$appendix"
expect "a missing appendix" 2 "was not found" "$nothing" --check
none="$(fixture no-checklist)"
rm -r "$none/docs/work/mvp/phase2" "$none/docs/work/mvp/phase3"
expect "no checklist" 2 "no checklist" "$none" --check
expect "a bad flag" 2 "usage" "$(fixture flag)" --bogus
expect "--report over a broken fixture" 0 "unowned +BBB-2" "$(fixture report '' '| `BBB-1` | SHOULD | ✅ | built |')" --report

exit $status
