<#
.SYNOPSIS
  Points a standalone test build's assembly binding at the installed Rhino.

.DESCRIPTION
  A standalone build (outside the Rhino source tree) has no RhinoCommon.dll next to the test
  assembly: MxTests.csproj references the RhinoCommon package with ExcludeAssets="runtime",
  deliberately, because the managed RhinoCommon has to load from Rhino's own System folder or its
  native core (RhinoLibrary.dll) cannot be found. Without any binding help, NUnit cannot load the
  test assembly at all and Test Explorer discovers zero tests.

  This script adds <bindingRedirect> + <codeBase> entries for RhinoCommon, Rhino.UI and Eto to the
  app.config the .NET SDK generates, binding each straight to the installed Rhino without copying
  anything. The versions are read from the installed files at build time, so a Rhino WIP update
  cannot leave a stale hardcoded version behind - which is exactly what broke the previous,
  hand-written version of this file.

  Existing redirects in the generated config (nunit.framework, System.Runtime.CompilerServices.Unsafe,
  ...) are preserved; only the three Rhino entries are replaced.
#>
[CmdletBinding()]
param(
  # The generated config to amend, e.g. bin\Debug\net48\MxTests.dll.config.
  [Parameter(Mandatory = $true)] [string] $ConfigPath,

  # Rhino's System folder, e.g. "C:\Program Files\Rhino 9 WIP\System".
  [Parameter(Mandatory = $true)] [string] $RhinoSystemDir
)

$ErrorActionPreference = 'Stop'

# The caller may still pass a trailing separator; a trailing backslash immediately before the
# closing quote of an Exec command line escapes that quote, so the targets file trims it. Trim
# again here so the script is safe to run by hand either way.
$RhinoSystemDir = $RhinoSystemDir.TrimEnd('\', '/')
$ASM_NS = 'urn:schemas-microsoft-com:asm.v1'

# Rhino ships these three as a matched set; Eto carries its own version.
$targets = @(
  @{ Name = 'RhinoCommon'; File = 'RhinoCommon.dll'; Token = '552281e97c755530' }
  @{ Name = 'Rhino.UI';    File = 'Rhino.UI.dll';    Token = '552281e97c755530' }
  @{ Name = 'Eto';         File = 'Eto.dll';         Token = '552281e97c755530' }
)

if (-not (Test-Path -LiteralPath $RhinoSystemDir)) {
  Write-Host "write-rhino-bindings: Rhino system directory not found, skipping: $RhinoSystemDir"
  exit 0
}

# Start from the SDK-generated config so its redirects survive; synthesise one if absent.
$xml = New-Object System.Xml.XmlDocument
$xml.PreserveWhitespace = $false
if (Test-Path -LiteralPath $ConfigPath) {
  $xml.Load((Resolve-Path -LiteralPath $ConfigPath))
} else {
  $xml.LoadXml('<?xml version="1.0" encoding="utf-8"?><configuration><runtime /></configuration>')
}

$configuration = $xml.DocumentElement
if (-not $configuration) { throw "write-rhino-bindings: no root element in $ConfigPath" }

$runtime = $configuration.SelectSingleNode('runtime')
if (-not $runtime) { $runtime = $configuration.AppendChild($xml.CreateElement('runtime')) }

$nsMgr = New-Object System.Xml.XmlNamespaceManager $xml.NameTable
$nsMgr.AddNamespace('asm', $ASM_NS)

$binding = $runtime.SelectSingleNode('asm:assemblyBinding', $nsMgr)
if (-not $binding) {
  $binding = $runtime.AppendChild($xml.CreateElement('assemblyBinding', $ASM_NS))
}

$written = @()
foreach ($t in $targets) {
  $dll = Join-Path $RhinoSystemDir $t.File
  if (-not (Test-Path -LiteralPath $dll)) {
    Write-Host "write-rhino-bindings: $($t.File) not in $RhinoSystemDir, skipping that entry"
    continue
  }

  $version = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $dll)).Version.ToString()

  # Drop any entry for this assembly - a previous run's, or one the SDK generated - so the
  # freshly read version is the only one left. The SDK emits one <assemblyBinding> per redirect
  # rather than a single shared one, so this has to sweep all of them, not just $binding.
  $stalePath = "asm:assemblyBinding/asm:dependentAssembly[asm:assemblyIdentity/@name='$($t.Name)']"
  foreach ($stale in @($runtime.SelectNodes($stalePath, $nsMgr))) {
    [void]$stale.ParentNode.RemoveChild($stale)
  }

  $dep = $binding.AppendChild($xml.CreateElement('dependentAssembly', $ASM_NS))

  $identity = $dep.AppendChild($xml.CreateElement('assemblyIdentity', $ASM_NS))
  $identity.SetAttribute('name', $t.Name)
  $identity.SetAttribute('publicKeyToken', $t.Token)
  $identity.SetAttribute('culture', 'neutral')

  # The upper bound has to cover the compile-time reference, which can be a NuGet build either
  # newer or older than the install, so span the entire range instead of guessing a bound - a
  # major-version guess reads 2.9.9.9 for Eto 2.12.0.0, which excludes the very version it means.
  $redirect = $dep.AppendChild($xml.CreateElement('bindingRedirect', $ASM_NS))
  $redirect.SetAttribute('oldVersion', '0.0.0.0-65535.65535.65535.65535')
  $redirect.SetAttribute('newVersion', $version)

  $codeBase = $dep.AppendChild($xml.CreateElement('codeBase', $ASM_NS))
  $codeBase.SetAttribute('version', $version)
  $codeBase.SetAttribute('href', ([Uri]((Resolve-Path -LiteralPath $dll).Path)).AbsoluteUri)

  $written += "$($t.Name) $version"
}

if ($written.Count -eq 0) {
  Write-Host "write-rhino-bindings: nothing to write"
  exit 0
}

# Sweeping the SDK's one-redirect-per-block layout can empty some of those blocks; drop the shells.
foreach ($empty in @($runtime.SelectNodes('asm:assemblyBinding[not(asm:dependentAssembly)]', $nsMgr))) {
  [void]$empty.ParentNode.RemoveChild($empty)
}

$dir = Split-Path -Parent $ConfigPath
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.IndentChars = '  '
$settings.Encoding = New-Object System.Text.UTF8Encoding $false
$writer = [System.Xml.XmlWriter]::Create($ConfigPath, $settings)
try { $xml.Save($writer) } finally { $writer.Dispose() }

Write-Host "write-rhino-bindings: bound $($written -join ', ') -> $RhinoSystemDir"
