# Pure observation state; UIA scanning and timestamp emission stay in the reader.
function Get-CaptionObservationUpdates {
    param([hashtable]$PanelStates, [object[]]$Panels, [switch]$EmitBaseline)

    $present = @{}
    foreach ($panel in $Panels) {
        $present[$panel.Key] = $true
        if ($panel.Complete -eq $false) { continue }
        $baseline = -not $PanelStates.ContainsKey($panel.Key)
        if ($baseline) { $PanelStates[$panel.Key] = @{} }
        $seen = $PanelStates[$panel.Key]
        foreach ($item in $panel.Items) {
            $changed = $seen.ContainsKey($item.Key) -and $seen[$item.Key] -ne $item.Text
            $new = -not $seen.ContainsKey($item.Key)
            $seen[$item.Key] = $item.Text
            if ($baseline -and $EmitBaseline) { $item.InitialHistory = $true; $item }
            # A panel's first successful snapshot establishes history, even if it is empty.
            if (-not $baseline -and ($new -or $changed)) { $item }
        }
    }
    # Reopened panels get a fresh baseline instead of replaying their visible history.
    foreach ($key in @($PanelStates.Keys)) {
        if (-not $present.ContainsKey($key)) { $PanelStates.Remove($key) }
    }
}
