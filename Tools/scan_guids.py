#!/usr/bin/env python3
"""Scan every scene/prefab/asset for m_Script GUIDs that no longer resolve.

A clean compile does NOT prove serialized references still resolve - a renamed or
deleted script leaves prefabs pointing at nothing, and Unity reports it only as a
silent "missing script" in the inspector.

Baseline: this project has 4 known pre-existing dangling GUIDs (listed by BASELINE
below). A run is clean when the count is still 4 and the set is unchanged.

Usage: python3 Tools/scan_guids.py
"""
import os
import re
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GUID_IN_META = re.compile(r"^guid:\s*([0-9a-f]{32})", re.M)
SCRIPT_REF = re.compile(r"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})")
ASSET_EXT = (".unity", ".prefab", ".asset", ".controller", ".playable")

# The 4 pre-existing dangling GUIDs, recorded 2026-09-27 on a clean tree. These predate the
# opening-menu work; two are in a bowling animator, two in unused Oculus sample scenes.
BASELINE = {
    "3e94b2dc026df45a09a7f8b9b086cdd0",   # Resources/Animations/Bowling/Steyn.controller
    "afbfc8b197c564f0db527bfe01050944",   # Resources/Animations/Bowling/Steyn.controller
    "f5f67c52d1564df4a8936ccd202a3bd8",   # Oculus/VR/Scenes/HandTest_Custom.unity (+4)
    "f70555f144d8491a825f0804e09c671c",   # Oculus/VR/Scenes/UI.unity (+8)
}


def known_guids():
    """Every script/DLL GUID that exists, from Assets and the package cache."""
    found = set()
    roots = [os.path.join(PROJECT, "Assets"),
             os.path.join(PROJECT, "Library", "PackageCache")]
    for root in roots:
        for base, _dirs, files in os.walk(root):
            for f in files:
                if f.endswith(".cs.meta") or f.endswith(".dll.meta"):
                    p = os.path.join(base, f)
                    try:
                        text = open(p, encoding="utf-8", errors="ignore").read()
                    except OSError:
                        continue
                    m = GUID_IN_META.search(text)
                    if m:
                        found.add(m.group(1))
    return found


def main():
    have = known_guids()
    print(f"{len(have)} script/DLL GUIDs known")
    dangling = {}
    assets = os.path.join(PROJECT, "Assets")
    # Single pass: a per-file grep loop times out on this project.
    for base, _dirs, files in os.walk(assets):
        for f in files:
            if not f.endswith(ASSET_EXT):
                continue
            p = os.path.join(base, f)
            try:
                text = open(p, encoding="utf-8", errors="ignore").read()
            except OSError:
                continue
            for guid in set(SCRIPT_REF.findall(text)):
                if guid not in have:
                    dangling.setdefault(guid, []).append(
                        os.path.relpath(p, PROJECT))

    for guid, where in sorted(dangling.items()):
        print(f"  {guid}  <- {where[0]}" +
              (f" (+{len(where) - 1} more)" if len(where) > 1 else ""))

    count = len(dangling)
    print(f"\n{count} dangling GUIDs (baseline is {len(BASELINE) or 4})")
    if BASELINE:
        added = set(dangling) - BASELINE
        if added:
            print("NEW dangling GUIDs introduced by this change:")
            for g in sorted(added):
                print(f"  {g}  <- {dangling[g][0]}")
            return 1
        return 0
    return 1 if count > 4 else 0


if __name__ == "__main__":
    sys.exit(main())
