#!/bin/sh
# Generates the funeral cutscene's sound effects with ElevenLabs and converts them for the
# game (audio/sfx/<id>.ogg; raw MP3s and prompts in audio/sfx/raw/). Skips sounds that are
# already there, so it can simply be run again. Needs ffmpeg.
#   sh tools/make_funeral_sfx.sh
cd "$(dirname "$0")/.." || exit 1
R=audio/sfx/raw

make() {   # id, seconds, loop(0/1), loudness target (LUFS), prompt
	[ -f "audio/sfx/$1.ogg" ] && { echo "= $1"; return; }
	if [ ! -f "$R/$1.mp3" ]; then
		loop=""; [ "$3" = 1 ] && loop="--loop"
		python3 tools/elevenlabs_sfx.py "$R/$1.mp3" --seconds "$2" $loop --influence 0.6 --prompt "$5" || return
	fi
	ffmpeg -loglevel error -y -i "$R/$1.mp3" -af "loudnorm=I=$4:TP=-2:LRA=11" -ar 44100 -c:a libvorbis -q:a 6 "audio/sfx/$1.ogg" \
		&& echo "+ audio/sfx/$1.ogg"
}

make night_wind_loop  20 1 -24 "Cold night wind over a seaside graveyard on a hill, soft gusts through grass, very distant sea surf, no music"
make procession_steps 12 1 -24 "A slow funeral procession: many people walking slowly on a wet gravel path at night, shuffling footsteps, a lantern handle creaking, quiet, no voices"
make coffin_ropes      6 0 -18 "Thick hemp ropes creaking under weight as a wooden coffin is slowly lowered into a grave, wood knocking softly against earth at the end"
make shovel_earth      7 1 -20 "A gravedigger's spade cutting into wet earth and throwing it onto a wooden coffin, slow shovelfuls, thud of soil on wood, night quiet"
make fox_bark          3 0 -18 "A single red fox barking twice in the distance at night, eerie, small echo"
