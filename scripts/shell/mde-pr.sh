# mde-pr.sh - Bash and Zsh wrapper for `mde pr`.
#
# Usage: source this file from ~/.bashrc or ~/.zshrc, then run:
#   mde-pr <pull-request-id> [--ide code|vs]
#
# The function runs `mde pr <id> --output json`, parses ONLY the documented
# stdout contract {"schemaVersion":1,"repositoryPath":"<absolute-path>"} and
# changes the current shell's directory to that path after success.
# Diagnostics from mde go to stderr and are shown as-is. On any failure the
# directory is left unchanged and a nonzero status is returned.

mde-pr() {
  if [ "$#" -lt 1 ]; then
    echo "usage: mde-pr <pull-request-id> [--ide code|vs]" >&2
    return 2
  fi

  local pr_id="$1"
  shift

  local output
  output="$(command mde pr "$pr_id" "$@" --output json)" || return $?

  local repository_path
  repository_path="$(printf '%s\n' "$output" | sed -nE 's/^[[:space:]]*\{[[:space:]]*"schemaVersion"[[:space:]]*:[[:space:]]*1[[:space:]]*,[[:space:]]*"repositoryPath"[[:space:]]*:[[:space:]]*"(([^"\\]|\\.)*)"[[:space:]]*\}[[:space:]]*$/\1/p')"
  repository_path="${repository_path//\\\\/\\}"

  if [ -z "$repository_path" ] || [ ! -d "$repository_path" ]; then
    echo "mde-pr: unexpected output from 'mde pr'; directory not changed." >&2
    return 1
  fi

  cd -- "$repository_path" || return $?
}
