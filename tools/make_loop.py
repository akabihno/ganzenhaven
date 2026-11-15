#!/usr/bin/env python3
"""Turn a generated music track into a seamless loop (needs ffmpeg).

    python3 tools/make_loop.py <in.mp3> <out.ogg> [--crossfade 4] [--lufs -20]

The last <crossfade> seconds are blended into the first ones and the file then starts
right after that blend, so playing it on repeat has no seam. Loudness is normalised so
all location tracks sit at the same level. OGG Vorbis, because Godot loops it
sample-accurately (MP3 adds encoder padding, which clicks at the loop point).
"""
import argparse, json, subprocess, sys


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "json", path],
                         capture_output=True, text=True, check=True).stdout
    return float(json.loads(out)["format"]["duration"])


def main():
    p = argparse.ArgumentParser()
    p.add_argument("src")
    p.add_argument("dst")
    p.add_argument("--crossfade", type=float, default=4.0)
    p.add_argument("--lufs", type=float, default=-20.0)
    a = p.parse_args()

    L, C = duration(a.src), a.crossfade
    if L < 4 * C:
        sys.exit(f"{a.src}: too short ({L:.1f}s) for a {C}s crossfade")
    f = (
        f"[0:a]asplit=3[x][y][z];"
        f"[x]atrim={C}:{L - C},asetpts=PTS-STARTPTS[body];"
        f"[y]atrim={L - C}:{L},asetpts=PTS-STARTPTS,afade=t=out:st=0:d={C}:curve=qsin[tail];"
        f"[z]atrim=0:{C},asetpts=PTS-STARTPTS,afade=t=in:st=0:d={C}:curve=qsin[head];"
        f"[tail][head]amix=inputs=2:normalize=0[seam];"
        f"[body][seam]concat=n=2:v=0:a=1,loudnorm=I={a.lufs}:TP=-1.5:LRA=11[out]"
    )
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.src, "-filter_complex", f, "-map", "[out]",
                    "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5", a.dst], check=True)
    print(f"{a.dst}: {duration(a.dst):.1f}s loop")


if __name__ == "__main__":
    main()
