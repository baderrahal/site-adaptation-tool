#!/usr/bin/env bash
# Blocks every command that changes the Windows registry (R104). Reads the hook JSON on stdin.
# reg.exe is blocked whatever it does. A PowerShell item cmdlet or its alias, the .NET registry classes and winreg
# are blocked when the command names a registry path or a registry class.
input=$(cat)
text=${input,,}

bs=$'\x5c'
start="(^|[^a-z0-9_.-])"
re_reg="${start}reg(${bs}.exe)?[[:space:]\"']"
regpath="(hk(cu|lm|cr|u|cc)(:|${bs}${bs})|hkey_|registry::|microsoft${bs}.win32${bs}.registry|registrykey|winreg)"
cmdlets="${start}(set-itemproperty|new-itemproperty|remove-itemproperty|rename-itemproperty|copy-itemproperty|move-itemproperty|clear-itemproperty|remove-item|new-item|set-item|clear-item|rename-item|copy-item|move-item|sp|rp|clp|cpp|mp|rnp|ni|ri|rm|del|erase|rd|rmdir|si|cli|ren|rni|cpi|copy|mi|move)([^a-z0-9_-]|$)"
methods="(setvalue|setvalueex|createsubkey|createkey|createkeyex|deletevalue|deletesubkey|deletesubkeytree|deletekey|deletekeyex)[[:space:]]*${bs}("

deny() {
  echo "Blocked by .claude/hooks/guard-registry.sh: $1. Nothing in the registry is changed by this tool, Bader answers it by hand." >&2
  exit 2
}

[[ "$text" =~ $re_reg ]] && deny "reg.exe"
[[ "$text" =~ $regpath && "$text" =~ $cmdlets ]] && deny "a registry change"
[[ "$text" =~ $regpath && "$text" =~ $methods ]] && deny "a registry change"
exit 0
