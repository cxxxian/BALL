# Install core game design / balance skills into Skill/ (5 skills for BALL)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$TempBase = Join-Path $env:TEMP "ball-skills-install"
$MaxRetries = 3

$AllSkills = @(
    # balance-economy (2)
    @{ Category = "balance-economy"; DestName = "balance-check"; Repo = "donchitos/claude-code-game-studios"; RepoSkill = "balance-check" },
    @{ Category = "balance-economy"; DestName = "game-balance-economy"; Repo = "lvtd-llc/skills"; RepoSkill = "game-balance-economy" },
    # game-design-gdd (3)
    @{ Category = "game-design-gdd"; DestName = "design-game-design-fundamentals"; Repo = "fcsouza/agent-skills"; RepoSkill = "game-design-fundamentals" },
    @{ Category = "game-design-gdd"; DestName = "design-review"; Repo = "donchitos/claude-code-game-studios"; RepoSkill = "design-review" },
    @{ Category = "game-design-gdd"; DestName = "level-design"; Repo = "gamedev-skills/awesome-gamedev-agent-skills"; RepoSkill = "level-design" }
)

function Get-OrCloneRepo([string]$Repo) {
    $DirName = ($Repo -replace "/", "-")
    $Dir = Join-Path $TempBase $DirName
    if (Test-Path $Dir) {
        $HasGit = Test-Path (Join-Path $Dir ".git")
        if ($HasGit) { return $Dir }
        Remove-Item -Recurse -Force $Dir
    }
    New-Item -ItemType Directory -Force -Path $TempBase | Out-Null
    for ($i = 1; $i -le $MaxRetries; $i++) {
        Write-Host "    Cloning $Repo (attempt $i/$MaxRetries) ..."
        git clone --depth 1 "https://github.com/$Repo.git" $Dir 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $Dir ".git"))) { return $Dir }
        Remove-Item -Recurse -Force $Dir -ErrorAction SilentlyContinue
        Start-Sleep -Seconds (3 * $i)
    }
    throw "Failed to clone $Repo after $MaxRetries attempts"
}

function Find-SkillDir([string]$RepoDir, [string]$SkillName) {
    if (Test-Path (Join-Path $RepoDir "SKILL.md")) { return $RepoDir }
    $Candidates = Get-ChildItem -Path $RepoDir -Recurse -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $SkillName -and (Test-Path (Join-Path $_.FullName "SKILL.md")) }
    if ($Candidates.Count -eq 0) { return $null }
    return ($Candidates | Sort-Object {
        $p = $_.FullName
        if ($p -match '[\\/]skills[\\/]' ) { 0 }
        elseif ($p -match '[\\/]\.agents[\\/]' ) { 1 }
        elseif ($p -match '[\\/]\.claude[\\/]' ) { 2 }
        else { 3 }
    } | Select-Object -First 1).FullName
}

$Ok = 0; $Failed = @()

foreach ($Entry in $AllSkills) {
    $Dest = Join-Path (Join-Path $Root $Entry.Category) $Entry.DestName
    if ((Test-Path (Join-Path $Dest "SKILL.md"))) {
        Write-Host "[skip] $($Entry.DestName) already installed"
        $Ok++
        continue
    }
    Write-Host ">>> Installing $($Entry.DestName) ..."
    try {
        $RepoDir = Get-OrCloneRepo $Entry.Repo
        $Src = Find-SkillDir $RepoDir $Entry.RepoSkill
        if (-not $Src) { throw "SKILL.md not found: $($Entry.RepoSkill) in $($Entry.Repo)" }
        New-Item -ItemType Directory -Force -Path (Split-Path $Dest -Parent) | Out-Null
        if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
        Copy-Item -Path $Src -Destination $Dest -Recurse -Force
        Write-Host "    OK"
        $Ok++
    }
    catch {
        Write-Warning "    FAILED: $($_.Exception.Message)"
        $Failed += $Entry.DestName
    }
}

$ProjectRoot = Split-Path $Root -Parent
$CursorSkills = Join-Path $ProjectRoot ".cursor\skills"
New-Item -ItemType Directory -Force -Path $CursorSkills | Out-Null
Get-ChildItem -Path $Root -Recurse -Filter "SKILL.md" | ForEach-Object {
    $SkillDir = $_.Directory
    $Link = Join-Path $CursorSkills $SkillDir.Name
    if (Test-Path $Link) { cmd /c rmdir "$Link" 2>$null }
    cmd /c mklink /J "$Link" "$($SkillDir.FullName)" 2>&1 | Out-Null
}

Write-Host ""
Write-Host "=== Done: $Ok/5 installed ==="
if ($Failed.Count -gt 0) { Write-Host "Failed: $($Failed -join ', ')" }
