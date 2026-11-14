[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [string]$BlendFile,
    [string]$BlenderPath,
    [string[]]$ScriptArguments = @()
)

$ErrorActionPreference = 'Stop'
if (-not $BlenderPath) { $BlenderPath = $env:BLENDER_EXECUTABLE }
if (-not $BlenderPath) {
    $application = Get-Command blender -CommandType Application -ErrorAction SilentlyContinue
    if ($application) { $BlenderPath = $application.Source }
}
if (-not $BlenderPath -or -not (Test-Path -LiteralPath $BlenderPath -PathType Leaf)) {
    throw 'Specify -BlenderPath or set BLENDER_EXECUTABLE to your blender.exe path.'
}
$resolvedScript = (Resolve-Path -LiteralPath $ScriptPath).Path
$blenderArguments = @('--background', '--factory-startup', '--disable-autoexec')
if ($BlendFile) { $blenderArguments += (Resolve-Path -LiteralPath $BlendFile).Path }
$blenderArguments += @('--python-exit-code', '1', '--python', $resolvedScript)
if ($ScriptArguments.Count -gt 0) { $blenderArguments += @('--') + $ScriptArguments }
& $BlenderPath @blenderArguments
if ($LASTEXITCODE -ne 0) { throw "Blender task failed with exit code $LASTEXITCODE" }
