#!/usr/bin/env bash
set -euo pipefail

log(){ printf '[%s] %s\n' "$(date +%H:%M:%S)" "$*"; }

# Required env
: "${PGUSER:=postgres}"
: "${PGPASSWORD:?PGPASSWORD must be set}"
: "${APP_DB:=mfdb}"
: "${MIG_GLOB:=*.sql}"

# Optional env
: "${LEADER_CANDIDATES:=patroni1 patroni2 patroni3}"
: "${RW_FALLBACK_HOST:=haproxy}"
: "${RW_FALLBACK_PORT:=5000}"

# For libpq
export PGUSER PGPASSWORD PGAPPNAME=db-migrator PGCONNECT_TIMEOUT=5

# ---- 1) Detect the leader directly (avoid HAProxy for DDL) -------------------
detect_leader_psql() {
  for n in ${LEADER_CANDIDATES}; do
    # Use sslmode=prefer for detection so this works with or without TLS on the node
    local st
    st=$(psql "host=${n} port=5432 dbname=postgres user=${PGUSER} sslmode=prefer connect_timeout=2" \
          -Atqc "select case when pg_is_in_recovery() then 'standby' else 'primary' end" 2>/dev/null || true)
    if [ "$st" = "primary" ]; then
      echo "$n"
      return 0
    fi
  done
  return 1
}

PGHOST="$(detect_leader_psql || true)"
if [ -n "${PGHOST}" ]; then
  PGPORT=5432
  log "Detected Patroni leader directly: ${PGHOST}:${PGPORT}"
else
  # Fallback to HAProxy RW if direct detection fails (still verify it's primary)
  PGHOST="${RW_FALLBACK_HOST}"
  PGPORT="${RW_FALLBACK_PORT}"
  log "WARN: direct leader detection failed; falling back to ${PGHOST}:${PGPORT}"
fi
export PGHOST PGPORT

# ---- 2) Wait for leader and verify role --------------------------------------
log "Waiting for ${PGHOST}:${PGPORT}…"
for i in {1..60}; do
  if psql -d postgres -Atqc "select 1" >/dev/null 2>&1; then break; fi
  log "…still waiting ($i/60)"; sleep 2
done

STATE=$(psql -d postgres -Atqc "select case when pg_is_in_recovery() then 'standby' else 'primary' end" || true)
if [ "$STATE" != "primary" ]; then
  log "ERROR: connected to standby at ${PGHOST}:${PGPORT} (state=$STATE)"
  exit 1
fi
log "Connected to PRIMARY at ${PGHOST}:${PGPORT}"

# ---- 3) Ensure database exists (race-safe, single-host) ----------------------
log "Ensuring database '${APP_DB}' exists…"
psql -d postgres -v "dbname=${APP_DB}" -v "dbowner=${PGUSER}" <<'SQL'
\set ON_ERROR_STOP 1
SELECT pg_advisory_lock(hashtext(:'dbname'));

-- Emit CREATE DATABASE only if missing (runs outside a txn via \gexec)
SELECT format('CREATE DATABASE %I OWNER %I', :'dbname', :'dbowner')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'dbname');
\gexec

SELECT pg_advisory_unlock(hashtext(:'dbname'));
SQL

# ---- 4) Wait until that DB is reachable on THIS primary ----------------------
log "Waiting until '${APP_DB}' is reachable on PRIMARY…"
for i in {1..60}; do
  if psql -d "${APP_DB}" -Atqc "select 1" >/dev/null 2>&1; then
    s2=$(psql -d "${APP_DB}" -Atqc "select case when pg_is_in_recovery() then 'standby' else 'primary' end" 2>/dev/null || true)
    [ "$s2" = "primary" ] && { log "'${APP_DB}' reachable on PRIMARY."; break; }
  fi
  log "…not yet ($i/60)"; sleep 2
done

# ---- 5) Extensions (idempotent) ----------------------------------------------
psql -d "${APP_DB}" -v ON_ERROR_STOP=1 -c "create extension if not exists pgcrypto;" || true
psql -d "${APP_DB}" -v ON_ERROR_STOP=1 -c "create extension if not exists \"uuid-ossp\";" || true

# ---- 6) Migrations -----------------------------------------------------------
log "Listing /migrations …"; ls -la /migrations || true

# Build a sorted list; keep NUL-safety for filenames
mapfile -d '' files < <(find /migrations -type f -name "${MIG_GLOB}" -print0 | sort -z)
log "Found ${#files[@]} files:"; for f in "${files[@]}"; do printf '  - %s\n' "$f"; done
if [ ${#files[@]} -eq 0 ]; then
  log "No migration files found."
  touch /tmp/migrations.done
  exit 0
fi

driver="$(mktemp)"
{
  printf '%s\n' '\set ON_ERROR_STOP 1'
  printf '%s\n' '\set ECHO all'
  printf '%s\n' "\connect ${APP_DB}"
  for f in "${files[@]}"; do
    esc=${f//\'/\'\'}
    printf '%s\n' "\\echo '>> applying ${esc}'"
    printf '%s\n' "\\i '${esc}'"
  done
} > "$driver"

log "Executing migrations on ${PGHOST}:${PGPORT}/${APP_DB} (PRIMARY)…"
psql -d postgres -v ON_ERROR_STOP=1 -f "$driver"

rm -f "$driver"
log "Migrations complete."
touch /tmp/migrations.done
exit 0


