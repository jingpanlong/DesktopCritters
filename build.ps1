param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.x C# 编译器。' }
$frameworkDir = Split-Path $compiler
$wpf = Join-Path $frameworkDir 'WPF'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$app = Join-Path $OutputDirectory 'DesktopCritters.exe'
$sources = @('Program.cs','Native.cs','Settings.cs','Surfaces.cs','CreatureCatalog.cs','CreatureRenderer.cs','PhotoSpriteRenderer.cs','DesktopIcons.cs','PetForm.cs','PetForm.Chores.cs','About.cs','Habitat.cs','ControlPanel.cs','Ui.cs','AppIcon.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/out:' + $app),'/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Xml.dll',('/r:'+(Join-Path $wpf 'UIAutomationClient.dll')),('/r:'+(Join-Path $wpf 'UIAutomationTypes.dll')),('/r:'+(Join-Path $wpf 'WindowsBase.dll')))
if (Test-Path (Join-Path $PSScriptRoot 'app.ico')) { $arguments += '/win32icon:'+(Join-Path $PSScriptRoot 'app.ico'); $arguments += '/resource:'+(Join-Path $PSScriptRoot 'app.ico')+',Critters.app.ico' }
foreach ($brand in @('logo','logo-mark','icon-master')) { $p = Join-Path $PSScriptRoot ('Assets\'+$brand+'.png'); if (Test-Path $p) { $name = $(if($brand -eq 'logo'){'logo.png'}elseif($brand -eq 'logo-mark'){'mark.png'}else{'icon.png'}); $arguments += '/resource:'+$p+',Critters.'+$name } }
Get-ChildItem -Path (Join-Path $PSScriptRoot 'Assets') -Filter '*-HD.png' | ForEach-Object { $arguments += '/resource:'+$_.FullName+',Critters.Assets.'+$_.Name }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw '桌面程序编译失败。' }
$setupArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),('/out:'+(Join-Path $OutputDirectory 'DesktopCritters-Setup.exe')),('/resource:'+$app+',DesktopCritters.Payload'),'/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
if (Test-Path (Join-Path $PSScriptRoot 'app.ico')) { $setupArgs += '/win32icon:'+(Join-Path $PSScriptRoot 'app.ico') }
& $compiler @setupArgs (Join-Path $PSScriptRoot 'Installer.cs')
if ($LASTEXITCODE -ne 0) { throw '安装程序编译失败。' }
Write-Output ('编译完成：'+$OutputDirectory)
