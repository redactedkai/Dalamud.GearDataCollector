# Renders the HTML mockup to a PNG with headless Edge or Chrome. No browser window opens.
# Usage: powershell -File render-mockup.ps1 [-Tab config|info] [-Width 1100] [-Height 1100]
#        [-Html <path>] [-Out <png path>]
param(
    [string]$Tab = 'info',
    [int]$Width = 1100,
    [int]$Height = 1100,
    [string]$Html,
    [string]$Out
)

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if (-not $Html) { $Html = Join-Path $repo 'mockups\window-tabs.html' }
if (-not $Out) { $Out = Join-Path $repo "scratch\shots\mock-$Tab.png" }

$browser = @(
    'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
    'C:\Program Files\Microsoft\Edge\Application\msedge.exe',
    'C:\Program Files\Google\Chrome\Application\chrome.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw 'Neither Edge nor Chrome was found.' }

# A temporary copy opens the requested tab on load; the original is not changed.
$outDir = Split-Path $Out
New-Item -ItemType Directory -Force $outDir | Out-Null
$temp = Join-Path $outDir "mock-$Tab.html"
$text = Get-Content $Html -Raw -Encoding UTF8
$text = $text -replace '</body>', "<script>window.addEventListener('load',function(){show('$Tab');});</script></body>"
Set-Content $temp $text -Encoding UTF8

$url = 'file:///' + ($temp -replace '\\', '/')
& $browser --headless=new --disable-gpu --hide-scrollbars --window-size="$Width,$Height" --screenshot="$Out" $url | Out-Null
Start-Sleep -Seconds 2
if (Test-Path $Out) { "Wrote $Out" } else { throw 'No screenshot was written.' }
