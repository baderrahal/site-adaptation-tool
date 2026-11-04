# PreToolUse. No lane reads the scenario answers or the hold-out answers, only the scoring scripts do, inside their own run. Never tune on what you test.
# Found by the path a tool names or the text of a command, so a script written to read them is not seen, the review looks for that.
. (Join-Path $PSScriptRoot 'lane.ps1')

$payload = Read-HookInput
$fields = @()
foreach ($name in @('file_path', 'notebook_path', 'path', 'glob', 'command')) {
    $value = $payload.tool_input.$name
    if ($value) { $fields += [string]$value }
}
foreach ($field in $fields) {
    $text = $field.Replace('\', '/').ToLowerInvariant()
    # A double backslash in a command reads as two slashes
    $text = $text -replace '/+', '/'
    if ($text -match 'scenarios/answers|scenarios/holdout_answers|tests/holdout/answers') {
        [Console]::Error.WriteLine('Blocked by .claude/hooks/guard-answers.ps1: the scenario and hold-out answers are read by the scoring scripts alone. Run scripts/scenarios/score_scenarios.py for the counts.')
        exit 2
    }
}
exit 0
