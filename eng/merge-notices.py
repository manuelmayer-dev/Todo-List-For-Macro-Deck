"""Merges the per-platform notices drafted by inventory-dependencies.py into THIRD-PARTY-NOTICES.

Usage: python3 eng/merge-notices.py <notices-win-x64.txt> <notices-osx-arm64.txt> <native-notices.txt> <output>
The native notices file is THIRD-PARTY-NOTICES.txt from the SkiaSharp.NativeAssets package that ships.
"""
import re
import sys

RULE = "=" * 80
LUCIDE = __import__("os").path.join(__import__("os").path.dirname(__file__), "notices", "Lucide-LICENSE.txt")


def parse(path):
    text = open(path, encoding="utf-8").read()
    after_packages = text.split(RULE + "\nNuGet packages\n" + RULE)[1]
    packages_part, texts_part = after_packages.split(RULE + "\nLicense and notice texts\n" + RULE)
    entries = {}
    for block in packages_part.strip().split("\n\n"):
        lines = block.strip().splitlines()
        entries[lines[0]] = lines[1:]
    texts = {}
    for match in re.finditer(r"^\[(\d+)\] (.+?)\n(.*?)(?=^\[\d+\] |\Z)", texts_part, re.S | re.M):
        texts[match.group(1)] = (match.group(2), match.group(3).rstrip() + "\n")
    return entries, texts


def main(win, osx, native, output):
    platforms = {"win-x64": parse(win), "osx-arm64": parse(osx)}
    merged, text_by_title = {}, {}
    for platform, (entries, texts) in platforms.items():
        for package, lines in entries.items():
            # A package that names a license file instead of an SPDX expression gets a pointer to its text.
            fields = [
                "  License: BSD-style, see the text below (the package's LICENSE file)" if l.strip() == "License: LICENSE" else l
                for l in lines
                if not l.strip().startswith("Texts:")
            ]
            ref = next(l for l in lines if l.strip().startswith("Texts:")).split("[")[1].rstrip("]")
            title, body = texts[ref]
            text_by_title.setdefault(title, body)
            entry = merged.setdefault(package, {"fields": fields, "text": title, "platforms": set()})
            entry["platforms"].add(platform)

    titles = sorted({e["text"] for e in merged.values()}, key=str.lower)
    numbers = {t: i + 1 for i, t in enumerate(titles)}
    out = [
        "THIRD-PARTY SOFTWARE NOTICES AND INFORMATION",
        "Todo List for Macro Deck",
        "",
        "Todo List for Macro Deck includes the third-party components listed below. Each component is",
        "provided under its own license, reproduced in the license texts section.",
        "",
        RULE, "NuGet packages", RULE, "",
    ]
    for package in sorted(merged, key=str.lower):
        entry = merged[package]
        out.append(package)
        out.extend(entry["fields"])
        if len(entry["platforms"]) == 1:
            out.append(f"  Shipped on: {next(iter(entry['platforms']))}")
        out.append(f"  Texts: [{numbers[entry['text']]}]")
        out.append("")
    out += [
        RULE, "Bundled native libraries", RULE, "",
        "Skia, HarfBuzz and the other native code inside SkiaSharp.NativeAssets and",
        "HarfBuzzSharp.NativeAssets are covered by the notices those packages ship, reproduced",
        "in full in the last section.",
        "",
        "SQLite (inside SQLitePCLRaw.lib.e_sqlite3)",
        "  License: public domain",
        "  URL: https://www.sqlite.org/copyright.html",
        "",
        RULE, "License and notice texts", RULE, "",
    ]
    for title in titles:
        out.append(f"[{numbers[title]}] {title}")
        out.append(text_by_title[title])
    out += [
        RULE, "Bundled assets", RULE, "",
        "Lucide icons (check, plus, settings, trash, pencil, x, clock, list, calendar, command,",
        "corner-down-left, space, arrow-up, arrow-down), drawn by the desktop UI",
        "  License: ISC",
        "  URL: https://lucide.dev",
        "",
        open(LUCIDE, encoding="utf-8").read().rstrip(),
        "",
    ]
    out += [RULE, "Notices shipped with SkiaSharp and HarfBuzzSharp native assets", RULE, ""]
    out.append(open(native, encoding="utf-8").read().rstrip() + "\n")
    open(output, "w", encoding="utf-8").write("\n".join(out))


if __name__ == "__main__":
    main(*sys.argv[1:5])
