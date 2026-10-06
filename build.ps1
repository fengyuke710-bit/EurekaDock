$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildRoot = Join-Path $projectRoot '.build'
$output = Join-Path $projectRoot 'bin'
$env:DOTNET_CLI_HOME = Join-Path $buildRoot 'cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:NUGET_PACKAGES = Join-Path $buildRoot 'packages'
$env:APPDATA = Join-Path $buildRoot 'appdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA, $output | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Build Tools (vswhere.exe) not found.' }
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) { throw 'Desktop development with C++ workload not found.' }
$compilerSetup = Join-Path $installation 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $compilerSetup)) { throw 'vcvars64.bat not found.' }
Push-Location $projectRoot
try {
  $setup = 'call "' + $compilerSetup + '" >nul && '
  $bridge = $setup + 'cl /nologo /std:c++20 /EHsc /W4 /LD native\Bridge.cpp /Fo:bin\Bridge.obj /Fe:bin\EurekaDock.Native.dll /link user32.lib shell32.lib ole32.lib gdi32.lib comctl32.lib dwmapi.lib uuid.lib /IMPLIB:bin\EurekaDock.Native.lib'
  & cmd.exe /d /c $bridge
  if ($LASTEXITCODE -ne 0) { throw 'Native bridge build failed.' }
  $media = $setup + 'cl /nologo /std:c++20 /EHsc /W4 /LD native\MediaBridge.cpp /Fo:bin\MediaBridge.obj /Fe:bin\EurekaDock.Media.dll /link windowsapp.lib /IMPLIB:bin\EurekaDock.Media.lib'
  & cmd.exe /d /c $media
  if ($LASTEXITCODE -ne 0) { throw 'Media bridge build failed.' }
  dotnet restore src/EurekaDock.csproj --configfile NuGet.Config -p:NuGetAudit=false
  if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
  dotnet build src/EurekaDock.csproj -c Release --no-restore -o bin
  if ($LASTEXITCODE -ne 0) { throw 'Managed build failed' }
  $frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  if (-not (Test-Path -LiteralPath $frameworkCompiler)) { throw '.NET Framework C# compiler not found.' }
  & $frameworkCompiler /nologo /target:winexe /win32icon:src\Assets\EurekaDock.ico /out:bin\EurekaDock.Launcher.exe /reference:System.Windows.Forms.dll launcher\PortableLauncher.cs
  if ($LASTEXITCODE -ne 0) { throw 'Portable launcher build failed.' }
} finally { Pop-Location }

