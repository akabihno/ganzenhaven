#!/usr/bin/env python3
"""Voice dialogue lines with ElevenLabs.

    python3 tools/elevenlabs_voice.py office/seagull [more/scripts ...] [--lang en ru] [--force] [--dry-run]
    python3 tools/elevenlabs_voice.py --all       every dialog script, plus the items
    python3 tools/elevenlabs_voice.py items       the inventory items, read by the author

For every speaker line of dialogs/<lang>/<script>.txt whose character has a voice in
tools/voices.json, writes audio/voice/<lang>/<script>/<key>.mp3, where <key> is the first 12
hex digits of sha1("<speaker>|<text>") — exactly what the game computes for the line it is
showing (DialogueRunner.VoiceKey). Edit a line and its key changes: the old recording stops
playing, and running this again voices the new text. Existing files are kept unless --force.
Recordings for lines that no longer exist are removed. Player choices (* lines) are not voiced.

"items" voices dialogs/<lang>/items.txt instead: the author reads "Name. Description." for each
item into audio/voice/<lang>/items/<id>.mp3 (redone when the text changes), which the inventory
plays when a thing is clicked.
"""
import argparse, csv, hashlib, json, os, pathlib, re, sys, time, urllib.request, urllib.error

ROOT = pathlib.Path(__file__).resolve().parent.parent
CONFIG = json.loads((ROOT / "tools/voices.json").read_text(encoding="utf-8"))

# Same rules as DialogueScript.cs.
SPEAKER = re.compile(r'^(?P<name>[^\s:@$*\\#\[][^:.!?"«»\[]{0,40}?)(\s*\[(?P<expr>[\w-]+)\])?:\s+(?P<text>.+)$')
BBCODE = re.compile(r'\[/?(?:i|b|u|s|color|font_size|wave|shake)(?:=[^\]]*)?\]')
UNDERSCORED = re.compile(r'(?<![\w])_(?P<t>[^_\n]+?)_(?![\w])')


def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key and (ROOT / ".env").exists():
        for line in (ROOT / ".env").read_text().splitlines():
            if line.strip().startswith("ELEVENLABS_API_KEY="):
                key = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not key:
        sys.exit("ELEVENLABS_API_KEY not set (env or .env)")
    return key


def character_names():
    """Display name (any language) or id -> character id, from translations/ui.csv."""
    names = {}
    with open(ROOT / "translations/ui.csv", encoding="utf-8") as f:
        rows = list(csv.reader(f))
    for row in rows[1:]:
        if row and row[0].startswith("char_"):
            cid = row[0][5:]
            names[cid.lower()] = cid
            for name in row[1:]:
                names[name.strip().lower()] = cid
    return names


def voice_key(speaker, text):
    return hashlib.sha1(f"{speaker}|{text}".encode("utf-8")).hexdigest()[:12]


def lines(path):
    """(speaker, expression, text) for every spoken line, in file order."""
    for raw in path.read_text(encoding="utf-8").lstrip("﻿").splitlines():
        t = raw.strip()
        if not t or t.startswith(("#", "@", "$", "* ")):
            continue
        if t.startswith("\\"):
            yield "", "", t[1:].lstrip()
            continue
        m = SPEAKER.match(t)
        yield (m["name"].strip(), m["expr"] or "", m["text"].strip()) if m else ("", "", t)


def spoken(text):
    text = BBCODE.sub("", text)
    return UNDERSCORED.sub(lambda m: m["t"].upper(), text)  # v3 stresses capitals


def tts(voice_id, text):
    body = {"text": text, "model_id": CONFIG["model"]}
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format={CONFIG['output_format']}"
    for attempt in range(5):
        req = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST",
                                     headers={"xi-api-key": api_key(), "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < 4:
                time.sleep(3 * (attempt + 1))
                continue
            sys.exit(f"tts -> {e.code}: {e.read().decode()[:400]}")


# Files in dialogs/<lang>/ that are not conversations.
NOT_SCRIPTS = {"journal", "items", "ledger"}


def all_scripts():
    root = ROOT / "dialogs" / "en"
    found = sorted(str(f.relative_to(root).with_suffix("")) for f in root.rglob("*.txt"))
    return [s for s in found if s not in NOT_SCRIPTS] + ["items"]


def item_texts(lang):
    """id -> "Name. Description." from dialogs/<lang>/items.txt."""
    items, current = {}, None
    path = ROOT / "dialogs" / lang / "items.txt"
    if not path.exists():
        return items
    for raw in path.read_text(encoding="utf-8").lstrip("\ufeff").splitlines():
        t = raw.strip()
        if not t or t.startswith("#"):
            continue
        if raw[0] in " \t":
            if current:
                items[current] = f"{items[current]} {t}" if items[current].endswith(".") else f"{items[current]}. {t}"
            continue
        if ":" in t:
            current, name = (x.strip() for x in t.split(":", 1))
            items[current] = name
    return items


def voice_items(lang, force, dry_run):
    narrator = CONFIG["voices"]["narrator"]
    out_dir = ROOT / "audio/voice" / lang / "items"
    manifest_path = out_dir / "lines.json"
    old = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    manifest = {}
    for item_id, text in item_texts(lang).items():
        line = f"{narrator.get('direction', '')} {spoken(text)}".strip()
        manifest[item_id] = {"text": text, "sent": line}
        dst = out_dir / f"{item_id}.mp3"
        if dst.exists() and not force and old.get(item_id, {}).get("text") == text:
            print(f"  = {lang} items/{item_id}: {text}")
            continue
        print(f"  + {lang} items/{item_id}: {line}")
        if not dry_run:
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(tts(narrator["voice_id"], line))
    if not dry_run and manifest:
        out_dir.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("scripts", nargs="*")
    p.add_argument("--all", action="store_true")
    p.add_argument("--lang", nargs="+", default=["en", "ru"])
    p.add_argument("--force", action="store_true")
    p.add_argument("--dry-run", action="store_true")
    a = p.parse_args()

    if a.all:
        a.scripts = all_scripts()
    if not a.scripts:
        p.error("name some scripts, or --all")
    names = character_names()
    skip = CONFIG.get("skip_lines_containing", [])
    for lang in a.lang:
        for script in a.scripts:
            if script == "items":
                voice_items(lang, a.force, a.dry_run)
                continue
            src = ROOT / "dialogs" / lang / f"{script}.txt"
            if not src.exists():
                print(f"skip {src} (missing)")
                continue
            out_dir = ROOT / "audio/voice" / lang / script
            keep, manifest = set(), {}
            for speaker, expr, text in lines(src):
                if any(s in text for s in skip):
                    continue
                cid = names.get(speaker.lower(), "narrator" if not speaker else "")
                voice = CONFIG["voices"].get(cid)
                if not voice:
                    continue
                if "{" in text:
                    print(f"  ! not voiced, has a {{variable}}: {text}")
                    continue
                key = voice_key(speaker, text)
                keep.add(key)
                direction = voice.get("expressions", {}).get(expr) or voice.get("direction", "")
                words = spoken(text)
                if not re.search(r"\w", words):
                    # "..." and the like: let the voice make a sound instead of reading dots
                    words = voice.get("wordless", "[sighs] Hmm.")
                line = f"{direction} {words}".strip()
                manifest[key] = {"speaker": speaker, "expression": expr, "text": text, "sent": line}
                dst = out_dir / f"{key}.mp3"
                if dst.exists() and not a.force:
                    print(f"  = {lang} {key} {speaker}: {text}")
                    continue
                print(f"  + {lang} {key} {speaker}: {line}")
                if not a.dry_run:
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    dst.write_bytes(tts(voice["voice_id"], line))
            if out_dir.exists() and not a.dry_run:
                for old in out_dir.glob("*.mp3"):
                    if old.stem not in keep:
                        print(f"  - {lang} {old.stem} (line gone)")
                        old.unlink()
                        imp = old.with_suffix(".mp3.import")
                        if imp.exists():
                            imp.unlink()
                (out_dir / "lines.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
