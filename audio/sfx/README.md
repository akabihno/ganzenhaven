# Sound effects

Generated with `tools/elevenlabs_sfx.py` (ElevenLabs sound generation). The raw MP3s and
their prompts (`raw/<id>.json`) are in `raw/`. The game plays the OGGs here, on the
**Sfx** bus.

Processing from raw MP3 to OGG (`ffmpeg … -c:a libvorbis -q:a 6`):

- A fixed linear gain per file, so loops sit around -24 LUFS and one-shots around -18 LUFS
  with nothing clipping. No compression, so the transients stay sharp.
- `bird_thud_tumble`: the first 0.48 s is trimmed (near-silence before the hit), with a 10 ms
  fade-in, so the thud lands on its cue.

| File | Length | Used in |
|---|---|---|
| harbour_fog_loop | 20 s loop | seagull crash: outside the window throughout |
| hearth_fire_loop | 15 s loop | seagull crash: the hearth |
| gull_approach | 4.5 s | seagull crash: the bird's cries as it flies in |
| glass_shatter | 3 s | seagull crash: the leaded glass breaking |
| shutter_bang | 1.5 s | seagull crash: the right shutter knocked back |
| bird_thud_tumble | 2 s | seagull crash: hitting the floor and rolling |
| gull_indignant | 2 s | seagull crash: the squawk on getting up |

Levels and timing within the scene are set in `scripts/SeagullCrash.cs` (`PrepareSound`).
