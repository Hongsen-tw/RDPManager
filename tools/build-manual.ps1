# 將 docs/USER_MANUAL.md 轉成 PDF (需 PowerShell 7 與 Microsoft Edge)
# 用法:pwsh tools/build-manual.ps1 [-Out dist/RDPManager-UserManual.pdf]
param([string]$Out = (Join-Path $PSScriptRoot "..\dist\RDPManager-UserManual.pdf"))
$ErrorActionPreference = 'Stop'

$docs = Resolve-Path (Join-Path $PSScriptRoot "..\docs")
$md = Get-Content (Join-Path $docs "USER_MANUAL.md") -Raw -Encoding utf8
$body = (ConvertFrom-Markdown -InputObject $md).Html

$html = @"
<!doctype html>
<html lang="zh-Hant"><head><meta charset="utf-8">
<title>RDP 遠端連線管理員 使用手冊</title>
<style>
  @page { size: A4; margin: 18mm 16mm 20mm 16mm; }
  body { font-family: "Microsoft JhengHei UI", "Microsoft JhengHei", sans-serif; font-size: 10.5pt; line-height: 1.65; color: #1f2328; }
  h1 { font-size: 22pt; color: #0b4f9c; border-bottom: 3px solid #1e90e8; padding-bottom: 6px; margin-top: 0; }
  h2 { font-size: 15pt; color: #0b4f9c; border-bottom: 1px solid #d0d7de; padding-bottom: 4px; margin-top: 26px; page-break-after: avoid; }
  h3 { font-size: 12.5pt; margin-top: 18px; page-break-after: avoid; }
  h4 { font-size: 11pt; margin-top: 14px; page-break-after: avoid; }
  h2:not(:first-of-type) { page-break-before: auto; }
  table { border-collapse: collapse; width: 100%; margin: 8px 0 12px; page-break-inside: avoid; }
  th, td { border: 1px solid #d0d7de; padding: 5px 8px; text-align: left; vertical-align: top; }
  th { background: #eef4fb; }
  code { font-family: Consolas, monospace; background: #f3f5f7; padding: 1px 4px; border-radius: 3px; font-size: 9.5pt; }
  pre { background: #f3f5f7; padding: 10px 12px; border-radius: 6px; overflow-x: auto; page-break-inside: avoid; }
  pre code { background: none; padding: 0; }
  img { max-width: 100%; border: 1px solid #d0d7de; border-radius: 4px; display: block; margin: 8px auto; page-break-inside: avoid; }
  p:has(> img) { page-break-inside: avoid; }
  blockquote { margin: 10px 0; padding: 6px 12px; background: #fff8e6; border-left: 4px solid #f0b429; }
  hr { border: none; border-top: 1px solid #d0d7de; margin: 18px 0; }
  .cover { text-align: center; padding-top: 70mm; page-break-after: always; }
  .cover img { border: none; width: 140px; }
  .cover .t { font-size: 28pt; font-weight: bold; color: #0b4f9c; margin-top: 18px; }
  .cover .s { font-size: 16pt; margin-top: 6px; }
  .cover .c { margin-top: 90mm; font-size: 11pt; color: #57606a; }
</style></head>
<body>
<div class="cover">
  <img src="images/icon-256.png" alt="">
  <div class="t">RDP 遠端連線管理員</div>
  <div class="s">使用手冊</div>
  <div class="s" style="font-size:12pt;color:#57606a">版本 1.1.0</div>
  <div class="c">鴻森智能科技股份有限公司<br>Copyright © 2026 All rights reserved.</div>
</div>
$body
</body></html>
"@

$htmlPath = Join-Path $docs "_manual.html"
Set-Content $htmlPath $html -Encoding utf8
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$outFull = [System.IO.Path]::GetFullPath($Out)

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "找不到 Microsoft Edge" }

$tmpProfile = Join-Path ([System.IO.Path]::GetTempPath()) ("edge-pdf-" + [guid]::NewGuid())
$uri = ([System.Uri]$htmlPath).AbsoluteUri
& $edge --headless=new --disable-gpu --no-first-run "--user-data-dir=$tmpProfile" --no-pdf-header-footer "--print-to-pdf=$outFull" $uri 2>&1 | Out-Null
Start-Sleep -Seconds 1
Remove-Item $htmlPath -ErrorAction SilentlyContinue
Remove-Item $tmpProfile -Recurse -Force -ErrorAction SilentlyContinue
if (-not (Test-Path $outFull)) { throw "PDF 產生失敗" }
"PDF written: $outFull ($([math]::Round((Get-Item $outFull).Length / 1KB)) KB)"
