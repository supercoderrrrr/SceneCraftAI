param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$CmPath = 'C:\Program Files\PlasticSCM5\client\cm.exe'
)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $ProjectPath
try {
    $ids = @(& $CmPath find changeset 'order by changesetid asc' '--format={changesetid}' --nototal)
    if ($LASTEXITCODE -ne 0) { throw 'SCM history query failed' }
    $records = @()
    foreach ($id in $ids) {
        if ($id -notmatch '^\d+$') { continue }
        [xml]$log = (& $CmPath log "cs:$id" --xml --repositorypaths --encoding=utf-8 | Out-String)
        if ($LASTEXITCODE -ne 0) { throw "SCM export failed for changeset $id" }
        $cs = $log.LogList.Changeset
        $records += [ordered]@{
            changeset = [int]$id
            date = [string]$cs.Date
            branch = [string]$cs.Branch
            comment = [string]$cs.Comment
            changedItems = @($cs.Changes.Item | Where-Object { $null -ne $_ } | ForEach-Object {
                [ordered]@{ type = [string]$_.Type; path = [string]$_.DstCmPath; previousPath = [string]$_.SrcCmPath }
            })
        }
    }
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    $export = [ordered]@{
        source = 'Unity Version Control / Plastic SCM'
        exportedAt = [DateTimeOffset]::UtcNow.ToString('o')
        redactions = @('Account email', 'Organization and server identifiers', 'Absolute workspace paths', 'Authentication configuration')
        note = 'Genuine changeset metadata, not historical Git commits or a full source-revision export'
        changesets = $records
    }
    $json = ($export | ConvertTo-Json -Depth 12) + "`n"
    if ($json -match '[A-Z]:[\\/]|[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}|sk-[a-zA-Z0-9]{20,}') {
        throw 'Public history contains sensitive identifiers; review before exporting'
    }
    [IO.File]::WriteAllText((Join-Path $OutputPath 'changesets.json'), $json, [Text.UTF8Encoding]::new($false))
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Plastic SCM History / Plastic SCM 历史')
    $lines.Add('')
    $lines.Add('Actual changeset IDs, timestamps, original comments and repository-relative paths exported from Unity Version Control. Account email, cloud organization/server identifiers and local workspace paths are excluded. This is a metadata archive, not a full source-revision export or reconstructed Git history. CS0 initializes the repository.')
    $lines.Add('')
    $lines.Add('以下编号、时间、原始提交说明与仓库相对路径直接导出自 Unity Version Control。账户邮箱、云组织/服务器标识与本机路径未公开。这是元数据归档，不是历史源码的完整导出，也没有伪造 Git 历史。CS0 是仓库初始化。')
    foreach ($record in $records) {
        $lines.Add('')
        $lines.Add("## CS$($record.changeset) | $($record.date) | $($record.branch)")
        $lines.Add('')
        $lines.Add($(if ([string]::IsNullOrWhiteSpace($record.comment)) { '(No check-in comment / 无提交说明)' } else { $record.comment }))
        $lines.Add('')
        $lines.Add("Changed items: $($record.changedItems.Count)")
        $lines.Add('')
        $lines.Add('<details><summary>Changed paths / 文件清单</summary>')
        $lines.Add('')
        foreach ($item in $record.changedItems) { $lines.Add("- ``$($item.type)`` ``$($item.path)``") }
        $lines.Add('')
        $lines.Add('</details>')
    }
    [IO.File]::WriteAllText((Join-Path $OutputPath 'HISTORY.md'), ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Exported $($records.Count) genuine SCM changesets"
}
finally { Pop-Location }
