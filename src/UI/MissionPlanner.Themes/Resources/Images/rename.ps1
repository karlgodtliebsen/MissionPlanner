Get-ChildItem -File | ForEach-Object {
    if ($_.Name -match '^x_(.+)_x(\.[^.]+)$') {
        $newName = "$($Matches[1])$($Matches[2])"

        Rename-Item -LiteralPath $_.FullName -NewName $newName
        Write-Host "$($_.Name) -> $newName"
    }
}