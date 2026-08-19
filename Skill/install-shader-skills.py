#!/usr/bin/env python3
"""Download core shader skills into Skill/shader-unity/ (3 skills for BALL project)."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CURSOR_SKILLS = ROOT.parent / ".cursor" / "skills"
MAX_RETRIES = 3

# Category, dest folder, GitHub repo, path inside repo
# 只保留 BALL 项目（Unity 2D URP + 手写 HLSL）最相关的 3 个
SKILLS: list[tuple[str, str, str, str]] = [
    ("shader-unity", "urp-hlsl-templates", "adevra/unity-shader-agent-skills", "skills/urp-hlsl-templates"),
    ("shader-unity", "shader-programming", "gamedev-skills/awesome-gamedev-agent-skills", "skills/disciplines/shader-programming"),
    ("shader-unity", "shader-techniques", "pluginagentmarketplace/custom-plugin-game-developer", "skills/shader-techniques"),
]


def fetch(url: str) -> bytes:
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "ball-skill-installer"})
            with urllib.request.urlopen(req, timeout=60) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError) as exc:
            if attempt == MAX_RETRIES:
                raise
            time.sleep(2 * attempt)
            last_exc = exc
    raise last_exc  # type: ignore[name-defined]


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


def main() -> None:
    ok: list[str] = []
    failed: list[str] = []

    for category, name, repo, repo_path in SKILLS:
        dest = ROOT / category / name
        if (dest / "SKILL.md").exists():
            print(f"[skip] {name}")
            ok.append(name)
            link_cursor(dest)
            continue

        print(f">>> Installing {name} ({repo}) ...")
        try:
            if dest.exists():
                shutil.rmtree(dest)
            dest.mkdir(parents=True, exist_ok=True)
            download_tree(repo, repo_path, dest)
            if not (dest / "SKILL.md").exists():
                raise FileNotFoundError("SKILL.md not found after download")
            link_cursor(dest)
            print("    OK")
            ok.append(name)
        except Exception as exc:  # noqa: BLE001
            print(f"    FAILED: {exc}")
            failed.append(name)

    print(f"\n=== Done: {len(ok)}/{len(SKILLS)} installed ===")
    if failed:
        print(f"Failed: {', '.join(failed)}")


if __name__ == "__main__":
    main()
