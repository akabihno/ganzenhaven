#!/usr/bin/env python3
"""Generate an instrumental track with ElevenLabs Music.

    python3 tools/elevenlabs_music.py <out.mp3> --prompt "..." [--seconds 90]

Reads ELEVENLABS_API_KEY from the environment or the project's .env and writes
<out>.json with the prompt beside the audio. The game loops tracks; tools/make_loop.py
turns a generated track into a seamless loop.
"""
import argparse, json, os, pathlib, sys, urllib.request, urllib.error

ROOT = pathlib.Path(__file__).resolve().parent.parent


def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key and (ROOT / ".env").exists():
        for line in (ROOT / ".env").read_text().splitlines():
            if line.strip().startswith("ELEVENLABS_API_KEY="):
                key = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not key:
        sys.exit("ELEVENLABS_API_KEY not set (env or .env)")
    return key


def main():
    p = argparse.ArgumentParser()
    p.add_argument("out")
    p.add_argument("--prompt", required=True)
    p.add_argument("--seconds", type=float, default=90)
    p.add_argument("--model", default="music_v1")
    a = p.parse_args()

    body = {"prompt": a.prompt, "music_length_ms": int(a.seconds * 1000),
            "force_instrumental": True, "model_id": a.model}
    req = urllib.request.Request("https://api.elevenlabs.io/v1/music?output_format=mp3_44100_192",
                                 data=json.dumps(body).encode(), method="POST",
                                 headers={"xi-api-key": api_key(), "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=600) as r:
            audio, song_id = r.read(), r.headers.get("song-id")
    except urllib.error.HTTPError as e:
        sys.exit(f"music -> {e.code}: {e.read().decode()[:400]}")

    out = pathlib.Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(audio)
    out.with_suffix(".json").write_text(json.dumps({"prompt": a.prompt, "seconds": a.seconds,
                                                    "model": a.model, "song_id": song_id}, indent=2))
    print(f"saved {out} ({len(audio) // 1024} KB)")


if __name__ == "__main__":
    main()
