<#
.SYNOPSIS
    Exercises the built assembly outside RimWorld, without loading a save.

.DESCRIPTION
    RimWorld takes about twenty minutes to load, so anything checkable without it should be.
    This loads Arkh.dll by reflection, wires up settings, and drives the parts that
    do not need a Pawn or a Map: the budget allocator, the text clamp, and every section's
    variant enumeration.

    It exists because it caught a real bug the compiler could not: our IL called the
    parameterless String.TrimEnd(), which is a .NET Core / .NET Standard 2.1 addition. It
    compiles fine against the reference assemblies and throws MissingMethodException on the
    .NET Framework-era runtime the game uses. A provider throwing is caught and turned into
    empty text, so in game it would have looked like "the anchor never fires" — a wrong diagnosis
    costing another twenty-minute load.

    Any MissingMethodException here is that class of bug. Treat it as a build failure.

.EXAMPLE
    .\tools\smoke-test.ps1
#>
param(
    [string]$RimWorldDir = "E:\SteamLibrary\steamapps\common\RimWorld"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$managed = Join-Path $RimWorldDir "RimWorldWin64_Data\Managed"
$probeExtra = @()
$mine    = Join-Path $root "1.6\Assemblies"

if (-not (Test-Path (Join-Path $mine "Arkh.dll"))) {
    Write-Host "No build found. Run .\build.ps1 first." -ForegroundColor Red
    exit 1
}

$probe = @($mine, $managed)
[AppDomain]::CurrentDomain.add_AssemblyResolve([ResolveEventHandler] {
    param($s, $e)
    $n = ($e.Name -split ',')[0]
    foreach ($p in $probe) {
        $f = Join-Path $p "$n.dll"
        if (Test-Path $f) { return [Reflection.Assembly]::LoadFrom($f) }
    }
    return $null
})

$BF = [Reflection.BindingFlags]'Public,Static'
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $mine "Arkh.dll"))

$T = @{
    Settings = $asm.GetType('Arkh.Settings.ArkhSettings')
    Mod      = $asm.GetType('Arkh.ArkhMod')
    Catalog  = $asm.GetType('Arkh.Prompt.PromptCatalog')
    Decl     = $asm.GetType('Arkh.Prompt.PromptSection')
    Budget   = $asm.GetType('Arkh.Budget.PromptBudget')
    Text     = $asm.GetType('Arkh.Util.TextUtil')
    Age      = $asm.GetType('Arkh.Context.AgeVoice')
    Gender   = $asm.GetType('Arkh.Context.GenderVoice')
    Lore     = $asm.GetType('Arkh.Context.WorldLore')
}

$settings = [Activator]::CreateInstance($T.Settings)
$T.Mod.GetField('Settings', $BF).SetValue($null, $settings)

# Declare the real sections rather than a mirror of them.
#
# PromptCatalog deliberately has no StaticConstructorOnStartup, so this works with no game: the
# startup work that does need one lives in ArkhStartup. That separation is what lets this harness
# exercise the shipping declarations instead of a copy that can quietly drift out of step.
#
# [void] is load-bearing, not tidiness: MethodInfo.Invoke is declared to return object, so even for
# a void method PowerShell emits its null into the pipeline — which inside a function turns the
# return value into an array, and every lookup against it then silently yields nothing.
[void]$T.Catalog.GetMethod('EnsureDeclared', $BF).Invoke($null, @())

$sectionCount = $T.Catalog.GetProperty('Sections', $BF).GetValue($null).Count

$script:failures = 0
function Check($label, $condition, $detail) {
    if ($condition) {
        Write-Host ("  PASS  " + $label) -ForegroundColor Green
    } else {
        Write-Host ("  FAIL  " + $label + "  -- " + $detail) -ForegroundColor Red
        $script:failures++
    }
}

function Get-Allocation {
    [void]$T.Budget.GetMethod('Invalidate', $BF).Invoke($null, @())
    $rows = @{}
    foreach ($r in $T.Budget.GetMethod('Table', $BF).Invoke($null, @())) {
        $rows[$r.Item1] = @{ Allowance = $r.Item2; Desired = $r.Item3; Squeezed = $r.Item4 }
    }
    $rows['_committed'] = $T.Budget.GetMethod('Committed', $BF).Invoke($null, @())
    return $rows
}

Write-Host "`nNo unexpected exceptions is the headline result: a MissingMethodException here is a"
Write-Host "runtime-only bug the compiler cannot see.`n"

# --- 1. Text clamping, including the shapes that have no spaces ------------------------------
Write-Host "Text clamping"
$clamp = $T.Text.GetMethod('Clamp', $BF)
Check "short text is untouched" ($clamp.Invoke($null, @("hello", 50)) -eq "hello") "got '$($clamp.Invoke($null,@('hello',50)))'"
Check "long text is trimmed to the limit" ($clamp.Invoke($null, @(("a b " * 100), 40)).Length -le 41) "length was $($clamp.Invoke($null,@(('a b '*100),40)).Length)"
Check "spaceless text still clamps (CJK, URLs)" ($clamp.Invoke($null, @(("x" * 500), 40)).Length -le 41) "length was $($clamp.Invoke($null,@(('x'*500),40)).Length)"
Check "zero budget yields nothing" ($clamp.Invoke($null, @("anything", 0)) -eq "") "got non-empty"

# --- 2. The real catalogue --------------------------------------------------------------------
Write-Host "`nCatalogue"
Check "sections declare themselves with no game loaded" ($sectionCount -ge 3) "got $sectionCount"

# --- 3. Variant enumeration with nothing authored --------------------------------------------
Write-Host "`nVariants, nothing authored"
$ageVariants = $T.Age.GetMethod('Variants', $BF).Invoke($null, @())
Check "five age bands" ($ageVariants.Count -eq 5) "got $($ageVariants.Count)"
$adult = $ageVariants | Where-Object { $_.Label -like 'Adult*' }
Check "adult is a placeholder by design" ($adult.IsPlaceholder) "adult emitted real text"
$child = $ageVariants | Where-Object { $_.Label -like 'Child*' }
Check "child emits real guidance" (-not $child.IsPlaceholder) "child was a placeholder"

$a = Get-Allocation
Check "unwritten gender reserves nothing" ($a['GenderVoice'].Desired -eq 0) "desired=$($a['GenderVoice'].Desired)"
Check "age reserves its longest band" ($a['AgeVoice'].Desired -gt 100) "desired=$($a['AgeVoice'].Desired)"

# --- 3. The core invariant: never overspend ---------------------------------------------------
Write-Host "`nAllocation never exceeds the ceiling"
$settings.WorldLore = ("The Rim remembers what the colonists would rather forget. " * 80)
$settings.MaleVoice = "Clipped and understated."
$settings.FemaleVoice = "Direct, and slower to anger."

$overspends = @()
$combinations = 0
foreach ($total in 100, 250, 400, 700, 1200, 2000, 5000) {
    foreach ($pawns in 1, 3, 6) {
        $settings.TotalBudgetChars = $total
        $settings.AssumedParticipants = $pawns
        $combinations++
        $a = Get-Allocation
        if ($null -eq $a['_committed']) {
            $overspends += "total=$total pawns=$pawns produced no allocation"
        } elseif ($a['_committed'] -gt $total) {
            $overspends += "total=$total pawns=$pawns committed $($a['_committed'])"
        }
    }
}
Check "no overspend across $combinations budget/size combinations" ($overspends.Count -eq 0) ($overspends -join '; ')

# --- 4. Squeezing takes from the lowest priority first ----------------------------------------
Write-Host "`nSqueeze order"
$settings.AssumedParticipants = 3
$settings.TotalBudgetChars = 5000
$roomy = Get-Allocation
$settings.TotalBudgetChars = 600
$tight = Get-Allocation

Check "world lore gives way when tight" ($tight['WorldLore'].Allowance -lt $roomy['WorldLore'].Allowance) `
      "roomy=$($roomy['WorldLore'].Allowance) tight=$($tight['WorldLore'].Allowance)"
Check "age voice is protected ahead of lore" ($tight['AgeVoice'].Allowance -eq $roomy['AgeVoice'].Allowance) `
      "roomy=$($roomy['AgeVoice'].Allowance) tight=$($tight['AgeVoice'].Allowance)"

Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "All smoke checks passed." -ForegroundColor Green
    exit 0
}
Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
exit 1
