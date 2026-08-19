# Install shader skills into Skill/shader-unity/ and Skill/shader-general/
# Uses GitHub API + raw download (works when git clone to GitHub fails)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
python (Join-Path $Root "install-shader-skills.py")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
