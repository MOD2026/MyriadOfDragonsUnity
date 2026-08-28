#!/usr/bin/env bash
# Canonical Linux equivalent of the Windows EditMode test command in docs/AI_CONTRIBUTING.md §5.
# Runs the full EditMode suite headlessly. Requires an activated Unity license (see .cursor/start.sh).
#
# Deliberately does NOT pass -quit: with -runTests, adding -quit makes the run silently do nothing
# (documented gotcha in docs/START_HERE.md §2).
set -uo pipefail

UNITY_VERSION="6000.5.6f1"
UNITY_BIN="/opt/unity/${UNITY_VERSION}/Editor/Unity"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS="${REPO_ROOT}/results.xml"
LOG="${REPO_ROOT}/run.log"

if [ ! -x "${UNITY_BIN}" ]; then
  echo "Unity not installed. Run .cursor/install.sh first." >&2
  exit 1
fi

rm -f "${RESULTS}" "${LOG}"
xvfb-run -a "${UNITY_BIN}" -batchmode -nographics \
  -projectPath "${REPO_ROOT}" \
  -runTests -testPlatform EditMode \
  -testResults "${RESULTS}" -logFile "${LOG}"
UNITY_EXIT=$?

echo "Unity exit code: ${UNITY_EXIT}"
if grep -Eq "error CS|Aborting batchmode" "${LOG}" 2>/dev/null; then
  echo "COMPILE ERRORS DETECTED — no tests ran. First errors:" >&2
  grep -E "error CS|Aborting batchmode" "${LOG}" | head -20 >&2
  exit 1
fi
if [ -f "${RESULTS}" ]; then
  echo "Results written to ${RESULTS}"
  grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' "${RESULTS}" | head -1 || true
fi
exit ${UNITY_EXIT}
