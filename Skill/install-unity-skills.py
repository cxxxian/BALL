#!/usr/bin/env python3
"""Download curated Unity + level skills into Skill/unity-dev/ (5 skills for BALL)."""
from __future__ import annotations

import json
import shutil
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CURSOR_SKILLS = ROOT.parent / ".cursor" / "skills"
MAX_RETRIES = 3

# 精简 5 个：覆盖日常 Unity 2D 开发 + Tilemap 关卡落地
# 已跳过：unity-scripting（与 csharp 重叠）、performance/postprocessing/sprite-editor（低频）、
# unity-level-design（已有 level-design）、terrain/probuilder（3D）
SKILLS: list[tuple[str, str, str, str]] = [
    ("unity-dev", "unity-2d", "nice-wolf-studio/unity-claude-skills", "skills/unity-2d"),
    ("unity-dev", "unity-physics", "nice-wolf-studio/unity-claude-skills", "skills/unity-physics"),
    ("unity-dev", "unity-csharp-scripting", "gamedev-skills/awesome-gamedev-agent-skills", "skills/unity/unity-csharp-scripting"),
    ("unity-dev", "unity-cli", "unity-technologies/skills", "skills/unity-cli"),
    ("unity-dev", "unity-tilemap-2d", "gamedev-skills/awesome-gamedev-agent-skills", "skills/unity/unity-tilemap-2d"),
]


def fetch(url: str) -> bytes:
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "ball-skill-installer"})
            with urllib.request.urlopen(req, timeout=60) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError):
            if attempt == MAX_RETRIES:
                raise
            time.sleep(2 * attempt)
    raise RuntimeError("unreachable")


def list_tree(repo: str, path: str, ref: str = "main") -> list[dict]:
    url = f"https://api.github.com/repos/{repo}/contents/{path}?ref={ref}"
    data = json.loads(fetch(url))
    if isinstance(data, dict) and data.get("type") == "file":
        return [data]
    return data


def download_tree(repo: str, path: str, dest: Path, ref: str = "main") -> None:
    for item in list_tree(repo, path, ref):
        local = dest / item["name"]
        if item["type"] == "file":
            local.parent.mkdir(parents=True, exist_ok=True)
            local.write_bytes(fetch(item["download_url"]))
        else:
            download_tree(repo, item["path"], local, ref)


def link_cursor(skill_dir: Path) -> None:
    CURSOR_SKILLS.mkdir(parents=True, exist_ok=True)
    link = CURSOR_SKILLS / skill_dir.name
    if link.exists():
        subprocess.run(["cmd", "/c", "rmdir", str(link)], capture_output=True, check=False)
    subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(skill_dir)], capture_output=True, check=False)


def link_all_skills() -> None:
    for skill_md in ROOT.rglob("SKILL.md"):
        link_cursor(skill_md.parent)


def main() -> None:
    ok: list[str] = []
    failed: list[str] = []

    for category, name, repo, repo_path in SKILLS:
        dest = ROOT / category / name
        if (dest / "SKILL.md").exists():
            print(f"[skip] {name}")
            ok.append(name)
            continue

        print(f">>> Installing {name} ({repo}/{repo_path}) ...")
        try:
            if dest.exists():
                shutil.rmtree(dest)
            download_tree(repo, repo_path, dest)
            if not (dest / "SKILL.md").exists():
                raise FileNotFoundError("SKILL.md missing after download")
            print("    OK")
            ok.append(name)
        except Exception as exc:  # noqa: BLE001
            print(f"    FAILED: {exc}")
            failed.append(name)

    link_all_skills()
    print()
    print(f"=== Done: {len(ok)}/{len(SKILLS)} Unity skills ===")
    if failed:
        print(f"Failed: {', '.join(failed)}")


if __name__ == "__main__":
    main()
