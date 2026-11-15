# Dialogue scripts

One plain-text file per conversation, in two parallel folders:

```
dialogs/en/harbor/fisherman.txt
dialogs/ru/harbor/fisherman.txt
```

The path without the language folder and `.txt` is the script id (`harbor/fisherman`).
Characters and hotspots in the location scenes point at it in their **Dialogue** field.
If a Russian file is missing, the English one is used (with a warning in the output).

While the game runs from the editor, files are re-read every time a conversation starts:
edit, save, click the character again.

## Lines

```
Fisherman: Harbourmaster! The fish are looking at me.     speaker line
The fog thickens.                                          narration (shown in italics)
\Note: this is narration even though it has a colon.       leading \ forces narration
# comment                                                  ignored
```

A speaker line is `Name: text`. `Name [expression]: text` changes that character's sprite for
this one line (`Seagull [tilt]: Do you have fish?`); the next line without a tag goes back to
the sprite set by `@show`. The name is matched against the character names in
`translations/ui.csv`, so `Чайка [tilt]:` works in the Russian file. The name is whatever you write; it is shown as is, so the
Russian file writes `Рыбак:`. `_word_` is shown in italics; BBCode works too: `[i]…[/i]`,
`[b]…[/b]`, `[color=red]…[/color]`.
`{name}` inserts the value of a variable.

## Choices and conditions (indent with spaces)

```
* Looking how?
  Fisherman: Judgementally.
* Fish don't look.
  Fisherman: These ones do.
  $set fish_suspicious
* Ask about the tavern  @if !tavern_keeper_dead      option only shown while the condition holds
```

Everything that should happen after picking an option must be indented under it. A line at
the same level as the `*` lines ends the choice: it runs whichever option was picked.

```

@if tavern_keeper_dead
  Fisherman: ...and now they whisper.
@elif fish_suspicious
  Fisherman: Still looking.
@else
  Fisherman: Morning.
```

Conditions: `flag`, `!flag`, `coins >= 3` (also `<= == != > <`), `journal:find_fish` (entry
open), `done:find_fish` (entry ticked off), combined with `and` / `or`.

## Story state

```
$set flag            $unset flag
$set coins = 3       $add coins 1      $add coins -2
```

## Journal

```
$journal find_fish           add an entry (a notice pops up: "New in the journal: …")
$journal done find_fish      tick it off (shown struck through)
$journal remove find_fish
```

The text of each entry lives in `dialogs/en/journal.txt` and `dialogs/ru/journal.txt`, one
`id: text` per line, so the journal follows the language setting.

## The two acts

**`tavern_keeper_dead`** is the one flag the game itself knows: setting it starts the second
half. Locations then use `<background>_after.png` when one exists, and characters/hotspots
switch with their ShowIf / HideIf fields (e.g. the Tavern Keeper: `HideIf = tavern_keeper_dead`).

## Stage and flow

```
@show fisherman [expression] [left|center|right|farleft|farright|0.0–1.0]
@hide fisherman      @hide all
@bg tavern           background from assets/art/backgrounds/
@wait 1.5
@music tavern        switch the background track (audio/music/<id>.ogg); @music stop
                     (until the next screen change: every location plays its own track)
@cutscene funeral    play a 3D cutscene, then continue this conversation
@travel graveyard    end the conversation and go to a location
@map                 end the conversation and open the map
@label name   @goto name     (labels at the top level of the file)
@call other/script           run another script, then come back
@jump other/script           continue in another script
@end
```

Sprites are `assets/art/characters/<id>.png`; an expression is `<id>_<expression>.png`
(falls back to the plain sprite until it exists). An expression sprite should be cropped to the
same size as the plain one, so swapping doesn't make the character jump.

Expressions so far: `seagull tilt` (head cocked, asking).

## Voice

Lines are voiced with ElevenLabs by `tools/elevenlabs_voice.py`:

```
python3 tools/elevenlabs_voice.py office/seagull                 both languages
python3 tools/elevenlabs_voice.py office/seagull --lang ru --force
```

Who speaks with which voice is in `tools/voices.json` (the seagull is Lily), including a
delivery direction per character and per expression (`Seagull [tilt]:` lines sound curious).
Each recording is named after its line's text, so after you edit a line it simply stops
playing until you run the tool again; recordings of deleted lines are removed. Player
choices, placeholder lines and lines with `{variables}` are not voiced. `_word_` is stressed.

## Testing (editor / debug builds)

* **F9** flips the act (sets or clears `tavern_keeper_dead`) and reloads the screen.
* **F10** opens the map from anywhere.
* **Esc** skips a cutscene.
