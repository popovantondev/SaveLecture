param(
    [Parameter(Mandatory = $true)][long]$SessionClockOrigin100ns,
    [int]$PollMilliseconds = 150,
    [int]$ParentProcessId = 0,
    [switch]$Probe
)

$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadWithPartialName('UIAutomationClient')
[void][Reflection.Assembly]::LoadWithPartialName('UIAutomationTypes')
[void][Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms')

$script:textCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Text
)
$panelConditions = New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
foreach ($name in @('Liveuntertitel', 'Live captions', 'Closed captions', 'Субтитры', 'Живые субтитры')) {
    $panelConditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name))
}
foreach ($id in @('closed-captions-panel', 'closed-captions-container', 'live-captions-panel', 'live-captions-container', 'captions-panel', 'captions-container')) {
    $panelConditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
}
$script:panelCondition = [System.Windows.Automation.OrCondition]::new($panelConditions.ToArray())
. (Join-Path $PSScriptRoot 'caption_observation_state.ps1')
$script:panelStates = @{}
$script:labelPattern = [regex]::new('\(([^,()]{1,80}),\s*([^()]{1,80})\)')

function Get-TeamsProcessIds {
    try { $all = @(Get-CimInstance Win32_Process -ErrorAction Stop) }
    catch {
        $fallbackIds = New-Object 'System.Collections.Generic.HashSet[int]'
        foreach ($process in Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(ms-teams|Teams)$' }) {
            [void]$fallbackIds.Add([int]$process.Id)
        }
        return @($fallbackIds)
    }
    $ids = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($process in $all | Where-Object { $_.Name -in @('ms-teams.exe', 'Teams.exe') }) {
        [void]$ids.Add([int]$process.ProcessId)
    }
    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($process in $all) {
            if ($ids.Contains([int]$process.ParentProcessId) -and $ids.Add([int]$process.ProcessId)) { $changed = $true }
        }
    }
    return @($ids)
}

function Get-ElementKey($Element) {
    try { return (($Element.GetRuntimeId()) -join '.') }
    catch { return "$($Element.Current.AutomationId)|$([int]$Element.Current.BoundingRectangle.X)|$([int]$Element.Current.BoundingRectangle.Y)" }
}

function Normalize-Text([string]$Text) {
    return [regex]::Replace(($Text -replace '[\r\n\t]+', ' '), '\s{2,}', ' ').Trim()
}

function Is-UiText([string]$Text) {
    if (-not $Text -or $Text.Length -lt 2 -or $Text.Length -gt 500) { return $true }
    if ($Text -match '^\d{1,2}:\d{2}\s*:|^(Heute|Gestern|Today|Yesterday)\b|^Besprechung in .+ beendet$') { return $true }
    if ($Text -match '^(Microsoft Teams|Live captions|Captions|Show live captions|Hide live captions|Meeting controls|Meeting controls|Chat|People|React|View|Camera|Microphone|Share|Leave)$') { return $true }
    if ($Text -match '^vc[-_]|^\S+\s*\([^,()]+,\s*[^()]+\)\s*$|^\([^,()]+,\s*[^()]+\)$') { return $true }
    return $false
}

function Get-RowSpeaker($Element, [string]$PanelKey) {
    $parent = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($Element)
    if ($parent) {
        $siblings = @($parent.FindAll([System.Windows.Automation.TreeScope]::Children, $script:textCondition))
        if ($siblings.Count -ge 2) { return (Normalize-Text $siblings[0].Current.Name) }
    }
    for ($depth = 0; $depth -lt 5 -and $parent; $depth++) {
        if ((Get-ElementKey $parent) -eq $PanelKey) { break }
        $names = @()
        foreach ($label in $parent.FindAll([System.Windows.Automation.TreeScope]::Descendants, $script:textCondition)) {
            $match = $script:labelPattern.Match($label.Current.Name)
            if ($match.Success) { $names += ($match.Groups[2].Value.Trim() + ' ' + $match.Groups[1].Value.Trim()) }
        }
        $names = @($names | Select-Object -Unique)
        if ($names.Count -eq 1) { return $names[0] }
        if ($names.Count -gt 1) { return '' }
        $parent = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($parent)
    }
    return ''
}

function Emit-Update($Item, [string]$Speaker) {
    $counter = [Diagnostics.Stopwatch]::GetTimestamp()
    $ticks = [long]($counter * (10000000.0 / [Diagnostics.Stopwatch]::Frequency))
    $record = [ordered]@{
        type = 'caption'
        source_key = $Item.Key
        text = $Item.Text
        speaker = $Speaker
        observed_100ns = [long]($ticks - $SessionClockOrigin100ns)
        scope_verified = $true
        initial_history = [bool]$Item.InitialHistory
    }
    [Console]::Out.WriteLine(($record | ConvertTo-Json -Compress -Depth 3))
}

$teamsIds = Get-TeamsProcessIds
$root = [System.Windows.Automation.AutomationElement]::RootElement
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$lastPanelCount = -1
$refreshClock = [Diagnostics.Stopwatch]::StartNew()

while ($true) {
    if ($ParentProcessId -gt 0) {
        try { $owner = [Diagnostics.Process]::GetProcessById($ParentProcessId); $owner.Dispose() }
        catch { return }
    }
    try {
        if ($refreshClock.Elapsed.TotalSeconds -ge 5) { $teamsIds = Get-TeamsProcessIds; $refreshClock.Restart() }
        if ((-not $teamsIds -or $teamsIds.Count -eq 0) -and -not $Probe) {
            if ($lastPanelCount -ne 0) { [Console]::Out.WriteLine('{"status":"Teams not running; waiting."}'); $lastPanelCount = 0 }
            Start-Sleep -Milliseconds 500
            continue
        }
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        $items = New-Object System.Collections.Generic.List[object]
        $panelSnapshots = New-Object System.Collections.Generic.List[object]
        $visibleTeamsWindows = 0
        $panelCount = 0
        foreach ($window in $windows) {
            if ($teamsIds -notcontains [int]$window.Current.ProcessId) { continue }
            $visibleTeamsWindows++
            try {
                $panels = @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $script:panelCondition) | Where-Object {
                    $_.Current.ControlType -in @([System.Windows.Automation.ControlType]::Group, [System.Windows.Automation.ControlType]::Pane) -and
                    -not $_.Current.IsOffscreen -and ($_.Current.Name -match '^(Liveuntertitel|Live captions|Closed captions|\u0421\u0443\u0431\u0442\u0438\u0442\u0440\u044b|\u0416\u0438\u0432\u044b\u0435 \u0441\u0443\u0431\u0442\u0438\u0442\u0440\u044b)$' -or $_.Current.AutomationId -match '^(closed-captions|live-captions|captions)-(panel|container)$')
                })
            }
            catch { continue }
            foreach ($panel in $panels) {
            $panelCount++
            $panelKey = Get-ElementKey $panel
            $panelItems = New-Object System.Collections.Generic.List[object]
            $panelComplete = $true
            $elements = $panel.FindAll([System.Windows.Automation.TreeScope]::Descendants, $script:textCondition)
            foreach ($element in $elements) {
                try {
                    if ($element.Current.IsOffscreen) { continue }
                    $row = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)
                    $rowTexts = @($row.FindAll([System.Windows.Automation.TreeScope]::Children, $script:textCondition))
                    if ($rowTexts.Count -ge 2 -and (Get-ElementKey $rowTexts[0]) -eq (Get-ElementKey $element)) { continue }
                    if ($rowTexts.Count -eq 1) {
                        $rowChildren = $row.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
                        if (@($rowChildren | Where-Object { $_.Current.Name -in @('Ungeprüft', 'Unverified') }).Count -gt 0) { continue }
                    }
                    $text = Normalize-Text $element.Current.Name
                    if (Is-UiText $text) { continue }
                    $rect = $element.Current.BoundingRectangle
                    if ($rowTexts.Count -ge 2) { $speaker = Normalize-Text $rowTexts[0].Current.Name }
                    else { $speaker = Get-RowSpeaker $element $panelKey }
                    $item = [pscustomobject]@{ Key = Get-ElementKey $element; Text = $text; Speaker = $speaker; InitialHistory = $false }
                    $items.Add($item)
                    $panelItems.Add($item)
                } catch { $panelComplete = $false }
            }
            $panelSnapshots.Add([pscustomobject]@{ Key = $panelKey; Items = @($panelItems.ToArray()); Complete = $panelComplete })
            }
        }

        if ($Probe) {
            $speakerCandidates = @($items | Where-Object { $_.Speaker }).Count
            [Console]::Out.WriteLine((@{ teams_processes = @($teamsIds).Count; teams_windows = $visibleTeamsWindows;
                caption_panels = $panelCount; visible_text_controls = $items.Count; participant_label_candidates = $speakerCandidates } | ConvertTo-Json -Compress))
            return
        }
        if ($lastPanelCount -ne $panelCount) {
            [Console]::Out.WriteLine((@{status = "Caption panels visible: $panelCount; scoped text controls: $($items.Count)."} | ConvertTo-Json -Compress))
            $lastPanelCount = $panelCount
        }
        foreach ($item in @(Get-CaptionObservationUpdates -PanelStates $script:panelStates -Panels $panelSnapshots.ToArray() -EmitBaseline)) {
            Emit-Update $item $item.Speaker
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    } catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        Start-Sleep -Milliseconds 1000
    }
}
