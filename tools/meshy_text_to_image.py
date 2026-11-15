#!/usr/bin/env python3
"""Generate an image with Meshy's Text to Image API.

Usage:
    python3 tools/meshy_text_to_image.py <out.png> --prompt "..." [--model nano-banana-2]
        [--aspect 16:9] [--transparent]

Reads MESHY_API_KEY from the environment or the project's .env. Writes <out.png> and
<out>.json (task id, prompt, credits). Re-running resumes the saved task.
"""
import argparse, json, os, pathlib, sys, time, urllib.request, urllib.error

API = "https://api.meshy.ai/openapi/v1/text-to-image"
ROOT = pathlib.Path(__file__).resolve().parent.parent


def api_key():
    key = os.environ.get("MESHY_API_KEY")
    if not key and (ROOT / ".env").exists():
        for line in (ROOT / ".env").read_text().splitlines():
            if line.strip().startswith("MESHY_API_KEY="):
                key = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not key:
        sys.exit("MESHY_API_KEY not set (env or .env)")
    return key


def call(method, url, body=None):
    req = urllib.request.Request(url, method=method,
                                 headers={"Authorization": f"Bearer {api_key()}",
                                          "Content-Type": "application/json"},
                                 data=json.dumps(body).encode() if body else None)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"{method} {url} -> {e.code}: {e.read().decode()[:500]}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("out")
    p.add_argument("--prompt", required=True)
    p.add_argument("--model", default="nano-banana-2")
    p.add_argument("--aspect", default="16:9")
    p.add_argument("--transparent", action="store_true")
    a = p.parse_args()

    out = pathlib.Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    meta_file = out.with_suffix(".json")
    meta = json.loads(meta_file.read_text()) if meta_file.exists() else {}

    if "task_id" not in meta:
        body = {"ai_model": a.model, "prompt": a.prompt, "aspect_ratio": a.aspect}
        if a.transparent:
            body["remove_background"] = True
        meta = {"task_id": call("POST", API, body)["result"], "prompt": a.prompt,
                "model": a.model, "aspect": a.aspect}
        meta_file.write_text(json.dumps(meta, indent=2))

    while True:
        t = call("GET", f"{API}/{meta['task_id']}")
        if t["status"] == "SUCCEEDED":
            break
        if t["status"] in ("FAILED", "CANCELED"):
            sys.exit(f"{t['status']}: {t.get('task_error')}")
        time.sleep(5)

    with urllib.request.urlopen(t["image_urls"][0], timeout=300) as r:
        out.write_bytes(r.read())
    meta["consumed_credits"] = t.get("consumed_credits")
    meta_file.write_text(json.dumps(meta, indent=2))
    print(f"saved {out} ({out.stat().st_size // 1024} KB, {meta['consumed_credits']} credits)")


if __name__ == "__main__":
    main()
