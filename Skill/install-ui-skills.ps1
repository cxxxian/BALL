# Install UI design + Unity UI skills into Skill/ (4 skills for BALL)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
python (Join-Path $Root "install-ui-skills.py")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
