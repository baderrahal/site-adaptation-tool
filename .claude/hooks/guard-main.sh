#!/usr/bin/env bash
# R18. Blocks git commit on main and any git push from main or to main. Reads the hook JSON on stdin.
# The branch is the branch of the repo each git command acts on: the folder it runs in after any cd or
# Set-Location earlier in the same command, or the path given with git -C. The folder the session started in does not
# count, so a commit on a branch in any clone or worktree passes and a commit on main anywhere is blocked.
# GUARD_BRANCH, when set, stands in for the branch of every folder, for the tests.
export LC_ALL=C
input=$(cat)

# Most commands never commit or push, they pass at once
if [[ "$input" != *commit* && "$input" != *push* ]]
then
  exit 0
fi

semi=$'\x3b'
bs=$'\x5c'
dq='"'
sq="'"
nl=$'\n'

deny() {
  echo "Blocked by .claude/hooks/guard-main.sh: $1. Work on a branch and merge by pull request." >&2
  exit 2
}

# The text of the first JSON string value of a key, with its escapes undone. Empty when the key is not there.
# A key inside another string has its quotes escaped, so the match must not follow a backslash
json_value() {
  local pattern="(^|[^${bs}${bs}])${dq}$1${dq}[[:space:]]*:[[:space:]]*${dq}(([^${dq}${bs}${bs}]|${bs}${bs}.)*)${dq}"
  if [[ "$input" =~ $pattern ]]
  then
    local value="${BASH_REMATCH[2]}" keep=$'\x01'
    value="${value//"$bs$bs"/$keep}"
    value="${value//"$bs$dq"/$dq}"
    value="${value//"${bs}n"/$nl}"
    value="${value//"${bs}t"/ }"
    value="${value//"${bs}r"/ }"
    value="${value//"${bs}/"//}"
    printf '%s' "${value//$keep/$bs}"
  fi
}

# A folder in the form git takes, with forward slashes, resolved against the folder before it, into FOLDER.
# It sets a variable and prints nothing, since a subshell per call costs a process start on Windows
folder_of() {
  local base="$1" path="${2//$bs//}"
  if [[ -z "$path" || "$path" == "~" ]]
  then
    FOLDER="${HOME:-$base}"
  elif [[ "$path" == "~/"* ]]
  then
    FOLDER="${HOME:-$base}/${path:2}"
  elif [[ "$path" == /* || "$path" =~ ^[A-Za-z]: ]]
  then
    FOLDER="$path"
  else
    FOLDER="${base%/}/$path"
  fi
}

branch_of() {
  if [[ -n "${GUARD_BRANCH+set}" ]]
  then
    printf '%s' "$GUARD_BRANCH"
  else
    git -C "$1" branch --show-current 2>/dev/null
  fi
}

# Splits a command into SEGMENTS on && || | & the semicolon and new lines, outside quotes. It takes a run of plain
# characters at a time, so a long command costs a few steps and not one step per character
split_segments() {
  local text="$1" part="" quote="" plain="^([^${sq}${dq}${semi}|&${nl}]*)(.?)" single="^([^${sq}]*)(${sq}?)" double="^(([^${dq}${bs}${bs}]|${bs}${bs}.)*)(${dq}?)"
  SEGMENTS=()
  while [[ -n "$text" ]]
  do
    if [[ "$quote" == "$sq" ]]
    then
      [[ "$text" =~ $single ]]
      part+="${BASH_REMATCH[0]}"
      [[ -n "${BASH_REMATCH[2]}" ]] && quote=""
    elif [[ "$quote" == "$dq" ]]
    then
      [[ "$text" =~ $double ]]
      part+="${BASH_REMATCH[0]}"
      [[ -n "${BASH_REMATCH[3]}" ]] && quote=""
    else
      [[ "$text" =~ $plain ]]
      part+="${BASH_REMATCH[1]}"
      if [[ "${BASH_REMATCH[2]}" == "$sq" || "${BASH_REMATCH[2]}" == "$dq" ]]
      then
        quote="${BASH_REMATCH[2]}"
        part+="$quote"
      elif [[ -n "${BASH_REMATCH[2]}" ]]
      then
        SEGMENTS+=("$part")
        part=""
      fi
    fi
    if (( ${#BASH_REMATCH[0]} == 0 ))
    then
      break
    fi
    text="${text:${#BASH_REMATCH[0]}}"
  done
  SEGMENTS+=("$part")
}

# Splits a segment into WORDS with the quotes taken off, a run of characters at a time
split_words() {
  local text="$1" word="" have=0 quote="" piece
  local plain="^([^${sq}${dq}[:space:]]*)(.?)" single="^([^${sq}]*)(${sq}?)" double="^(([^${dq}${bs}${bs}]|${bs}${bs}.)*)(${dq}?)"
  WORDS=()
  while [[ -n "$text" ]]
  do
    if [[ "$quote" == "$sq" ]]
    then
      [[ "$text" =~ $single ]]
      word+="${BASH_REMATCH[1]}"
      [[ -n "${BASH_REMATCH[2]}" ]] && quote=""
    elif [[ "$quote" == "$dq" ]]
    then
      [[ "$text" =~ $double ]]
      piece="${BASH_REMATCH[1]}"
      piece="${piece//"$bs$dq"/$dq}"
      word+="${piece//"$bs$bs"/$bs}"
      [[ -n "${BASH_REMATCH[3]}" ]] && quote=""
    else
      [[ "$text" =~ $plain ]]
      word+="${BASH_REMATCH[1]}"
      [[ -n "${BASH_REMATCH[1]}" ]] && have=1
      if [[ "${BASH_REMATCH[2]}" == "$sq" || "${BASH_REMATCH[2]}" == "$dq" ]]
      then
        quote="${BASH_REMATCH[2]}"
        have=1
      elif [[ -n "${BASH_REMATCH[2]}" ]]
      then
        if (( have ))
        then
          WORDS+=("$word")
        fi
        word=""
        have=0
      fi
    fi
    if (( ${#BASH_REMATCH[0]} == 0 ))
    then
      break
    fi
    text="${text:${#BASH_REMATCH[0]}}"
  done
  if (( have ))
  then
    WORDS+=("$word")
  fi
}

# One git command in the folder given, with the words after git
check_git() {
  local dir="$1"
  shift
  local words=("$@") k=0 sub="" word branch deleting=0
  while (( k < ${#words[@]} ))
  do
    word="${words[k]}"
    if [[ "$word" == "-C" ]]
    then
      folder_of "$dir" "${words[k+1]}"
      dir="$FOLDER"
      k=$((k + 2))
    elif [[ "$word" == "-c" ]]
    then
      k=$((k + 2))
    elif [[ "$word" == -* ]]
    then
      k=$((k + 1))
    else
      sub="$word"
      k=$((k + 1))
      break
    fi
  done
  if [[ "$sub" == "commit" ]]
  then
    branch=$(branch_of "$dir")
    if [[ "$branch" == "main" || "$branch" == "master" ]]
    then
      deny "git commit on $branch in $dir"
    fi
  elif [[ "$sub" == "push" ]]
  then
    for word in "${words[@]:k}"
    do
      if [[ "$word" == "--delete" || "$word" == "-d" ]]
      then
        deleting=1
      fi
      if [[ "$word" =~ ^\+?((HEAD|[^:]*):)?(refs/heads/)?(main|master)$ ]]
      then
        deny "git push to main"
      fi
    done
    branch=$(branch_of "$dir")
    if (( ! deleting )) && [[ "$branch" == "main" || "$branch" == "master" ]]
    then
      deny "git push from $branch in $dir"
    fi
  fi
}

# Walks a command segment by segment and follows the folder through cd and Set-Location. A command handed to
# bash -c or powershell -Command is walked the same way, so a git command inside it is checked too
check_command() {
  local dir="$1" text="$2" depth="${3:-0}" segment k j name target
  if (( depth > 3 ))
  then
    return
  fi
  split_segments "$text"
  local segments=("${SEGMENTS[@]}")
  for segment in "${segments[@]}"
  do
    # Only a segment that may run git, change the folder or start a shell can matter
    if [[ ! "$segment" =~ $worth ]]
    then
      continue
    fi
    split_words "$segment"
    local words=("${WORDS[@]}")
    k=0
    # Assignments before a command and the PowerShell call operator are not the command
    while (( k < ${#words[@]} )) && [[ "${words[k]}" == "&" || "${words[k]}" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]]
    do
      k=$((k + 1))
    done
    if (( k >= ${#words[@]} ))
    then
      continue
    fi
    name="${words[k]##*/}"
    name="${name##*$bs}"
    name="${name,,}"
    name="${name%.exe}"
    if [[ "$name" =~ ^(cd|chdir|pushd|set-location|sl|push-location)$ ]]
    then
      j=$((k + 1))
      target=""
      while (( j < ${#words[@]} ))
      do
        if [[ "${words[j]}" =~ ^-(path|literalpath|Path|LiteralPath)$ ]]
        then
          target="${words[j+1]}"
          break
        elif [[ "${words[j]}" == -* ]]
        then
          j=$((j + 1))
        else
          target="${words[j]}"
          break
        fi
      done
      folder_of "$dir" "$target"
      dir="$FOLDER"
    elif [[ "$name" == "git" ]]
    then
      check_git "$dir" "${words[@]:k+1}"
    elif [[ "$name" =~ ^(bash|sh|pwsh|powershell|cmd)$ ]]
    then
      j=$((k + 1))
      while (( j < ${#words[@]} ))
      do
        if [[ "${words[j]}" =~ ^(-c|-Command|-command|/c|/C)$ ]]
        then
          check_command "$dir" "${words[j+1]}" $((depth + 1))
          break
        fi
        j=$((j + 1))
      done
    fi
  done
}

worth="(^|[[:space:]${sq}${dq}/${bs}${bs}])(git|cd|chdir|pushd|sl|set-location|push-location|bash|sh|pwsh|powershell|cmd)(${bs}.exe)?([[:space:]${sq}${dq}]|$)"
shopt -s nocasematch
command=$(json_value command)
# A command with no git word before a commit or a push word cannot commit or push
if [[ ! "$command" =~ git[^${nl}]*(commit|push) ]]
then
  exit 0
fi
start=$(json_value cwd)
start=${start:-${CLAUDE_PROJECT_DIR:-.}}
folder_of "$PWD" "$start"
check_command "$FOLDER" "$command"
exit 0
