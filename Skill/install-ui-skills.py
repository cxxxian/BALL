#!/usr/bin/env python3
"""Download UI design + Unity UI skills into Skill/ui-design/ and Skill/ui-unity/."""
from __future__ import annotations

import json
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CURSOR_SKILLS = ROOT.parent / ".cursor" / "skills"
MAX_RETRIES = 3

# category, dest name, GitHub repo, path inside repo
SKILLS: list[tuple[str, str, str, str]] = [
    ("ui-design", "game-ui-design", "omer-metin/skills-for-antigravity", "skills/game-ui-design"),
    ("ui-design", "game-ui-ux", "gamedev-skills/awesome-gamedev-agent-skills", "skills/disciplines/game-ui-ux"),
    ("ui-unity", "ui-ugui", "unity-technologies/skills", "skills/ui-ugui"),
    ("ui-unity", "ui-uitk", "unity-technologies/skills", "skills/ui-uitk"),
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
                import shutil
                shutil.rmtree(dest)
            download_tree(repo, repo_path, dest)
            if not (dest / "SKILL.md").exists():
                raise FileNotFoundError(f"SKILL.md missing after download")
            print("    OK")
            ok.append(name)
        except Exception as exc:  # noqa: BLE001
            print(f"    FAILED: {exc}")
            failed.append(name)

    link_all_skills()
    print()
    print(f"=== Done: {len(ok)}/{len(SKILLS)} UI skills ===")
    if failed:
        print(f"Failed: {', '.join(failed)}")


if __name__ == "__main__":
    main()
