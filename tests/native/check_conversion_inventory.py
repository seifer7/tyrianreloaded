"""Check native definitions, conversion counts and added-file inventory coverage."""

import re
from pathlib import Path


LITERALS = re.compile(
    r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', re.S
)
DEFINITIONS = re.compile(
    r"(?m)^[ \t]*(?:[A-Za-z_]\w*(?:\s+|\s*\*+\s*))+"
    r"([A-Za-z_]\w*)\s*\([^;{}]*\)\s*\{"
)
NON_FUNCTIONS = {"ITEM_INT", "ITEM_REF", "if", "for", "while", "switch"}


def definitions(path):
    source = path.read_text(encoding="utf-8")
    source = LITERALS.sub(lambda match: "\n" * match.group().count("\n"), source)
    return {match.group(1) for match in DEFINITIONS.finditer(source)} - NON_FUNCTIONS


def main():
    root = Path(__file__).resolve().parents[2]
    progress = (root / "CONVERSION_PROGRESS.md").read_text(encoding="utf-8")
    foundation, rest = progress.split("## Baseline file inventory", 1)
    baseline, inventory = rest.split("## Function inventory", 1)
    files = {path: status for status, path in re.findall(r"(?m)^- \[([ x])\] `([^`]+)`", baseline)}
    sections = {
        match.group(1): {
            name: status
            for status, name in re.findall(r"(?m)^- \[([ x])\] `([^`]+)`", match.group(2))
        }
        for match in re.finditer(
            r"(?m)^#{3,4} `([^`]+)`\s*\n(.*?)(?=^#{3,4} |\Z)", inventory, re.S
        )
    }
    errors = []
    for path in files:
        file = root / path
        if not file.is_file():
            errors.append(f"Missing baseline file: {path}")
            continue
        if file.suffix not in {".c", ".h"}:
            continue
        actual = definitions(file)
        listed = set(sections.get(path, {}))
        for name in sorted(actual - listed):
            errors.append(f"Unlisted native function: {path}: {name}")
        for name in sorted(listed - actual):
            errors.append(f"Inventory function has no recognized definition: {path}: {name}")
    total = sum(len(functions) for functions in sections.values())
    completed = sum(status == "x" for functions in sections.values() for status in functions.values())
    summary = re.search(
        r"Baseline conversion status: (\d+)/(\d+) files dispositioned; (\d+)/(\d+) functions",
        progress,
    )
    counts = (sum(status == "x" for status in files.values()), len(files), completed, total)
    if summary is None or tuple(map(int, summary.groups())) != counts:
        errors.append(f"Progress summary disagrees with inventory counts: {counts}")
    added = [root / "Directory.Build.props", root / "OpenTyrian.slnx", root / ".github/workflows/dotnet.yml",
             root / "tools/generate_hq_kernels.py"]
    for directory, suffixes in [
        ("src-csharp", {".cs", ".csproj"}),
        ("tests/OpenTyrian.Core.Tests", {".cs", ".csproj"}),
        ("tests/native", {".c", ".py"}),
    ]:
        added.extend(
            file for file in (root / directory).rglob("*")
            if file.is_file() and file.suffix in suffixes and not {"bin", "obj"} & set(file.parts)
        )
    for file in added:
        if f"`{file.relative_to(root).as_posix()}`" not in foundation:
            errors.append(f"Unlisted implementation/build/test file: {file.relative_to(root).as_posix()}")
    added_summary = re.search(r"\((\d+) added implementation/build/test files\)", progress)
    if added_summary is None or int(added_summary.group(1)) != len(added):
        errors.append(f"Added-file summary disagrees with filesystem count: {len(added)}")
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"Inventory verified: {counts[0]}/{counts[1]} files, {completed}/{total} functions, {len(added)} added files.")


if __name__ == "__main__":
    main()
