#!/usr/bin/env python3
"""Google drift watch.

Compares two public Google sources with snapshots that are stored in this repository:

1. The Gemini API discovery document (v1beta). It lists every method, every field and every enum
   value. A new field or a new method means Google changed the API.
2. The deprecations page. It lists every model, its shutdown date and its replacement. A changed
   date or a new row means Google changed a deadline.

Neither source needs an API key.

Why this exists. In 2026 Google shipped Gemini 4, moved a shutdown date and rerouted two models, and
this library found out by hand. This check turns "someone notices" into "a GitHub issue opens".

Exit codes:
  0  Nothing changed.
  1  Something changed. The report file says what.
  2  The check could not run. A download failed, or a page has a new format.

Use --update after you review a change. It rewrites the snapshots and exits 0.

The script uses only the Python standard library.
"""

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

DISCOVERY_URL = "https://generativelanguage.googleapis.com/$discovery/rest?version=v1beta"
DEPRECATIONS_URL = "https://aistudio.google.com/docs/deprecations.md"

REPO_ROOT = Path(__file__).resolve().parents[2]
SNAPSHOT_DIR = REPO_ROOT / ".github" / "drift"
DISCOVERY_SNAPSHOT = SNAPSHOT_DIR / "discovery-v1beta.txt"
DEPRECATIONS_SNAPSHOT = SNAPSHOT_DIR / "deprecations.txt"

# A page that parses to fewer lines than this has probably changed its format. The check stops
# and exits with 2. It does not report "everything was removed".
MIN_DISCOVERY_LINES = 1000
MIN_DEPRECATION_ROWS = 40

# GitHub rejects an issue body above 65,536 characters. Stay well below that.
MAX_REPORT_CHARS = 55000

USER_AGENT = "junaid-googlegemini-net-drift-watch"


def fetch(url):
    """Download a URL as text. Try three times, with a short wait between tries."""
    last_error = None
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=60) as response:
                return response.read().decode("utf-8")
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            last_error = error
            time.sleep(2 * (attempt + 1))
    raise RuntimeError(f"Could not download {url}: {last_error}")


# ---------------------------------------------------------------------------------------------
# Discovery document
# ---------------------------------------------------------------------------------------------

def enum_values(node):
    """Return the enum values of a schema node, including values on array items."""
    values = list(node.get("enum", []))
    items = node.get("items")
    if isinstance(items, dict):
        values.extend(items.get("enum", []))
    return values


def discovery_lines(document):
    """Turn the discovery document into a sorted list of simple text lines."""
    lines = set()

    def walk_resources(resources):
        for resource in resources.values():
            for method in resource.get("methods", {}).values():
                lines.add(f"method {method.get('httpMethod', '?')} {method.get('path', '?')}")
            walk_resources(resource.get("resources", {}))

    walk_resources(document.get("resources", {}))

    for schema_name, schema in document.get("schemas", {}).items():
        for value in enum_values(schema):
            lines.add(f"enum {schema_name} {value}")
        for property_name, node in schema.get("properties", {}).items():
            lines.add(f"prop {schema_name}.{property_name}")
            for value in enum_values(node):
                lines.add(f"enum {schema_name}.{property_name} {value}")

    return sorted(lines)


# ---------------------------------------------------------------------------------------------
# Deprecations page
# ---------------------------------------------------------------------------------------------

HEADING = re.compile(r"^(#{2,3})\s+(.*?)\s*(\{%.*%\})?\s*$")


def deprecation_lines(text):
    """Turn the deprecations page into a sorted list of rows, one per model or agent.

    The page uses Markdoc tables. A table starts with {% table %} and ends with {% /table %}.
    Rows are separated by a line that holds only '---'. Each cell is a line that starts with '*'.
    The first row of each table is the header.
    """
    rows = []
    section_h2 = ""
    section_h3 = ""
    in_table = False
    table_rows = []
    current = []

    def finish_table():
        nonlocal current, table_rows
        if current:
            table_rows.append(current)
        section = f"{section_h2} / {section_h3}" if section_h3 else section_h2
        for row in table_rows[1:]:  # skip the header row
            cells = [re.sub(r"\s+", " ", cell).strip() for cell in row]
            # rstrip matters. A row with no replacement ends in an empty cell, and a snapshot file
            # reader strips trailing spaces. Without this, those rows never match their snapshot.
            rows.append(" | ".join([section] + cells).rstrip())
        table_rows = []
        current = []

    for raw in text.splitlines():
        stripped = raw.strip()

        if not in_table:
            match = HEADING.match(raw)
            if match:
                if len(match.group(1)) == 2:
                    section_h2 = match.group(2)
                    section_h3 = ""
                else:
                    section_h3 = match.group(2)
                continue
            if stripped.startswith("{% table"):
                in_table = True
                table_rows = []
                current = []
            continue

        if stripped.startswith("{% /table"):
            finish_table()
            in_table = False
        elif stripped == "---":
            table_rows.append(current)
            current = []
        elif stripped.startswith("*"):
            current.append(stripped[1:].strip())
        elif stripped and current:
            current[-1] += " " + stripped  # a cell that continues on the next line

    return sorted(rows)


# ---------------------------------------------------------------------------------------------
# Snapshots and comparison
# ---------------------------------------------------------------------------------------------

def read_snapshot(path):
    """Read a snapshot file. Comment lines start with '#'. A missing file is an empty snapshot."""
    if not path.exists():
        return None
    lines = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if line and not line.startswith("#"):
            lines.append(line)
    return lines


def write_snapshot(path, header, lines):
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "".join(f"# {line}\n" for line in header) + "".join(f"{line}\n" for line in lines)
    path.write_text(text, encoding="utf-8", newline="\n")


def compare(old, new):
    old_set, new_set = set(old), set(new)
    return sorted(new_set - old_set), sorted(old_set - new_set)


# ---------------------------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------------------------

def group_discovery(lines):
    """Group discovery lines by kind, so the report is easy to read."""
    methods = [line[len("method "):] for line in lines if line.startswith("method ")]
    enums = [line[len("enum "):] for line in lines if line.startswith("enum ")]
    props = {}
    for line in lines:
        if line.startswith("prop "):
            schema, _, name = line[len("prop "):].partition(".")
            props.setdefault(schema, []).append(name)
    return methods, props, enums


def discovery_section(added, removed, old_revision, new_revision):
    out = ["## Gemini API discovery document (v1beta)", ""]
    out.append(f"Revision: `{old_revision}` in the snapshot, `{new_revision}` now.")
    out.append("")
    for title, lines in (("Added", added), ("Removed", removed)):
        if not lines:
            continue
        methods, props, enums = group_discovery(lines)
        out.append(f"### {title}")
        out.append("")
        if methods:
            out.append("Methods:")
            out.extend(f"- `{m}`" for m in methods)
            out.append("")
        if props:
            out.append("Fields:")
            for schema in sorted(props):
                names = ", ".join(f"`{n}`" for n in sorted(props[schema]))
                out.append(f"- `{schema}`: {names}")
            out.append("")
        if enums:
            out.append("Enum values:")
            out.extend(f"- `{e}`" for e in enums)
            out.append("")
    return out


def deprecation_section(added, removed):
    out = ["## Deprecations page", ""]
    out.append("Each row reads: section, model, release date, shutdown date, replacement.")
    out.append("A changed row shows as one removed row and one added row.")
    out.append("")
    if added:
        out.append("### Added or changed rows")
        out.append("")
        out.extend(f"- {row}" for row in added)
        out.append("")
    if removed:
        out.append("### Removed or replaced rows")
        out.append("")
        out.extend(f"- {row}" for row in removed)
        out.append("")
    return out


def write_report(path, sections):
    header = [
        "# Google drift report",
        "",
        "Google changed something that this library depends on. The sections below say what.",
        "Read each change, decide if the library needs a code change, and then update the snapshots:",
        "",
        "```",
        "python3 .github/scripts/google_drift_watch.py --update",
        "```",
        "",
    ]
    text = "\n".join(header + sections)
    if len(text) > MAX_REPORT_CHARS:
        text = text[:MAX_REPORT_CHARS] + "\n\n(The report was cut. Run the script locally for the rest.)\n"
    Path(path).write_text(text, encoding="utf-8", newline="\n")


# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------

def main(argv):
    parser = argparse.ArgumentParser(description="Compare Google's API and deprecation pages with the snapshots.")
    parser.add_argument("--update", action="store_true", help="Rewrite the snapshots and exit 0.")
    parser.add_argument("--report", default="drift-report.md", help="Where to write the Markdown report.")
    parser.add_argument("--discovery-file", help="Read the discovery document from this file. For tests.")
    parser.add_argument("--deprecations-file", help="Read the deprecations page from this file. For tests.")
    args = parser.parse_args(argv)

    try:
        discovery_text = (Path(args.discovery_file).read_text(encoding="utf-8")
                          if args.discovery_file else fetch(DISCOVERY_URL))
        deprecations_text = (Path(args.deprecations_file).read_text(encoding="utf-8")
                             if args.deprecations_file else fetch(DEPRECATIONS_URL))
        document = json.loads(discovery_text)
    except (RuntimeError, OSError, ValueError) as error:
        print(f"Could not read a source: {error}", file=sys.stderr)
        return 2

    new_discovery = discovery_lines(document)
    new_deprecations = deprecation_lines(deprecations_text)
    new_revision = document.get("revision", "unknown")

    if len(new_discovery) < MIN_DISCOVERY_LINES or len(new_deprecations) < MIN_DEPRECATION_ROWS:
        print(
            f"A source has a new format. Found {len(new_discovery)} discovery lines "
            f"(need {MIN_DISCOVERY_LINES}) and {len(new_deprecations)} deprecation rows "
            f"(need {MIN_DEPRECATION_ROWS}). Fix the parser before you trust this check.",
            file=sys.stderr,
        )
        return 2

    if args.update:
        write_snapshot(
            DISCOVERY_SNAPSHOT,
            [f"Source: {DISCOVERY_URL}", f"Revision: {new_revision}",
             "One line per method, field or enum value. Written by google_drift_watch.py --update."],
            new_discovery,
        )
        write_snapshot(
            DEPRECATIONS_SNAPSHOT,
            [f"Source: {DEPRECATIONS_URL}",
             "One line per table row. Written by google_drift_watch.py --update."],
            new_deprecations,
        )
        print(f"Snapshots updated: {len(new_discovery)} discovery lines, {len(new_deprecations)} deprecation rows.")
        return 0

    old_discovery = read_snapshot(DISCOVERY_SNAPSHOT)
    old_deprecations = read_snapshot(DEPRECATIONS_SNAPSHOT)
    if old_discovery is None or old_deprecations is None:
        print("A snapshot file is missing. Run with --update once and commit the result.", file=sys.stderr)
        return 2

    old_revision = "unknown"
    for raw in DISCOVERY_SNAPSHOT.read_text(encoding="utf-8").splitlines():
        if raw.startswith("# Revision:"):
            old_revision = raw.split(":", 1)[1].strip()
            break

    d_added, d_removed = compare(old_discovery, new_discovery)
    p_added, p_removed = compare(old_deprecations, new_deprecations)

    print(f"Discovery revision {old_revision} -> {new_revision}: {len(d_added)} added, {len(d_removed)} removed.")
    print(f"Deprecations: {len(p_added)} rows added or changed, {len(p_removed)} removed or replaced.")

    if not (d_added or d_removed or p_added or p_removed):
        print("No drift.")
        return 0

    sections = []
    if d_added or d_removed:
        sections += discovery_section(d_added, d_removed, old_revision, new_revision)
    if p_added or p_removed:
        sections += deprecation_section(p_added, p_removed)
    write_report(args.report, sections)
    print(f"Drift found. Report written to {args.report}.")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
