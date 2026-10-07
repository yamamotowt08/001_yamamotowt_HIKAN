<#
.SYNOPSIS
  参照DLLバージョン検証(CLAUDE.PRIVATE.md 9.2)。コード生成・ビルド前に Windows 側で実行する。
.DESCRIPTION
  Step 1: src 配下の .csproj の Autodesk 系 Reference が Private=False であることを確認
  Step 2: AutoCAD / Civil 3D / Dynamo の実 DLL バージョンを実測
  Step 3: 期待バージョン(AutoCAD 系 25.x、Dynamo 系 3.3.x)と突き合わせ
  Step 4: 不一致があれば一覧を出して終了コード 1 で停止(推測で書き進めない)
  WSL2 から: powershell.exe -File scripts/verify-dll-versions.ps1
#>
param(
    [string]$AcadDir = "C:\Program Files\Autodesk\AutoCAD 2025",
    [string]$DynamoCoreDir = "C:\Program Files\Autodesk\AutoCAD 2025\C3D\Dynamo\Core"
)

$ng = $false
$rows = @()

function Check-Dll([string]$dir, [string]$name, [string]$expectPattern, [string]$expectLabel) {
    $path = Join-Path $dir $name
    if (-not (Test-Path $path)) {
        $script:rows += [pscustomobject]@{ Name = $name; FileVersion = '(未検出)'; ProductVersion = ''; Expected = $expectLabel; Result = 'NG'; Path = $path }
        $script:ng = $true
        return
    }
    $vi = (Get-Item $path).VersionInfo
    $ok = ($vi.FileVersion -match $expectPattern)
    if (-not $ok) { $script:ng = $true }
    $script:rows += [pscustomobject]@{ Name = $name; FileVersion = $vi.FileVersion; ProductVersion = $vi.ProductVersion; Expected = $expectLabel; Result = $(if ($ok) { 'OK' } else { 'NG' }); Path = $path }
}

# Step 1: csproj の Private=False
$root = Split-Path -Parent $PSScriptRoot
foreach ($proj in Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.csproj) {
    [xml]$x = Get-Content $proj.FullName -Encoding UTF8
    foreach ($ref in $x.Project.ItemGroup.Reference) {
        if ($ref -and $ref.HintPath -and $ref.Private -ne 'False') {
            Write-Host "NG: $($proj.Name) の Reference '$($ref.Include)' が Private=False ではありません。" -ForegroundColor Red
            $ng = $true
        }
    }
}

# Step 2/3: 実測と突き合わせ
Check-Dll $AcadDir 'AcCoreMgd.dll' '^25\.' '25.x'
Check-Dll $AcadDir 'AcDbMgd.dll'   '^25\.' '25.x'
Check-Dll $AcadDir 'AcMgd.dll'     '^25\.' '25.x'
Check-Dll $DynamoCoreDir 'DynamoServices.dll' '^3\.3\.' '3.3.x'
Check-Dll $DynamoCoreDir 'ProtoGeometry.dll'  '^3\.3\.' '3.3.x (参考: 第1段階では未使用)'

# Aecc*Mgd はバージョン体系が AutoCAD と異なるため一覧表示のみ(第1段階では未使用)
$c3d = Join-Path $AcadDir 'C3D'
if (Test-Path $c3d) {
    foreach ($f in Get-ChildItem (Join-Path $c3d 'Aecc*Mgd.dll') -ErrorAction SilentlyContinue) {
        $rows += [pscustomobject]@{ Name = $f.Name; FileVersion = $f.VersionInfo.FileVersion; ProductVersion = $f.VersionInfo.ProductVersion; Expected = '(参考)'; Result = '-'; Path = $f.FullName }
    }
}

$rows | Format-Table Name, FileVersion, ProductVersion, Expected, Result -AutoSize
$rows | Format-Table Path -AutoSize | Out-String | Write-Host

if ($ng) {
    Write-Host '不一致または未検出があります。コード生成・ビルドを停止し、以下を確認してください:' -ForegroundColor Red
    Write-Host '  1. 検出された実バージョン(上表)'
    Write-Host '  2. .csproj が期待するバージョン(AutoCAD 系 25.x / Dynamo 系 3.3.x)'
    Write-Host '  3. 対処: HintPath 修正(-p:AcadDir / -p:DynamoCoreDir)/ 別環境への切替 / バージョン統一'
    exit 1
}
Write-Host '全 DLL のバージョン一致を確認しました。' -ForegroundColor Green
exit 0
