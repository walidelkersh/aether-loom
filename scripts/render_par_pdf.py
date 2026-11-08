#!/usr/bin/env python3
"""Print docs/PAR-sheet.html to docs/PAR-sheet.pdf with a headless Chromium (Edge, Chrome, or Playwright's)."""
from __future__ import annotations

import glob
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CANDIDATES = [
    os.environ.get("CHROME_PATH", ""),
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    shutil.which("chromium") or "", shutil.which("chromium-browser") or "",
    shutil.which("google-chrome") or "", shutil.which("microsoft-edge") or "",
    # Chromium installed by `npx playwright install chromium` (the repo's own dev dependency).
    *sorted(glob.glob(os.path.expandvars(r"%LOCALAPPDATA%\ms-playwright\chromium-*\chrome-win*\chrome.exe")), reverse=True),
    *sorted(glob.glob(os.path.expanduser("~/.cache/ms-playwright/chromium-*/chrome-linux*/chrome")), reverse=True),
]


def main() -> int:
    browser = next((c for c in CANDIDATES if c and Path(c).exists()), None)
    if browser is None:
        print("No Chromium-based browser found; set CHROME_PATH.", file=sys.stderr)
        return 1
    source = (ROOT / "docs/PAR-sheet.html").resolve()
    target = (ROOT / "docs/PAR-sheet.pdf").resolve()
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
                    f"--print-to-pdf={target}", source.as_uri()], check=True, timeout=120,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print(f"Wrote {target.relative_to(ROOT)} ({target.stat().st_size // 1024} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
