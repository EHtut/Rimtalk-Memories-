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

# Essential sections (the instruction and the output contract) are taken off the top in full, so
# the floor they impose is the one case where going over the ceiling is correct: a trimmed contract
# loses every reply, where an overspend loses only money.
$settings.TotalBudgetChars = 100000
$settings.AssumedParticipants = 1
$roomyTable = Get-Allocation
$essentialFloor = $roomyTable['SystemInstruction'].Desired + $roomyTable['OutputContract'].Desired

$overspends = @()
$trimmedEssentials = @()
$combinations = 0
foreach ($total in 100, 250, 400, 700, 1200, 2000, 5000) {
    foreach ($pawns in 1, 3, 6) {
        $settings.TotalBudgetChars = $total
        $settings.AssumedParticipants = $pawns
        $combinations++
        $a = Get-Allocation

        if ($null -eq $a['_committed']) {
            $overspends += "total=$total pawns=$pawns produced no allocation"
            continue
        }

        foreach ($name in 'SystemInstruction', 'OutputContract') {
            if ($a[$name].Allowance -lt $a[$name].Desired) {
                $trimmedEssentials += "total=$total pawns=$pawns trimmed $name"
            }
        }

        $ceiling = [Math]::Max($total, $essentialFloor)
        if ($a['_committed'] -gt $ceiling) {
            $overspends += "total=$total pawns=$pawns committed $($a['_committed']) over $ceiling"
        }
    }
}
Check "no overspend across $combinations budget/size combinations" ($overspends.Count -eq 0) ($overspends -join '; ')
Check "the output contract is never trimmed, at any budget" ($trimmedEssentials.Count -eq 0) ($trimmedEssentials -join '; ')

# --- 4. Squeezing takes from the lowest priority first ----------------------------------------
Write-Host "`nSqueeze order"
$settings.AssumedParticipants = 3
$settings.TotalBudgetChars = 5000
$roomy = Get-Allocation
$settings.TotalBudgetChars = 600
$tight = Get-Allocation

Check "world lore gives way when tight" ($tight['WorldLore'].Allowance -lt $roomy['WorldLore'].Allowance) `
      "roomy=$($roomy['WorldLore'].Allowance) tight=$($tight['WorldLore'].Allowance)"

# The invariant is about the *fraction* each section gets of what it asked for, not absolutes.
# A high-priority per-participant section cannot swallow the whole pool — it takes at most
# remaining/participants per call — so integer division legitimately leaves crumbs that flow down
# to lower-priority sections. What must always hold is that the higher priority is satisfied at
# least as completely as the lower one.
$ageShare  = $tight['AgeVoice'].Allowance  / [Math]::Max(1, $tight['AgeVoice'].Desired)
$loreShare = $tight['WorldLore'].Allowance / [Math]::Max(1, $tight['WorldLore'].Desired)
Check "age voice is satisfied more fully than lore when tight" ($ageShare -ge $loreShare) `
      ("age={0:P1} of {1}, lore={2:P1} of {3}" -f $ageShare, $tight['AgeVoice'].Desired, $loreShare, $tight['WorldLore'].Desired)

$settings.TotalBudgetChars = 60
$starved = Get-Allocation
Check "an impossible budget still ships the contract" ($starved['OutputContract'].Allowance -eq $starved['OutputContract'].Desired) `
      "allowance=$($starved['OutputContract'].Allowance) desired=$($starved['OutputContract'].Desired)"
Check "an impossible budget silences the optional blocks" ($starved['WorldLore'].Allowance -eq 0 -and $starved['AgeVoice'].Allowance -eq 0) `
      "lore=$($starved['WorldLore'].Allowance) age=$($starved['AgeVoice'].Allowance)"

# --- 5. JSON ----------------------------------------------------------------------------------
Write-Host "`nJSON"
$tJson = $asm.GetType('Arkh.Util.Json')
$parse = $tJson.GetMethod('Parse', $BF)
$quote = $tJson.GetMethod('Quote', $BF)

$sample = '{"choices":[{"message":{"role":"assistant","content":"Cold out.\nColder in."}}],"usage":{"prompt_tokens":12,"completion_tokens":5}}'
$v = $parse.Invoke($null, @($sample))
$content = $v.Item('choices').Item(0).Item('message').Item('content')
Check "reads a chat-completion envelope" ($content.AsString('') -eq "Cold out.`nColder in.") "got '$($content.AsString(''))'"
Check "reads usage numbers" ($v.Item('usage').Item('prompt_tokens').AsInt(0) -eq 12) "got $($v.Item('usage').Item('prompt_tokens').AsInt(0))"

$missing = $v.Item('nope').Item(3).Item('deeper')
Check "missing paths stay navigable" (-not $missing.Exists -and $missing.AsString('fallback') -eq 'fallback') "threw or returned wrong"

$html = $parse.Invoke($null, @('<html>502 Bad Gateway</html>'))
Check "an HTML error page parses to absent, not an exception" (-not $html.Exists) "reported as present"

# Kept ASCII on purpose: Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI, so a literal
# non-ASCII character here would be mangled before it ever reached the parser under test.
$tricky = 'quote" back\ slash' + "`t tab and `n newline"
$roundJson = '{"k":' + $quote.Invoke($null, @($tricky)) + '}'
$round = $parse.Invoke($null, @($roundJson))
Check "escapes survive a write/read round trip" ($round.Item('k').AsString('') -eq $tricky) "got '$($round.Item('k').AsString(''))'"

# The backslash is built from a char code rather than typed. Writing the escape literally is not
# safe here: tooling that edits this file can collapse "日" into the character it denotes, and
# Windows PowerShell 5.1 then reads those UTF-8 bytes as ANSI and hands the parser three mojibake
# characters instead of one escape. That misreports as a parser bug, which cost a debugging round.
$bs = [string][char]92
$escaped = '{"k":"' + $bs + 'u65e5' + $bs + 'u672c' + $bs + 'u8a9e"}'
$cjk = $parse.Invoke($null, @($escaped)).Item('k').AsString('')
Check "\u escapes decode to real characters" ($cjk.Length -eq 3 -and [int]$cjk[0] -eq 0x65e5) `
      "len=$($cjk.Length) codes=$(($cjk.ToCharArray() | ForEach-Object { '0x{0:x}' -f [int]$_ }) -join ' ')"

# --- 6. The model client ------------------------------------------------------------------------
Write-Host "`nModel client"
$tMsg   = $asm.GetType('Arkh.Model.ChatMessage')
$tOpts  = $asm.GetType('Arkh.Model.ModelOptions')
$tMock  = $asm.GetType('Arkh.Model.MockClient')
$tOai   = $asm.GetType('Arkh.Model.OpenAiCompatibleClient')
$listT  = [System.Collections.Generic.List[object]]

$msgSystem = $tMsg.GetMethod('System', $BF).Invoke($null, @('You are a colonist.'))
$msgUser   = $tMsg.GetMethod('User', $BF).Invoke($null, @('Say something.'))
$msgList   = [Activator]::CreateInstance([System.Collections.Generic.List[object]].Assembly.GetType('System.Collections.Generic.List`1').MakeGenericType($tMsg))
[void]$msgList.GetType().GetMethod('Add').Invoke($msgList, @($msgSystem))
[void]$msgList.GetType().GetMethod('Add').Invoke($msgList, @($msgUser))

$opts = [Activator]::CreateInstance($tOpts)
$tOpts.GetField('Model').SetValue($opts, 'gpt-4o-mini')

# Mock: deterministic, and honours the failure dial.
$mock = [Activator]::CreateInstance($tMock, @([int]7))
$r1 = $tMock.GetMethod('Complete').Invoke($mock, @($msgList, $opts))
Check "mock returns text without a network" ($r1.Ok -and $r1.Text.Length -gt 0) "ok=$($r1.Ok) text='$($r1.Text)'"

$mockA = [Activator]::CreateInstance($tMock, @([int]7))
$mockB = [Activator]::CreateInstance($tMock, @([int]7))
$a = $tMock.GetMethod('Complete').Invoke($mockA, @($msgList, $opts)).Text
$b = $tMock.GetMethod('Complete').Invoke($mockB, @($msgList, $opts)).Text
Check "mock is reproducible for a given seed" ($a -eq $b) "'$a' vs '$b'"

$alwaysFails = [Activator]::CreateInstance($tMock, @([int]7))
$tMock.GetField('FailureRate').SetValue($alwaysFails, [float]1.0)
$rf = $tMock.GetMethod('Complete').Invoke($alwaysFails, @($msgList, $opts))
Check "a forced failure is a value, not an exception" ((-not $rf.Ok) -and $null -ne $rf.Failure) "ok=$($rf.Ok)"
Check "a rate limit is marked transient" ($rf.Failure.Transient) "Kind=$($rf.Failure.Kind)"

# Request shaping: our own parser must be able to read what we send.
$NP = [Reflection.BindingFlags]'NonPublic,Instance'
$client = [Activator]::CreateInstance($tOai, @('OpenAI', 'https://example.invalid/v1', 'sk-test', $true))
$body = $tOai.GetMethod('BuildRequestBody', $NP).Invoke($client, @($msgList, $opts))
$parsedBody = $parse.Invoke($null, @($body))
Check "request body is valid JSON" ($parsedBody.Exists) "body was $body"
Check "request carries the model" ($parsedBody.Item('model').AsString('') -eq 'gpt-4o-mini') "got '$($parsedBody.Item('model').AsString(''))'"
Check "request carries both messages in order" ($parsedBody.Item('messages').Count -eq 2 -and $parsedBody.Item('messages').Item(0).Item('role').AsString('') -eq 'system') "count=$($parsedBody.Item('messages').Count)"
Check "streaming is off" ($parsedBody.Item('stream').AsBool($true) -eq $false) "stream was not false"

# Response handling, including the shapes that are not success.
$read = $tOai.GetMethod('ReadResponse', $NP)
$okResp = $read.Invoke($client, @($sample, [int]10))
Check "parses a successful completion" ($okResp.Ok -and $okResp.Text -eq "Cold out.`nColder in.") "ok=$($okResp.Ok) text='$($okResp.Text)'"
Check "records token usage" ($okResp.PromptTokens -eq 12 -and $okResp.CompletionTokens -eq 5) "$($okResp.PromptTokens)/$($okResp.CompletionTokens)"

$errResp = $read.Invoke($client, @('{"error":{"message":"You exceeded your current quota"}}', [int]10))
Check "a 200-with-error body is a failure" (-not $errResp.Ok) "reported ok"
Check "spent quota is not mistaken for a rate limit" ("$($errResp.Failure.Kind)" -eq 'QuotaExhausted') "Kind=$($errResp.Failure.Kind)"
Check "spent quota is not retried" (-not $errResp.Failure.Transient) "marked transient"

$junkResp = $read.Invoke($client, @('{"unexpected":true}', [int]10))
Check "an unexpected envelope fails cleanly" ((-not $junkResp.Ok) -and "$($junkResp.Failure.Kind)" -eq 'Malformed') "Kind=$($junkResp.Failure.Kind)"

$noKey = [Activator]::CreateInstance($tOai, @('OpenAI', 'https://example.invalid/v1', '', $true))
Check "no key is a calm state, not an error" (-not $tOai.GetProperty('Configured').GetValue($noKey)) "reported configured"

$local = [Activator]::CreateInstance($tOai, @('Ollama', 'http://localhost:11434/v1', '', $false))
Check "a local server needs no key" ($tOai.GetProperty('Configured').GetValue($local)) "reported unconfigured"

# --- 7. The response contract -------------------------------------------------------------------
# Models wrap JSON in fences, chat before it, or ignore the format and just write the line. Every
# one of those has been paid for, so every one of them has to degrade into something usable.
Write-Host "`nResponse contract"
$tContract = $asm.GetType('Arkh.Talk.ResponseContract')
$parseReply = $tContract.GetMethod('Parse', $BF)

function Read-Reply($text) { return $parseReply.Invoke($null, @($text, 'Ada')) }

$clean = Read-Reply '{"lines":[{"speaker":"Ada","text":"Cold out."}]}'
Check "reads the contracted shape" ($clean.Count -eq 1 -and $clean[0].Text -eq 'Cold out.') "count=$($clean.Count)"

$fenced = Read-Reply "``````json`n{`"lines`":[{`"speaker`":`"Bo`",`"text`":`"Not again.`"}]}`n``````"
Check "survives a code fence" ($fenced.Count -eq 1 -and $fenced[0].Speaker -eq 'Bo') "count=$($fenced.Count) speaker=$($fenced[0].Speaker)"

$chatty = Read-Reply 'Sure! Here you go: {"lines":[{"speaker":"Ada","text":"Fine."}]} Hope that helps.'
Check "survives prose around the JSON" ($chatty.Count -eq 1 -and $chatty[0].Text -eq 'Fine.') "got '$($chatty[0].Text)'"

$multi = Read-Reply '{"lines":[{"speaker":"Ada","text":"You hear that?"},{"speaker":"Bo","text":"No."}]}'
Check "reads a two-sided exchange in order" ($multi.Count -eq 2 -and $multi[1].Speaker -eq 'Bo') "count=$($multi.Count)"

$prose = Read-Reply 'The freezer door is open again.'
Check "plain prose is kept, not discarded" ($prose.Count -eq 1 -and $prose[0].Text -eq 'The freezer door is open again.') "count=$($prose.Count)"
Check "plain prose is attributed to the initiator" ($prose.Count -eq 1 -and $prose[0].Speaker -eq 'Ada') "speaker=$($prose[0].Speaker)"

$quoted = Read-Reply '{"lines":[{"speaker":"Ada","text":"\"Quoted speech.\""}]}'
Check "surrounding quotes are stripped" ($quoted[0].Text -eq 'Quoted speech.') "got '$($quoted[0].Text)'"

$empty = Read-Reply ''
Check "an empty reply yields nothing" ($empty.Count -eq 0) "count=$($empty.Count)"

$blankJson = Read-Reply '{"lines":[{"speaker":"Ada","text":"  "}]}'
Check "a blank line is not spoken" ($blankJson.Count -eq 0) "count=$($blankJson.Count)"

# --- 8. Prompt assembly --------------------------------------------------------------------------
# Assembly needs a Map for world sections and a Pawn for per-pawn ones, neither of which exists
# outside a running game. What can be checked here is the part that does not: that the core blocks
# are declared into the right slots, and that the output contract the parser expects is the one the
# prompt actually asks for.
Write-Host "`nPrompt assembly"
$tCore = $asm.GetType('Arkh.Prompt.CoreSections')
$slotsByName = @{}
foreach ($s in $T.Catalog.GetProperty('Sections', $BF).GetValue($null)) {
    $slotsByName[$s.SectionName] = "$($s.Slot)"
}
Check "a system instruction is declared" ($slotsByName['SystemInstruction'] -eq 'SystemInstruction') "slot=$($slotsByName['SystemInstruction'])"
Check "an output contract is declared" ($slotsByName['OutputContract'] -eq 'OutputContract') "slot=$($slotsByName['OutputContract'])"
Check "age voice sits in pawn context" ($slotsByName['AgeVoice'] -eq 'PawnContext') "slot=$($slotsByName['AgeVoice'])"
Check "world lore sits in world context" ($slotsByName['WorldLore'] -eq 'WorldContext') "slot=$($slotsByName['WorldLore'])"

# The prompt and the parser are two halves of one bargain; this is the check that they still agree.
$contractText = $tCore.GetField('DefaultContract', $BF).GetValue($null)
$shapeFromPrompt = Read-Reply ($contractText.Substring($contractText.IndexOf('{')))
Check "the contract we send is a shape the parser understands" ($null -ne $shapeFromPrompt) "parser rejected its own contract"
Check "the contract names lines, speaker and text" (
    $contractText.Contains('"lines"') -and $contractText.Contains('"speaker"') -and $contractText.Contains('"text"')
) "contract text drifted from the parser"

# --- 9. The display bridge -----------------------------------------------------------------------
# Interaction Bubbles takes anything that is a PlayLogEntry_Interaction out of vanilla PlayLog.Add
# and renders ToGameStringFromPOV above the initiator. Those two facts are the entire contract, and
# both are checkable here — getting either wrong would show up in game as silence.
Write-Host "`nDisplay bridge"
$tEntry = $asm.GetType('Arkh.Display.PlayLogEntry_ArkhSpeech')
Check "speech entry exists" ($null -ne $tEntry) "type not found"

$baseName = $tEntry.BaseType.Name
Check "derives from PlayLogEntry_Interaction, which is what Bubbles accepts" ($baseName -eq 'PlayLogEntry_Interaction') "base was $baseName"

$AF = [Reflection.BindingFlags]'Public,NonPublic,Instance'
$worker = $tEntry.GetMethod('ToGameStringFromPOV_Worker', $AF)
Check "overrides the text worker, not the sealed wrapper" ($null -ne $worker -and $worker.DeclaringType -eq $tEntry) `
      "declared on $($worker.DeclaringType)"

Check "has the parameterless constructor Scribe needs on load" ($null -ne $tEntry.GetConstructor([Type]::EmptyTypes)) "missing"

$ctor = $tEntry.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 }
Check "takes def, initiator, recipient and the spoken line" ($null -ne $ctor) "no 4-arg constructor"

$tDisplay = $asm.GetType('Arkh.Display.SpeechDisplay')
Check "display wiring is idempotent" ($null -ne $tDisplay.GetMethod('Wire', $BF)) "no Wire method"

# --- 10. End to end, through the connection test --------------------------------------------------
# The widest check here: builds the real instruction and contract, sends them through a real client,
# and reads the reply back through the real parser. Against the mock that is the whole pipeline bar
# pawn selection and drawing, exercised with no game and no bill.
Write-Host "`nEnd to end (connection test, mock provider)"
$tTest = $asm.GetType('Arkh.Model.ConnectionTest')
$startTest = $tTest.GetMethod('Start', $BF)
$currentProp = $tTest.GetProperty('Current', $BF)

function Wait-Test($timeoutMs = 8000) {
    $waited = 0
    while ("$($currentProp.GetValue($null).Status)" -eq 'Running' -and $waited -lt $timeoutMs) {
        Start-Sleep -Milliseconds 50
        $waited += 50
    }
    return $currentProp.GetValue($null)
}

$settings.Provider = 0          # Mock
$settings.MockFailureRate = [float]0.0
$settings.MockDelayMs = 0
[void]$startTest.Invoke($null, @($settings))
$ok = Wait-Test
Check "a healthy provider reports success" ("$($ok.Status)" -eq 'Succeeded') "status=$($ok.Status) summary=$($ok.Summary)"
Check "success reports what was actually parsed as speech" ($ok.Detail -like '*Parsed as speech*') "detail=$($ok.Detail)"

$settings.MockFailureRate = [float]1.0
[void]$startTest.Invoke($null, @($settings))
$bad = Wait-Test
Check "a failing provider reports failure, not a hang" ("$($bad.Status)" -eq 'Failed') "status=$($bad.Status)"
Check "the failure names something a player can act on" (-not [string]::IsNullOrWhiteSpace($bad.Summary)) "summary was empty"

$settings.Provider = 1          # OpenAI, with no key configured
$settings.ApiKey = ''
[void]$startTest.Invoke($null, @($settings))
$unset = Wait-Test
Check "an unconfigured provider says so instead of calling out" (
    "$($unset.Status)" -eq 'Failed' -and $unset.Summary -like '*not configured*'
) "status=$($unset.Status) summary=$($unset.Summary)"

$settings.Provider = 0
$settings.MockFailureRate = [float]0.0

Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "All smoke checks passed." -ForegroundColor Green
    exit 0
}
Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
exit 1
