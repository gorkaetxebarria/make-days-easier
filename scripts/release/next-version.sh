#!/usr/bin/env bash
# Prints the next version derived from the highest stable vX.Y.Z tag.
# No stable tag yet means the baseline is 0.0.0.
#   next-version.sh <major|minor|patch>        -> X.Y.Z
#   next-version.sh alpha <run-id[.attempt]>   -> X.Y.(Z+1)-alpha.<run-id[.attempt]>
set -euo pipefail

usage() {
  echo "usage: $0 <major|minor|patch> | alpha <run-id[.attempt]>" >&2
  exit 2
}

[[ $# -ge 1 ]] || usage
mode="$1"

latest="$(git tag --list 'v*' \
  | grep -E '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' \
  | sed 's/^v//' \
  | sort -t. -k1,1n -k2,2n -k3,3n \
  | tail -n 1)" || true
latest="${latest:-0.0.0}"

IFS=. read -r major minor patch <<< "$latest"

case "$mode" in
  major) echo "$((major + 1)).0.0" ;;
  minor) echo "${major}.$((minor + 1)).0" ;;
  patch) echo "${major}.${minor}.$((patch + 1))" ;;
  alpha)
    id="${2:-}"
    if [[ ! $id =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
      echo "error: alpha requires a numeric run id (optionally .attempt)" >&2
      exit 2
    fi
    echo "${major}.${minor}.$((patch + 1))-alpha.${id}"
    ;;
  *) usage ;;
esac
