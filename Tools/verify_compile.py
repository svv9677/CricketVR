#!/usr/bin/env python3
"""Player-view Roslyn compile: every .cs under Assets, WITHOUT UnityEditor.dll.

Reproduces what a Quest player build sees, so editor-only API leaking into runtime
code fails here instead of at build time. Editor scripts (Assets/Editor/**) are
excluded, since they are allowed to use UnityEditor.

Usage: python3 Tools/verify_compile.py [--unity <editor root>]
Exit code 0 = clean.
"""
import argparse
import os
import subprocess
import sys
import tempfile

DOTNET = "/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Resources/Scripting/NetCoreRuntime/dotnet"
DEFAULT_UNITY = "/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents"
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def collect_sources(root):
    """Every runtime .cs: skips Editor folders and anything under Library/Temp."""
    out = []
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("Editor", "Tests")]
        for f in files:
            if f.endswith(".cs"):
                out.append(os.path.join(base, f))
    return sorted(out)


def collect_refs(unity):
    scripting = os.path.join(unity, "Resources", "Scripting")
    refs = []
    # netstandard 2.1 ref profile ONLY - mixing mscorlib with these produces
    # hundreds of spurious CS0012 errors.
    ns = os.path.join(scripting, "NetStandard", "ref", "2.1.0")
    for f in os.listdir(ns):
        if f.endswith(".dll"):
            refs.append(os.path.join(ns, f))
    managed = os.path.join(scripting, "Managed")
    for base, _dirs, files in os.walk(managed):
        for f in files:
            # UnityEditor.dll deliberately excluded: this is the player view.
            if f.endswith(".dll") and not f.startswith("UnityEditor"):
                refs.append(os.path.join(base, f))
    # Package assemblies (Input System, XR, TextMeshPro, ...) as precompiled DLLs.
    for pkgdir in (os.path.join(PROJECT, "Library", "ScriptAssemblies"),):
        if os.path.isdir(pkgdir):
            for f in os.listdir(pkgdir):
                # Skip assemblies whose sources we compile ourselves, or csc sees
                # every Oculus type twice and reports CS0121 ambiguity.
                if f.endswith(".dll") and not f.startswith("Assembly-CSharp") \
                        and not f.startswith("Oculus") and "Editor" not in f:
                    refs.append(os.path.join(pkgdir, f))
    return refs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", default=DEFAULT_UNITY)
    args = ap.parse_args()

    csc = os.path.join(args.unity, "Resources", "Scripting", "DotNetSdkRoslyn", "csc.dll")
    if not os.path.exists(csc):
        sys.exit(f"csc.dll not found at {csc} - pass --unity <editor Contents dir>")

    sources = collect_sources(os.path.join(PROJECT, "Assets"))
    refs = collect_refs(args.unity)
    print(f"{len(sources)} sources, {len(refs)} references")

    # Response file with every path quoted: several contain spaces.
    with tempfile.NamedTemporaryFile("w", suffix=".rsp", delete=False) as rsp:
        rsp.write("-target:library\n-nostdlib+\n-noconfig\n")
        rsp.write("-out:" + os.path.join(tempfile.gettempdir(), "playerview.dll") + "\n")
        rsp.write("-define:UNITY_2018_1_OR_NEWER;UNITY_2018_2_OR_NEWER;UNITY_2018_3_OR_NEWER;UNITY_2018_4_OR_NEWER;UNITY_2019_1_OR_NEWER;UNITY_2019_2_OR_NEWER;UNITY_2019_3_OR_NEWER;UNITY_2019_4_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2020_2_OR_NEWER;UNITY_2020_3_OR_NEWER;UNITY_2020_4_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2021_2_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2021_4_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2022_2_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2022_4_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2023_2_OR_NEWER;UNITY_2023_3_OR_NEWER;UNITY_2023_4_OR_NEWER;UNITY_2018_OR_NEWER;UNITY_2019_OR_NEWER;UNITY_2020_OR_NEWER;UNITY_2021_OR_NEWER;UNITY_2022_OR_NEWER;UNITY_2023_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_ANDROID;ENABLE_VR;UNITY_XR_MANAGEMENT\n")
        for r in refs:
            rsp.write(f'-r:"{r}"\n')
        for s in sources:
            rsp.write(f'"{s}"\n')
        path = rsp.name

    proc = subprocess.run([DOTNET, csc, f"@{path}"], capture_output=True, text=True)
    os.unlink(path)
    errors = [l for l in (proc.stdout + proc.stderr).splitlines() if ": error " in l]
    for line in errors[:40]:
        print(line)
    print(f"\n{len(errors)} errors")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
