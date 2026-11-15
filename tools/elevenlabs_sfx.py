#!/usr/bin/env python3
"""Generate a sound effect with ElevenLabs.

    python3 tools/elevenlabs_sfx.py <out.mp3> --prompt "..." [--seconds 3] [--loop] [--influence 0.5]

--loop asks for a sound that loops seamlessly (ambience). Writes <out>.json with the prompt.
Reads ELEVENLABS_API_KEY from the environment or the project's .env.
"""
import argparse, json, os, pathlib, sys, time, urllib.request, urllib.error

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
    p.add_argument("--seconds", type=float)
    p.add_argument("--loop", action="store_true")
    p.add_argument("--influence", type=float, default=0.5)
    p.add_argument("--model", default="eleven_text_to_sound_v3")
    a = p.parse_args()

    body = {"text": a.prompt, "loop": a.loop, "prompt_influence": a.influence, "model_id": a.model}
    if a.seconds:
        body["duration_seconds"] = a.seconds
    for attempt in range(5):
        req = urllib.request.Request("https://api.elevenlabs.io/v1/sound-generation", method="POST",
                                     data=json.dumps(body).encode(),
                                     headers={"xi-api-key": api_key(), "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                audio = r.read()
            break
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < 4:
                time.sleep(4 * (attempt + 1))
                continue
            sys.exit(f"sfx -> {e.code}: {e.read().decode()[:400]}")
    out = pathlib.Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(audio)
    out.with_suffix(".json").write_text(json.dumps({k: v for k, v in body.items()}, indent=2))
    print(f"saved {out} ({len(audio) // 1024} KB)")


if __name__ == "__main__":
    main()
