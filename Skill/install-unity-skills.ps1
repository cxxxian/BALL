# Install curated Unity + Tilemap skills into Skill/unity-dev/ (5 skills for BALL)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
python (Join-Path $Root "install-unity-skills.py")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
