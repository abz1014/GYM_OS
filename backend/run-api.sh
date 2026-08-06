#!/usr/bin/env bash
# Start the GymOS API reliably, the way run-tests.sh runs the suite reliably.
#
# The API gets stopped constantly during development and for good reasons: run-tests.sh kills port
# 5000 before building (a stale server once held the DLLs and made a test run report a false pass),
# `dotnet build` fails with a file lock while it is running, and `dotnet ef database drop` needs the
# connections closed. Restarting it was a long nohup line typed from memory each time, which is why
# it kept ending up down while the Vite frontend — supervised via .claude/launch.json — stayed up.
#
# Usage:  backend/run-api.sh          start (rebuilds nothing; use after a build)
#         backend/run-api.sh --stop   just stop whatever is on the port
set -uo pipefail

PORT=5000
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DLL="$ROOT/src/GymOS.API/bin/Debug/net10.0/GymOS.API.dll"
LOG="${TMPDIR:-/tmp}/gymos-api.log"

export GYMOS_DB_CONNECTION="${GYMOS_DB_CONNECTION:-Host=localhost;Port=5432;Database=gymos_dev;Username=postgres;Password=gymos_dev_pw}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

stop_existing() {
  local pid
  pid=$(netstat -ano 2>/dev/null | grep ":$PORT" | grep LISTENING | head -1 | awk '{print $NF}')
  if [ -n "${pid:-}" ]; then
    echo "  stopping existing API on :$PORT (PID $pid)"
    taskkill //F //PID "$pid" >/dev/null 2>&1 || kill -9 "$pid" 2>/dev/null
    sleep 2
  fi
}

stop_existing
if [ "${1:-}" = "--stop" ]; then
  echo "API stopped."
  exit 0
fi

if [ ! -f "$DLL" ]; then
  echo "ERROR: $DLL not found — run 'dotnet build' in backend/ first." >&2
  exit 1
fi

# Postgres has no Windows service here, so a down database looks like a hanging API rather than a
# clear error. Say so up front instead of leaving it to be diagnosed later.
if ! (netstat -ano 2>/dev/null | grep -q ":5432.*LISTENING"); then
  echo "WARNING: nothing is listening on 5432 — Postgres is down, so the API will start but every" >&2
  echo "         request will fail. Start it with postgres.exe -D '<data dir>' first." >&2
fi

echo "== starting API =="
cd "$ROOT/src/GymOS.API" || exit 1
nohup dotnet "$DLL" > "$LOG" 2>&1 &

for _ in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$PORT/health" 2>/dev/null)
  if [ "$code" = "200" ]; then
    echo "API healthy on http://localhost:$PORT  (log: $LOG)"
    exit 0
  fi
  sleep 1
done

echo "ERROR: API did not become healthy within 30s. Last 20 log lines:" >&2
tail -20 "$LOG" >&2
exit 1
