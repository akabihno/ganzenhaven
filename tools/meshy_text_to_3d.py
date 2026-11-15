#!/usr/bin/env python3
"""Generate a textured 3D model with Meshy's API.

Text to 3D (preview -> refine):
    python3 tools/meshy_text_to_3d.py <out_dir> --prompt "..." [--texture-prompt "..."]
        [--polycount 30000] [--pose ""|a-pose|t-pose] [--no-pbr]
Image to 3D (single task, from a reference render or concept image):
    python3 tools/meshy_text_to_3d.py <out_dir> --image ref.png [--texture-prompt "..."] ...

Reads MESHY_API_KEY from the environment or from the project's .env file.
Writes model.glb, model.fbx, the texture maps, thumbnail.png and task.json into <out_dir>.
Re-running resumes from task.json instead of spending credits again.
"""
import argparse, base64, json, os, pathlib, sys, time, urllib.request, urllib.error

API = "https://api.meshy.ai/openapi/v2/text-to-3d"
IMG_API = "https://api.meshy.ai/openapi/v1/image-to-3d"
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


def wait(task_id, label, api=API):
    last = -1
    while True:
        t = call("GET", f"{api}/{task_id}")
        if t["progress"] != last:
            print(f"[{label}] {t['status']} {t['progress']}%", flush=True)
            last = t["progress"]
        if t["status"] == "SUCCEEDED":
            return t
        if t["status"] in ("FAILED", "CANCELED"):
            sys.exit(f"[{label}] {t['status']}: {t.get('task_error')}")
        time.sleep(10)


def download(url, path):
    with urllib.request.urlopen(url, timeout=300) as r, open(path, "wb") as f:
        f.write(r.read())
    print(f"saved {path} ({path.stat().st_size // 1024} KB)", flush=True)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("out_dir")
    p.add_argument("--prompt")
    p.add_argument("--image", help="reference image -> Image to 3D")
    p.add_argument("--texture-prompt")
    p.add_argument("--polycount", type=int, default=30000)
    p.add_argument("--pose", default="")
    p.add_argument("--no-pbr", action="store_true")
    a = p.parse_args()

    out = pathlib.Path(a.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    state_file = out / "task.json"
    state = json.loads(state_file.read_text()) if state_file.exists() else {}
    save = lambda: state_file.write_text(json.dumps(state, indent=2))
    formats = ["glb", "fbx"]

    if a.image:
        if "image_task_id" not in state:
            uri = "data:image/png;base64," + base64.b64encode(pathlib.Path(a.image).read_bytes()).decode()
            body = {"image_url": uri, "ai_model": "latest", "should_remesh": True,
                    "topology": "triangle", "target_polycount": a.polycount,
                    "pose_mode": a.pose, "should_texture": True, "enable_pbr": not a.no_pbr,
                    "target_formats": formats, "alpha_thumbnail": True,
                    "multi_view_thumbnails": True}
            if a.texture_prompt:
                body["texture_prompt"] = a.texture_prompt
            state.update(image_task_id=call("POST", IMG_API, body)["result"],
                         image=str(a.image), texture_prompt=a.texture_prompt)
            save()
        t = wait(state["image_task_id"], "image-to-3d", IMG_API)
    else:
        if not a.prompt:
            sys.exit("--prompt or --image is required")
        t = text_to_3d(a, state, save, formats)
    finish(t, out, state, save, formats)


def text_to_3d(a, state, save, formats):
    if "preview_id" not in state:
        state["preview_id"] = call("POST", API, {
            "mode": "preview", "prompt": a.prompt, "ai_model": "latest",
            "should_remesh": True, "topology": "triangle",
            "target_polycount": a.polycount, "pose_mode": a.pose,
            "target_formats": formats})["result"]
        state["prompt"] = a.prompt
        save()
    wait(state["preview_id"], "preview")

    if "refine_id" not in state:
        body = {"mode": "refine", "preview_task_id": state["preview_id"],
                "enable_pbr": not a.no_pbr, "texture_resolution": "2k",
                "target_formats": formats, "alpha_thumbnail": True}
        if a.texture_prompt:
            body["texture_prompt"] = a.texture_prompt
            state["texture_prompt"] = a.texture_prompt
        state["refine_id"] = call("POST", API, body)["result"]
        save()
    return wait(state["refine_id"], "refine")


def finish(t, out, state, save, formats):
    state["consumed_credits"] = t.get("consumed_credits")
    save()

    for fmt, url in t["model_urls"].items():
        if fmt in formats:
            download(url, out / f"model.{fmt}")
    for kind, url in (t.get("texture_urls") or [{}])[0].items():
        download(url, out / f"texture_{kind}.png")
    thumb = t.get("alpha_thumbnail_url") or t.get("thumbnail_url")
    if thumb:
        download(thumb, out / "thumbnail.png")
    for view, url in (t.get("thumbnail_urls") or {}).items():
        download(url, out / f"thumbnail_{view}.png")
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
