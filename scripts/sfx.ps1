<#
.SYNOPSIS
    Generates a game sound file with the ElevenLabs text-to-sound-effects API.

.DESCRIPTION
    Fills one of the sound slots declared by
    src/Hukbo.Client/Audio/SoundCatalog.cs with an uncompressed PCM WAV file
    written into src/Hukbo.Client/Content/Audio/.

    The slot list is parsed out of SoundCatalog.cs, so the catalog stays the
    single source of truth for file names and this script cannot drift from it.

    Nothing in the game calls this script. It is an authoring tool: the client
    only ever reads whatever WAV files happen to be in the content folder, so
    the simulation, the build, and the canonical gate remain fully offline.

.PARAMETER Slot
    The sound slot to fill, for example 'death' or 'attack-kampilan'.
    Run with -List to see every slot and whether it already has a file.

.PARAMETER Prompt
    The description sent to ElevenLabs. Omit it to use this script's default
    prompt for the slot.

.PARAMETER Duration
    Requested length in seconds, between 0.5 and 30. Defaults to the slot's
    recommended length. The API's own floor is 0.5 seconds, so a combat hit
    always arrives longer than it should sound; trailing silence is trimmed
    afterwards unless -NoTrim is set.

.PARAMETER Trim
    Forces quiet-run trimming on for a slot whose default is off.

.PARAMETER NoTrim
    Forces trimming off, keeping the generation exactly as returned.

.PARAMETER AllowQuiet
    Keeps a take that peaks below ten percent of full scale. By default such a
    take is rejected without writing anything, because the model occasionally
    returns near-silent audio and that must never replace a good file.

.PARAMETER SilenceThreshold
    The percentage of the file's own peak below which audio counts as quiet
    when trimming. Defaults to 5. A generation never contains true digital
    silence — it has a room-tone floor of a few percent — so this is measured
    against the peak rather than against zero.

.PARAMETER PromptInfluence
    Between 0 and 1. Higher values follow the prompt more literally and produce
    less variety. Defaults to 0.4.

.PARAMETER SampleRate
    PCM sample rate. 44100 requires an ElevenLabs Pro subscription or above;
    24000 is the default because it works on lower tiers and is plenty for
    short combat hits.

.PARAMETER Channels
    Overrides channel detection. By default the script infers the channel count
    from the returned byte count and the requested duration.

.PARAMETER OutputDirectory
    Where the WAV file is written. Defaults to src/Hukbo.Client/Content/Audio.

.PARAMETER Force
    Overwrites an existing file for the slot. Without it, an existing file is
    left alone and the script fails.

.PARAMETER List
    Prints every slot, its default prompt, and whether a file already exists,
    then exits without calling the API.

.PARAMETER DryRun
    Resolves the slot, prompt, and output path and prints the request that
    would be sent, without calling the API. In -Batch mode, lists every
    variant that would be requested instead of a single request.

.PARAMETER Batch
    Reads a manifest CSV (the shape scripts/sfx-manifest.ps1 writes: at least
    BaseFileName, VariantCount, DurationSeconds, and Prompt columns) and
    requests every variant of every row in one run, instead of the single
    slot scripts/sfx.ps1 -Slot handles. Existing files are left alone unless
    -Force is set. Requires -ManifestPath and -OutputDirectory; this script
    does not assume which project's content folder a batch run belongs to.

.PARAMETER ManifestPath
    Path to the manifest CSV consumed by -Batch.

.EXAMPLE
    ./scripts/sfx.ps1 -List

.EXAMPLE
    ./scripts/sfx.ps1 -Slot death

.EXAMPLE
    ./scripts/sfx.ps1 -Slot attack-kampilan -Prompt 'single heavy steel blade cleaving flesh, wet impact, no music' -Duration 0.4 -Force

.EXAMPLE
    ./scripts/sfx.ps1 -Batch -ManifestPath artifacts/audio/sandata-sound-manifest.csv -OutputDirectory src/Sandata.Client/Content/Audio -DryRun
#>
[CmdletBinding(DefaultParameterSetName = 'Generate')]
param(
    [Parameter(ParameterSetName = 'Generate', Position = 0)]
    [string] $Slot,

    [Parameter(ParameterSetName = 'Generate')]
    [string] $Prompt,

    [Parameter(ParameterSetName = 'Generate')]
    [ValidateSet('skull', 'neck', 'ribcage', 'gut', 'limb', 'extremity')]
    [string] $Class,

    [Parameter(ParameterSetName = 'Generate')]
    [ValidateRange(1, 99)]
    [int] $Index,

    [Parameter(ParameterSetName = 'Generate')]
    [ValidateRange(0.5, 30.0)]
    [double] $Duration,

    [Parameter(ParameterSetName = 'Batch', Mandatory)]
    [switch] $Batch,

    [Parameter(ParameterSetName = 'Batch', Mandatory)]
    [string] $ManifestPath,

    [ValidateRange(0.0, 1.0)]
    [double] $PromptInfluence = 0.4,

    [ValidateSet(16000, 22050, 24000, 44100)]
    [int] $SampleRate = 24000,

    [ValidateSet(1, 2)]
    [int] $Channels,

    [string] $OutputDirectory,

    [switch] $Trim,

    [switch] $NoTrim,

    [ValidateRange(0.5, 50.0)]
    [double] $SilenceThreshold = 5.0,

    [switch] $AllowQuiet,

    [switch] $Force,

    [switch] $DryRun,

    [Parameter(ParameterSetName = 'List', Mandatory)]
    [switch] $List
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-RepositoryRoot
$catalogPath = Join-Path $root 'src/Hukbo.Client/Audio/SoundCatalog.cs'
$defaultOutputDirectory = Join-Path $root 'src/Hukbo.Client/Content/Audio'
$provenancePath = Join-Path $defaultOutputDirectory 'GENERATED.md'

$apiEndpoint = 'https://api.elevenlabs.io/v1/sound-generation'
$modelId = 'eleven_text_to_sound_v2'
$bitsPerSample = 16

# The API refuses anything shorter, so a tenth-of-a-second hit is generated at
# this length and then trimmed back down.
$minimumApiDuration = 0.5

# A take peaking below this is rejected. Full scale is 32767, so this is ten
# percent of it, roughly -20 dBFS. A usable generation lands far above it.
$minimumPeakAmplitude = 3277

# Concurrent generation runs draw rate-limit responses, so retry rather than
# throwing the take away.
$rateLimitRetries = 6

# Kept either side of the audible part so trimming cannot land on a hard edge.
$trimTailSeconds = 0.01
$trimLeadSeconds = 0.005

# Starting points, not house style. Every one of these is meant to be replaced
# by a better -Prompt once someone hears the result in a real battle.
# An attack event only exists when the attack landed, so every combat prompt is
# a contact. A prompt that produces a blade cutting empty air is wrong for this
# game. Each of these is the weapon's most common hit, and each doubles as the
# single-file fallback for its slot.
$defaultPrompts = @{
    'attack-kampilan' = @{
        Prompt   = 'one heavy two-handed blade cutting deep through a neck, thick wet sever with a brief metallic shiver in the steel, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'attack-wasay'    = @{
        Prompt   = 'one heavy hafted axe cleaving a shoulder, wet meat with a hard joint break, no ring, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'attack-kalis'    = @{
        Prompt   = 'one narrow blade sinking into a belly, soft deep wet entry with no bone, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'attack-itak'     = @{
        Prompt   = 'one short light blade slashing a forearm, fast light cut with a thin bone tick, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    # A shield block is the same four weapons hitting a light wooden board
    # instead of a body, so these prompts are the opposite of the four above:
    # dry wood rather than wet meat, and no metal at all. Generate them with
    # -PromptInfluence 0.5 rather than the 0.4 default, because the negative
    # is the whole point of the slot.
    'clash-shield-kampilan' = @{
        Prompt   = 'one heavy two-handed blade slamming hard into a large light wooden shield, loud close impact, deep board thud with a sharp woody bite in front of it, dry rattan-bound plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'clash-shield-wasay'    = @{
        Prompt   = 'one heavy axe head slamming hard into a large light wooden shield, loud close impact, sharp blunt crack with splitting wood fibres, dry plank break, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'clash-shield-kalis'    = @{
        Prompt   = 'one narrow blade point punching into a light wooden shield, tight woody punch skidding off the board face with a thin rattan buzz, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'clash-shield-itak'     = @{
        Prompt   = 'one short light blade tapping a large light wooden shield, quick shallow dry woody clack on a thin plank, dry packed earth, open air, very short, no ring, no metal, no reverb, no music, no voice'
        Duration = 0.5
        Trim     = $true
    }
    'death'                  = @{
        Prompt   = 'a body collapsing onto dry packed earth, dull heavy thud with a short scrape of cloth and gear, no music, no voice'
        Duration = 0.7
        Trim     = $true
    }
    # The outcome and interface cues are short, soft notification sounds in the
    # manner of a messaging app, not ceremonial instruments. They are the only
    # non-diegetic sounds in the game. A pitched tone decays smoothly, so they
    # trim at a gentler threshold than a combat hit: 5% of peak audibly chops
    # the tail, 2% only removes the dead air after it.
    'victory-blue'           = @{
        Prompt    = 'two soft glass notes rising a fifth, gentle bright interface notification, very short, clean decay, no reverb tail, no music, no voice'
        Duration  = 0.5
        Trim      = $true
        Threshold = 2.0
    }
    'victory-red'            = @{
        Prompt    = 'two soft mallet notes rising a fifth in a low warm register, gentle interface notification, very short, clean decay, no reverb tail, no music, no voice'
        Duration  = 0.5
        Trim      = $true
        Threshold = 2.0
    }
    'draw'                   = @{
        Prompt    = 'two soft muted notes at the same pitch, flat and unresolved interface notification, very short, dry, no reverb tail, no music, no voice'
        Duration  = 0.5
        Trim      = $true
        Threshold = 2.0
    }
    'ui-click'               = @{
        Prompt    = 'one very short soft tap, subtle interface tick, tiny and clean, almost no tone, no reverb, no music, no voice'
        Duration  = 0.5
        Trim      = $true
        Threshold = 2.0
    }
}

# Per-family trim thresholds for -Batch mode, keyed by the family token that
# leads a manifest base file name (for example "gun-556x45-single-close" has
# family token "gun"). A gunshot's report ends in a sharp muzzle crack with a
# quiet, gently decaying tail; trimming that tail at the 5% melee default
# chops it hard and audibly. A tighter 1.5% threshold keeps far more of the
# natural decay for every gunfire family, while non-gunfire families keep the
# single-slot script's existing 5% (or 2% for the short UI-style notification
# tones scripts/sfx.ps1 already generates for Hukbo).
$familyTrimThresholds = @{
    'gun'     = 1.5
    'gunloop' = 1.5
    'guntail' = 1.5
    'mech'    = 5.0
    'dry'     = 5.0
    'impact'  = 5.0
    'casing'  = 5.0
    'ui'      = 2.0
}
$defaultFamilyTrimThreshold = 5.0

function Get-FamilyTrimThreshold {
    <#
        The first hyphen-separated token of a manifest base file name is its
        family (scripts/sfx-manifest.ps1's own naming convention). Unknown
        families fall back to the melee default rather than throwing, so a
        manifest from a catalog this script has never seen still runs.
    #>
    param(
        [Parameter(Mandatory)] [string] $BaseFileName
    )

    $familyToken = $BaseFileName.Split('-')[0]
    if ($familyTrimThresholds.ContainsKey($familyToken)) {
        return $familyTrimThresholds[$familyToken]
    }

    return $defaultFamilyTrimThreshold
}

function Get-CatalogSlot {
    if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
        throw "The sound catalog was not found at $catalogPath."
    }

    $catalogText = Get-Content -Raw -LiteralPath $catalogPath
    $slotMatches = [regex]::Matches($catalogText, 'GameSoundId\.\w+\s*=>\s*"([a-z0-9-]+)"')
    if ($slotMatches.Count -eq 0) {
        throw "No slot names could be parsed from $catalogPath. The catalog's GetBaseName shape changed; update this script to match."
    }

    return @($slotMatches | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
}

function Get-SlotPath {
    <#
        Builds "<slot>[-<class>][-NN].wav". The hit class lives in the file name
        rather than in a code-side table so the mapping between a body part and
        the sound it makes cannot silently drift from the files on disk.
    #>
    param(
        [Parameter(Mandatory)] [string] $SlotName,
        [Parameter(Mandatory)] [string] $Directory,
        [string] $ClassName,
        [int] $VariantIndex
    )

    $name = $SlotName
    if (-not [string]::IsNullOrWhiteSpace($ClassName)) {
        $name += "-$ClassName"
    }

    if ($VariantIndex -gt 0) {
        $name += '-{0:D2}' -f $VariantIndex
    }

    return Join-Path $Directory "$name.wav"
}

function Add-ProvenanceRow {
    <#
        Several generation runs append here at once, so a plain Add-Content
        loses rows to file-sharing collisions. Retries around the lock instead.
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Row,

        # AllowEmptyString because the header contains blank separator lines,
        # and a mandatory string array rejects an empty element without it.
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string[]] $HeaderLines
    )

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
                $stream = [System.IO.File]::Open(
                    $Path,
                    [System.IO.FileMode]::CreateNew,
                    [System.IO.FileAccess]::Write,
                    [System.IO.FileShare]::None)
                try {
                    $writer = [System.IO.StreamWriter]::new($stream)
                    try {
                        foreach ($line in $HeaderLines) {
                            $writer.WriteLine($line)
                        }
                    }
                    finally {
                        $writer.Dispose()
                    }
                }
                finally {
                    $stream.Dispose()
                }
            }

            $appendStream = [System.IO.File]::Open(
                $Path,
                [System.IO.FileMode]::Append,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None)
            try {
                $appendWriter = [System.IO.StreamWriter]::new($appendStream)
                try {
                    $appendWriter.WriteLine($Row)
                }
                finally {
                    $appendWriter.Dispose()
                }
            }
            finally {
                $appendStream.Dispose()
            }

            return
        }
        catch [System.IO.IOException] {
            Start-Sleep -Milliseconds (100 * $attempt)
        }
    }

    Write-Host "[WARN] Could not append the provenance row for $Row" -ForegroundColor Yellow
}

function Get-ApiKey {
    # The environment wins, so a session override never needs a file edit.
    if (-not [string]::IsNullOrWhiteSpace($env:ELEVENLABS_API_KEY)) {
        return $env:ELEVENLABS_API_KEY.Trim()
    }

    $envFilePath = Join-Path $root '.env'
    if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
        return $null
    }

    foreach ($line in (Get-Content -LiteralPath $envFilePath)) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
            continue
        }

        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) {
            continue
        }

        if ($trimmed.Substring(0, $separator).Trim() -ne 'ELEVENLABS_API_KEY') {
            continue
        }

        $value = $trimmed.Substring($separator + 1).Trim().Trim('"', "'")
        if ([string]::IsNullOrWhiteSpace($value)) {
            return $null
        }

        return $value
    }

    return $null
}

function Get-PeakAmplitude {
    param(
        [Parameter(Mandatory)] [byte[]] $PcmData
    )

    # Widened to int before Abs: a sample of exactly Int16.MinValue has no
    # positive Int16 counterpart, and Math.Abs would throw on the loudest
    # possible take rather than accepting it.
    $peak = 0
    for ($offset = 0; $offset -lt $PcmData.Length - 1; $offset += 2) {
        $sample = [System.Math]::Abs([int] [BitConverter]::ToInt16($PcmData, $offset))
        if ($sample -gt $peak) {
            $peak = $sample
        }
    }

    return $peak
}

function Remove-Silence {
    <#
        Cuts the quiet run at each end of the audio. A generation always has a
        noise floor rather than true digital silence, so "quiet" is measured
        against the file's own peak, not against zero.
    #>
    param(
        [Parameter(Mandatory)] [byte[]] $PcmData,
        [Parameter(Mandatory)] [int] $Rate,
        [Parameter(Mandatory)] [int] $ChannelCount,
        [Parameter(Mandatory)] [double] $ThresholdRatio
    )

    $bytesPerFrame = $ChannelCount * ($bitsPerSample / 8)
    $frameCount = [int] ($PcmData.Length / $bytesPerFrame)
    if ($frameCount -lt 2) {
        return $PcmData
    }

    $peak = 0
    for ($frame = 0; $frame -lt $frameCount; $frame++) {
        for ($channel = 0; $channel -lt $ChannelCount; $channel++) {
            $sample = [System.Math]::Abs([int] [BitConverter]::ToInt16($PcmData, ($frame * $bytesPerFrame) + ($channel * 2)))
            if ($sample -gt $peak) {
                $peak = $sample
            }
        }
    }

    if ($peak -eq 0) {
        return $PcmData
    }

    $threshold = [int] [System.Math]::Max($peak * $ThresholdRatio, 8)

    $firstAudibleFrame = -1
    for ($frame = 0; $frame -lt $frameCount; $frame++) {
        for ($channel = 0; $channel -lt $ChannelCount; $channel++) {
            if ([System.Math]::Abs([int] [BitConverter]::ToInt16($PcmData, ($frame * $bytesPerFrame) + ($channel * 2))) -ge $threshold) {
                $firstAudibleFrame = $frame
                break
            }
        }

        if ($firstAudibleFrame -ge 0) {
            break
        }
    }

    if ($firstAudibleFrame -lt 0) {
        return $PcmData
    }

    $lastAudibleFrame = $firstAudibleFrame
    for ($frame = $frameCount - 1; $frame -ge $firstAudibleFrame; $frame--) {
        $loud = $false
        for ($channel = 0; $channel -lt $ChannelCount; $channel++) {
            if ([System.Math]::Abs([int] [BitConverter]::ToInt16($PcmData, ($frame * $bytesPerFrame) + ($channel * 2))) -ge $threshold) {
                $loud = $true
                break
            }
        }

        if ($loud) {
            $lastAudibleFrame = $frame
            break
        }
    }

    $startFrame = [System.Math]::Max(0, $firstAudibleFrame - [int] ($Rate * $trimLeadSeconds))
    $endFrame = [System.Math]::Min($frameCount, $lastAudibleFrame + 1 + [int] ($Rate * $trimTailSeconds))
    $keepFrames = $endFrame - $startFrame
    if ($keepFrames -ge $frameCount -or $keepFrames -lt 1) {
        return $PcmData
    }

    $trimmed = [byte[]]::new($keepFrames * $bytesPerFrame)
    [System.Array]::Copy($PcmData, $startFrame * $bytesPerFrame, $trimmed, 0, $trimmed.Length)
    return $trimmed
}

function Write-WavFile {
    param(
        [Parameter(Mandatory)] [byte[]] $PcmData,
        [Parameter(Mandatory)] [int] $Rate,
        [Parameter(Mandatory)] [int] $ChannelCount,
        [Parameter(Mandatory)] [string] $Path
    )

    if ($PcmData.Length -eq 0) {
        throw 'The API returned an empty audio body.'
    }

    $blockAlign = $ChannelCount * ($bitsPerSample / 8)
    if (($PcmData.Length % $blockAlign) -ne 0) {
        throw "The returned PCM length $($PcmData.Length) is not a whole number of $ChannelCount-channel 16-bit frames. Re-run with an explicit -Channels value."
    }

    $stream = [System.IO.File]::Create($Path)
    try {
        $writer = [System.IO.BinaryWriter]::new($stream)
        try {
            $ascii = [System.Text.Encoding]::ASCII
            $writer.Write($ascii.GetBytes('RIFF'))
            $writer.Write([int] (36 + $PcmData.Length))
            $writer.Write($ascii.GetBytes('WAVE'))
            $writer.Write($ascii.GetBytes('fmt '))
            $writer.Write([int] 16)
            $writer.Write([int16] 1)                                     # PCM, uncompressed
            $writer.Write([int16] $ChannelCount)
            $writer.Write([int] $Rate)
            $writer.Write([int] ($Rate * $blockAlign))                   # byte rate
            $writer.Write([int16] $blockAlign)
            $writer.Write([int16] $bitsPerSample)
            $writer.Write($ascii.GetBytes('data'))
            $writer.Write([int] $PcmData.Length)
            $writer.Write($PcmData)
            $writer.Flush()
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-SoundGeneration {
    <#
        The one place this script talks to the network: requests one
        generation, retries a rate limit, rejects a too-quiet take, trims
        it, and writes the WAV. Both the single -Slot path and the -Batch
        path call this after each has resolved its own prompt, duration,
        trim decision, and destination path, so a request is built and sent
        exactly the same way regardless of which mode reached it.
    #>
    param(
        [Parameter(Mandatory)] [string] $Prompt,
        [Parameter(Mandatory)] [double] $Duration,
        [Parameter(Mandatory)] [double] $PromptInfluenceValue,
        [Parameter(Mandatory)] [int] $SampleRateValue,

        # 0 means "not supplied"; a real channel count is always 1 or 2, so
        # 0 is a safe sentinel for "infer it from the returned byte count".
        [int] $ChannelsValue,

        [Parameter(Mandatory)] [bool] $ShouldTrimValue,
        [Parameter(Mandatory)] [double] $SilenceThresholdValue,
        [Parameter(Mandatory)] [bool] $AllowQuietValue,
        [Parameter(Mandatory)] [string] $ApiKeyValue,
        [Parameter(Mandatory)] [string] $DestinationPath
    )

    $body = [ordered] @{
        text             = $Prompt
        model_id         = $modelId
        duration_seconds = $Duration
        prompt_influence = $PromptInfluenceValue
    } | ConvertTo-Json -Depth 3

    $requestUri = "$($apiEndpoint)?output_format=pcm_$SampleRateValue"
    $temporaryPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "hukbo-sfx-$([System.Guid]::NewGuid()).pcm")

    try {
        # Several slots are commonly generated at once, so a rate-limit
        # response is expected rather than exceptional. Back off and retry
        # rather than failing the whole run and losing the take.
        for ($attempt = 1; $attempt -le $rateLimitRetries; $attempt++) {
            try {
                Invoke-WebRequest `
                    -Uri $requestUri `
                    -Method Post `
                    -Headers @{ 'xi-api-key' = $ApiKeyValue } `
                    -ContentType 'application/json' `
                    -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
                    -OutFile $temporaryPath `
                    -MaximumRedirection 0 | Out-Null

                break
            }
            catch {
                $statusCode = 0
                if ($null -ne $_.Exception.Response) {
                    $statusCode = [int] $_.Exception.Response.StatusCode
                }

                $isRetryable = $statusCode -eq 429 -or ($statusCode -ge 500 -and $statusCode -le 599)
                if (-not $isRetryable -or $attempt -eq $rateLimitRetries) {
                    throw
                }

                $backoffSeconds = [System.Math]::Min(30, [System.Math]::Pow(2, $attempt))
                Write-Host "[INFO] HTTP $statusCode. Waiting ${backoffSeconds}s, then retrying (attempt $attempt of $rateLimitRetries)."
                Start-Sleep -Seconds $backoffSeconds
            }
        }

        $pcm = [System.IO.File]::ReadAllBytes($temporaryPath)
        if ($pcm.Length -lt 1024) {
            throw "The API returned only $($pcm.Length) bytes, which is not usable audio. The request may have been rejected."
        }

        $resolvedChannels = $ChannelsValue
        if ($resolvedChannels -ne 1 -and $resolvedChannels -ne 2) {
            # Raw PCM carries no header, so the channel count is inferred
            # from how many 16-bit frames came back for the requested
            # duration.
            $expectedMonoBytes = $SampleRateValue * ($bitsPerSample / 8) * $Duration
            $ratio = $pcm.Length / $expectedMonoBytes
            if ($ratio -lt 1.5) {
                $resolvedChannels = 1
            }
            elseif ($ratio -lt 2.5) {
                $resolvedChannels = 2
            }
            else {
                throw "Returned $($pcm.Length) bytes, which is $([math]::Round($ratio, 2))x the mono size for ${Duration}s at $SampleRateValue Hz. Re-run with an explicit -Channels value."
            }
        }

        # The model sometimes returns a take that is technically valid audio
        # and far too quiet to hear in a battle. Catching it here is what
        # stops -Force from replacing a good file with a dud.
        $peak = Get-PeakAmplitude -PcmData $pcm
        $peakPercent = [math]::Round(100.0 * $peak / 32767.0, 1)
        if ($peak -lt $minimumPeakAmplitude -and -not $AllowQuietValue) {
            throw "This take peaks at only $peakPercent% of full scale, which is too quiet to hear over a battle. Nothing was written, so any existing file at $DestinationPath is untouched. Run the same command again for another take, or pass -AllowQuiet to keep this one."
        }

        Write-Host "[INFO] Peak level: $peakPercent% of full scale."

        $generatedSeconds = [math]::Round($pcm.Length / ($SampleRateValue * $resolvedChannels * ($bitsPerSample / 8)), 2)
        if ($ShouldTrimValue) {
            $trimmed = Remove-Silence `
                -PcmData $pcm `
                -Rate $SampleRateValue `
                -ChannelCount $resolvedChannels `
                -ThresholdRatio ($SilenceThresholdValue / 100.0)

            if ($trimmed.Length -lt $pcm.Length) {
                Write-Host ("[INFO] Trimmed to the audible part at {0}% of peak: {1}s generated, {2}s kept. Use -NoTrim to keep it all." -f `
                        $SilenceThresholdValue,
                    $generatedSeconds,
                    [math]::Round($trimmed.Length / ($SampleRateValue * $resolvedChannels * ($bitsPerSample / 8)), 2))
                $pcm = $trimmed
            }
            else {
                Write-Host "[INFO] Nothing to trim: the audio is audible from end to end at $SilenceThresholdValue% of peak."
            }
        }

        Write-WavFile -PcmData $pcm -Rate $SampleRateValue -ChannelCount $resolvedChannels -Path $DestinationPath

        $keptSeconds = [math]::Round($pcm.Length / ($SampleRateValue * $resolvedChannels * ($bitsPerSample / 8)), 2)
        $kilobytes = [math]::Round($pcm.Length / 1024.0, 1)
        Write-Host "[PASS] Wrote $DestinationPath ($kilobytes KB, ${keptSeconds}s, $resolvedChannels channel(s), $SampleRateValue Hz, 16-bit PCM)."

        return [pscustomobject] @{
            KeptSeconds = $keptSeconds
            Channels    = $resolvedChannels
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
}

$slots = Get-CatalogSlot

if ($List) {
    Write-Host 'Hukbo sound slots'
    Write-Host "Catalog: $catalogPath"
    Write-Host "Folder:  $defaultOutputDirectory"
    Write-Host ''
    foreach ($name in $slots) {
        $exists = Test-Path -LiteralPath (Get-SlotPath -SlotName $name -Directory $defaultOutputDirectory) -PathType Leaf
        $status = if ($exists) { 'PRESENT' } else { 'MISSING' }
        $defaultPrompt = if ($defaultPrompts.ContainsKey($name)) { $defaultPrompts[$name].Prompt } else { '(no default prompt)' }
        Write-Host ("[{0}] {1}" -f $status, $name)
        Write-Host ("          {0}" -f $defaultPrompt)
    }

    Write-Host ''
    Write-Host 'Generate one with: ./scripts/sfx.ps1 -Slot <name> [-Prompt "..."] [-Force]'
    return
}

if ($Batch) {
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "No manifest was found at $ManifestPath. Generate one first (for example, ./scripts/sfx-manifest.ps1)."
    }

    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        throw '-OutputDirectory is required with -Batch. This script does not assume which project a manifest belongs to, so it will not guess a content folder.'
    }

    if ($Trim -and $NoTrim) {
        throw 'Pass either -Trim or -NoTrim, not both.'
    }

    if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    }

    $manifestRows = @(Import-Csv -LiteralPath $ManifestPath)
    if ($manifestRows.Count -eq 0) {
        throw "$ManifestPath contains no rows."
    }

    foreach ($requiredColumn in @('BaseFileName', 'VariantCount', 'DurationSeconds', 'Prompt')) {
        if ($null -eq ($manifestRows[0].PSObject.Properties[$requiredColumn])) {
            throw "$ManifestPath has no '$requiredColumn' column. Expected the shape scripts/sfx-manifest.ps1 writes."
        }
    }

    Write-Host "Batch:    $ManifestPath ($($manifestRows.Count) rows)"
    Write-Host "Output:   $OutputDirectory"
    Write-Host ("Trim:     {0}" -f $(if ($NoTrim) { 'no, every generation is kept in full' } else { 'per-family threshold (gunfire 1.5%, UI 2%, everything else 5%), unless -SilenceThreshold overrides it' }))
    if ($DryRun) {
        Write-Host '[INFO] -DryRun was set; listing planned generations without calling the API.'
    }

    $apiKey = $null
    if (-not $DryRun) {
        $apiKey = Get-ApiKey
        if ([string]::IsNullOrWhiteSpace($apiKey)) {
            throw @'
No ElevenLabs API key was found. Provide it either way:

    ELEVENLABS_API_KEY=<your key>        in the repository's .env file
    $env:ELEVENLABS_API_KEY = '<key>'    for the current shell session only

.env is ignored by .gitignore and must stay that way. Never commit the key,
never put it in a tracked file, and never pass it on the command line where it
lands in shell history.
'@
        }
    }

    $channelsForBatch = 0
    if ($PSBoundParameters.ContainsKey('Channels')) {
        $channelsForBatch = $Channels
    }

    $generatedCount = 0
    $skippedCount = 0

    foreach ($row in $manifestRows) {
        $baseFileName = $row.BaseFileName
        $variantCount = [int] $row.VariantCount
        $rowPrompt = $row.Prompt
        $rowDuration = [System.Math]::Max([double] $row.DurationSeconds, $minimumApiDuration)

        $rowTrimThreshold = if ($PSBoundParameters.ContainsKey('SilenceThreshold')) { $SilenceThreshold } else { Get-FamilyTrimThreshold -BaseFileName $baseFileName }
        $rowShouldTrim = -not $NoTrim

        for ($variantIndex = 1; $variantIndex -le $variantCount; $variantIndex++) {
            $variantFileName = '{0}-{1:D2}.wav' -f $baseFileName, $variantIndex
            $variantPath = Join-Path $OutputDirectory $variantFileName

            if ((Test-Path -LiteralPath $variantPath -PathType Leaf) -and -not $Force) {
                $skippedCount++
                continue
            }

            if ($DryRun) {
                Write-Host ("[DRYRUN] {0}  ({1}s, trim {2}% of peak)  {3}" -f $variantFileName, $rowDuration, $rowTrimThreshold, $rowPrompt)
                continue
            }

            Write-Host ''
            Write-Host "Slot:     $baseFileName variant $variantIndex of $variantCount"
            Write-Host "Output:   $variantPath"
            Write-Host ("Duration: {0}s at {1} Hz, prompt influence {2}" -f $rowDuration, $SampleRate, $PromptInfluence)
            Write-Host ("Trim:     {0}" -f $(if ($rowShouldTrim) { "yes, below $rowTrimThreshold% of peak" } else { 'no, the full generation is kept' }))
            Write-Host "Prompt:   $rowPrompt"

            try {
                Invoke-SoundGeneration `
                    -Prompt $rowPrompt `
                    -Duration $rowDuration `
                    -PromptInfluenceValue $PromptInfluence `
                    -SampleRateValue $SampleRate `
                    -ChannelsValue $channelsForBatch `
                    -ShouldTrimValue $rowShouldTrim `
                    -SilenceThresholdValue $rowTrimThreshold `
                    -AllowQuietValue ([bool] $AllowQuiet) `
                    -ApiKeyValue $apiKey `
                    -DestinationPath $variantPath | Out-Null

                $generatedCount++
            }
            catch {
                Write-Host "[FAIL] $variantFileName : $($_.Exception.Message)" -ForegroundColor Red
                throw
            }
        }
    }

    Write-Host ''
    if ($DryRun) {
        Write-Host "[INFO] -DryRun complete: $($manifestRows.Count) manifest rows read; no request was sent and no file was written."
    }
    else {
        Write-Host "[PASS] Batch complete: $generatedCount file(s) generated, $skippedCount already present and skipped (re-run with -Force to replace)."
    }

    return
}

if ([string]::IsNullOrWhiteSpace($Slot)) {
    throw 'A -Slot is required. Run ./scripts/sfx.ps1 -List to see the available slots.'
}

$Slot = $Slot.Trim().ToLowerInvariant()
if ($Slot.EndsWith('.wav')) {
    $Slot = $Slot.Substring(0, $Slot.Length - 4)
}

if ($slots -notcontains $Slot) {
    throw "'$Slot' is not a sound slot. The game ignores any other file name. Available slots: $($slots -join ', ')."
}

if ([string]::IsNullOrWhiteSpace($Prompt)) {
    if (-not $defaultPrompts.ContainsKey($Slot)) {
        throw "Slot '$Slot' has no default prompt in this script. Pass -Prompt explicitly."
    }

    $Prompt = $defaultPrompts[$Slot].Prompt
}

if (-not $PSBoundParameters.ContainsKey('Duration')) {
    $slotDuration = if ($defaultPrompts.ContainsKey($Slot)) { $defaultPrompts[$Slot].Duration } else { 1.0 }
    $Duration = [System.Math]::Max($slotDuration, $minimumApiDuration)
}

if ($Trim -and $NoTrim) {
    throw 'Pass either -Trim or -NoTrim, not both.'
}

if ($Trim) {
    $shouldTrim = $true
}
elseif ($NoTrim) {
    $shouldTrim = $false
}
else {
    $shouldTrim = $defaultPrompts.ContainsKey($Slot) -and $defaultPrompts[$Slot].Trim
}

# A slot may carry its own trim threshold; an explicit -SilenceThreshold wins.
if (-not $PSBoundParameters.ContainsKey('SilenceThreshold') -and
    $defaultPrompts.ContainsKey($Slot) -and
    $defaultPrompts[$Slot].ContainsKey('Threshold')) {
    $SilenceThreshold = $defaultPrompts[$Slot].Threshold
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = $defaultOutputDirectory
}

if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

if (-not [string]::IsNullOrWhiteSpace($Class) -and -not $Slot.StartsWith('attack-')) {
    throw "-Class applies only to an attack slot. '$Slot' events carry no hit location, so a class would name a file the game can never select."
}

$outputPath = Get-SlotPath `
    -SlotName $Slot `
    -Directory $OutputDirectory `
    -ClassName $Class `
    -VariantIndex $Index

if ((Test-Path -LiteralPath $outputPath -PathType Leaf) -and -not $Force) {
    throw "$outputPath already exists. Re-run with -Force to replace it."
}

Write-Host "Slot:     $Slot"
Write-Host "Output:   $outputPath"
Write-Host "Model:    $modelId"
Write-Host ("Duration: {0}s at {1} Hz, prompt influence {2}" -f $Duration, $SampleRate, $PromptInfluence)
Write-Host ("Trim:     {0}" -f $(if ($shouldTrim) { "yes, below $SilenceThreshold% of peak" } else { 'no, the full generation is kept' }))
Write-Host "Prompt:   $Prompt"

if ($DryRun) {
    Write-Host '[INFO] -DryRun was set; no request was sent and no file was written.'
    return
}

$apiKey = Get-ApiKey
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw @'
No ElevenLabs API key was found. Provide it either way:

    ELEVENLABS_API_KEY=<your key>        in the repository's .env file
    $env:ELEVENLABS_API_KEY = '<key>'    for the current shell session only

.env is ignored by .gitignore and must stay that way. Never commit the key,
never put it in a tracked file, and never pass it on the command line where it
lands in shell history.
'@
}

Write-Host 'Requesting audio from ElevenLabs...'

$channelsForSlot = 0
if ($PSBoundParameters.ContainsKey('Channels')) {
    $channelsForSlot = $Channels
}

try {
    $result = Invoke-SoundGeneration `
        -Prompt $Prompt `
        -Duration $Duration `
        -PromptInfluenceValue $PromptInfluence `
        -SampleRateValue $SampleRate `
        -ChannelsValue $channelsForSlot `
        -ShouldTrimValue ([bool] $shouldTrim) `
        -SilenceThresholdValue $SilenceThreshold `
        -AllowQuietValue ([bool] $AllowQuiet) `
        -ApiKeyValue $apiKey `
        -DestinationPath $outputPath

    $escapedPrompt = $Prompt -replace '\|', '\|'
    $row = '| {0} | `{1}` | `{2}` | {3}s | {4}s | {5} | {6} |' -f `
    (Get-Date -Format 'yyyy-MM-dd'),
    (Split-Path -Leaf $outputPath),
    $modelId,
    $Duration,
    $result.KeptSeconds,
    $PromptInfluence,
    $escapedPrompt

    Add-ProvenanceRow -Path $provenancePath -Row $row -HeaderLines @(
        '# Generated sound provenance'
        ''
        'Every file in this folder produced by `scripts/sfx.ps1` is logged here.'
        'The game ignores this file; it exists so a sound can be traced back to'
        'the prompt and model that made it.'
        ''
        '| Date | File | Model | Requested | Kept | Influence | Prompt |'
        '| --- | --- | --- | --- | --- | --- | --- |'
    )

    Write-Host "[PASS] Logged the prompt in $provenancePath."
    Write-Host 'Next: launch with ./scripts/run.ps1 and press F9 to confirm the slot reports READY.'
}
catch {
    $detail = $null
    if ($null -ne $_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
        $detail = $_.ErrorDetails.Message
    }

    if ($null -ne $detail) {
        if ($detail.Length -gt 800) {
            $detail = $detail.Substring(0, 800) + '...'
        }

        Write-Host "[FAIL] ElevenLabs rejected the request: $detail" -ForegroundColor Red
        Write-Host '[INFO] A PCM output format above pcm_24000 requires a Pro subscription; re-run with -SampleRate 24000 if that is the cause.'
    }

    throw
}
