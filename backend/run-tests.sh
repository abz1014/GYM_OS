#!/usr/bin/env bash
# Runs the full backend test suite and fails LOUDLY instead of silently under-reporting.
#
# Root cause this guards against (hit 2026-08-05): `dotnet test` at the solution level still
# runs and prints a normal "Passed!" summary for whichever test projects DID build, even when
# another project fails to build entirely (here: GymOS.Api.IntegrationTests, via its GymOS.API
# dependency, because the API's output DLLs were locked by a live-verify server left running
# from manual browser testing). Piping that output through `tail -N` makes a partial run look
# like a complete one — and in Bash, `dotnet test | tail` also throws away dotnet test's real
# exit code (`$?` after a pipe reflects `tail`, not `dotnet`, without `set -o pipefail`). This
# script closes both gaps: it never pipes dotnet's exit status away, and it explicitly counts
# test-project result lines against the number of test projects that actually exist on disk,
# so a partial run fails the script instead of looking done.

set -o pipefail
cd "$(dirname "$0")" || exit 1

echo "== Step 1: checking for a stray API server on port 5000 (build-lock hazard) =="
API_PID=$(netstat -ano 2>/dev/null | grep -E ':5000[[:space:]].*LISTENING' | awk '{print $NF}' | head -1)
if [ -n "$API_PID" ]; then
  echo "  Found a process listening on :5000 (PID $API_PID) — stopping it before building."
  taskkill //F //PID "$API_PID" 2>/dev/null
  sleep 1
else
  echo "  none running"
fi

echo "== Step 2: dotnet build (must be clean before testing) =="
dotnet build --nologo -v quiet
BUILD_EXIT=$?
if [ "$BUILD_EXIT" -ne 0 ]; then
  echo ""
  echo "BUILD FAILED (exit $BUILD_EXIT) — aborting before running any tests. Re-run 'dotnet build' without -v quiet to see errors." >&2
  exit "$BUILD_EXIT"
fi

echo "== Step 3: dotnet test =="
LOG_FILE=$(mktemp)
dotnet test --nologo -v quiet > "$LOG_FILE" 2>&1
TEST_EXIT=$?
cat "$LOG_FILE"

EXPECTED_COUNT=$(find tests -mindepth 1 -maxdepth 1 -type d | while read -r d; do find "$d" -maxdepth 1 -iname "*.csproj" | head -1; done | grep -c .)
REPORTED_COUNT=$(grep -cE '^(Passed|Failed)!.*Total:' "$LOG_FILE")
TOTAL_FAILED=$(grep -oE 'Failed:[[:space:]]*[0-9]+' "$LOG_FILE" | grep -oE '[0-9]+' | awk '{s+=$1} END {print s+0}')
TOTAL_PASSED=$(grep -oE ', Passed:[[:space:]]*[0-9]+' "$LOG_FILE" | grep -oE '[0-9]+' | awk '{s+=$1} END {print s+0}')

echo ""
echo "== Result =="
echo "Test projects found under tests/:       $EXPECTED_COUNT"
echo "Test projects that reported a result:   $REPORTED_COUNT"

rm -f "$LOG_FILE"

if [ "$REPORTED_COUNT" -lt "$EXPECTED_COUNT" ]; then
  echo ""
  echo "FAILURE: only $REPORTED_COUNT of $EXPECTED_COUNT test projects reported a result." >&2
  echo "This is the silent-partial-run trap — a project almost certainly failed to BUILD (see output above)." >&2
  exit 1
fi

if [ "$TOTAL_FAILED" -gt 0 ] || [ "$TEST_EXIT" -ne 0 ]; then
  echo ""
  echo "FAILURE: $TOTAL_FAILED failing test(s) across $REPORTED_COUNT projects (dotnet test exit $TEST_EXIT)." >&2
  exit 1
fi

echo ""
echo "ALL GREEN: $TOTAL_PASSED passed across $REPORTED_COUNT test projects."
exit 0
