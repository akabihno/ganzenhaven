#!/usr/bin/env python3
"""Rig a Meshy model as a humanoid and fetch animations for it.

    python3 tools/meshy_rig.py assets/models/fox/source [--height 1.9] [--actions 694 617]

<model_dir> is a folder made by meshy_text_to_3d.py (its task.json names the model task).
Writes rigged.glb (the character with a skeleton), walking.glb / running.glb (Meshy's basic
animations) and anim_<action id>.glb for each library action, plus rig.json with the task ids.
Re-running resumes the saved tasks instead of spending credits again. Action ids come from
GET /openapi/v1/animations/library (e.g. 694 "Walk Left", 617 "Cautious Crouch Walk Left";
the _inplace variants stay on the spot).
"""
import argparse, json, os, pathlib, sys, time, urllib.request, urllib.error

ROOT = pathlib.Path(__file__).resolve().parent.parent
API = "https://api.meshy.ai/openapi/v1"


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
    req = urllib.request.Request(url, method=method, data=json.dumps(body).encode() if body else None,
                                 headers={"Authorization": f"Bearer {api_key()}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"{method} {url} -> {e.code}: {e.read().decode()[:400]}")


def wait(kind, task_id):
    while True:
        task = call("GET", f"{API}/{kind}/{task_id}")
        print(f"[{kind}] {task['status']} {task.get('progress', 0)}%", flush=True)
        if task["status"] == "SUCCEEDED":
            return task
        if task["status"] in ("FAILED", "CANCELED"):
            sys.exit(f"{kind} task {task_id} {task['status']}: {task.get('task_error')}")
        time.sleep(5)


def download(url, dst):
    with urllib.request.urlopen(url, timeout=300) as r:
        dst.write_bytes(r.read())
    print(f"saved {dst} ({dst.stat().st_size // 1024} KB)")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("model_dir")
    p.add_argument("--height", type=float, default=1.8)
    p.add_argument("--actions", type=int, nargs="*", default=[])
    a = p.parse_args()

    folder = ROOT / a.model_dir
    state_path = folder / "rig.json"
    state = json.loads(state_path.read_text()) if state_path.exists() else {}
    save = lambda: state_path.write_text(json.dumps(state, indent=2))

    if "rig_task" not in state:
        model_task = json.loads((folder / "task.json").read_text())
        model_id = (model_task.get("image_task_id") or model_task.get("refine_task_id") or model_task.get("refine_id")
                    or model_task.get("task_id") or model_task.get("id"))
        state["rig_task"] = call("POST", f"{API}/rigging", {"input_task_id": model_id, "height_meters": a.height})["result"]
        save()
    rig = wait("rigging", state["rig_task"])["result"]
    if not (folder / "rigged.glb").exists():
        download(rig["rigged_character_glb_url"], folder / "rigged.glb")
    basic = rig.get("basic_animations") or {}
    for name in ("walking", "running"):
        if basic.get(f"{name}_glb_url") and not (folder / f"{name}.glb").exists():
            download(basic[f"{name}_glb_url"], folder / f"{name}.glb")

    for action in a.actions:
        key = f"anim_{action}"
        if key not in state:
            state[key] = call("POST", f"{API}/animations", {"rig_task_id": state["rig_task"], "action_id": action})["result"]
            save()
        dst = folder / f"{key}.glb"
        if not dst.exists():
            download(wait("animations", state[key])["result"]["animation_glb_url"], dst)


if __name__ == "__main__":
    main()
