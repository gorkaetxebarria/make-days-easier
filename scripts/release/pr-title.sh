#!/usr/bin/env bash
# Validates a PR title against "[Work-Item-ID] type[!]: description".
# On success prints "bump=<major|minor|patch|none>"; otherwise exits 1 with an error on stderr.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <pr-title>" >&2
  exit 2
fi

title="$1"
types='feat|fix|bug|refactor|perf|docs|style|test|build|ci|chore|revert'
pattern="^\[[A-Za-z0-9][A-Za-z0-9._-]*\] (${types})(!)?: [^[:space:]].*$"

if [[ ! $title =~ $pattern ]]; then
  echo "error: PR title must match '[Work-Item-ID] type[!]: description': $title" >&2
  exit 1
fi

type="${BASH_REMATCH[1]}"
breaking="${BASH_REMATCH[2]}"

if [[ -n $breaking ]]; then
  bump="major"
elif [[ $type == feat ]]; then
  bump="minor"
elif [[ $type == fix || $type == bug ]]; then
  bump="patch"
else
  bump="none"
fi

echo "bump=$bump"
