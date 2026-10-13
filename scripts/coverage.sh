#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet tool restore

# Only merge this run's results, so old runs cannot inflate coverage.
mkdir -p coverage/raw
coverage_run_dir="$(mktemp -d "$PWD/coverage/raw/run.XXXXXX")"
dotnet test Yodaphone.sln \
  --collect:"XPlat Code Coverage" \
  --settings coverage.runsettings \
  --results-directory "$coverage_run_dir"

dotnet tool run reportgenerator \
  "-reports:$coverage_run_dir/**/coverage.cobertura.xml" \
  "-targetdir:coverage/report" \
  "-reporttypes:Html;Cobertura;TextSummary"

cat coverage/report/Summary.txt
printf '\nHTML report: %s/coverage/report/index.html\n' "$PWD"
