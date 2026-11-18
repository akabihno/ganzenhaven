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

Everything that should happen after picking an option must be indented under it.

When an option's lines are done, the same choices come back **without the one just picked**,
plus "That's all." to move on, so the player can ask about everything in turn. Once every
option has been picked (or "That's all." is chosen), the script carries on after the choices.
A single option, or a chain of them nested inside each other, simply runs through.

An option that is a decision rather than a topic ends with `@leave`: picking it closes the
choices for good (it can go before or after `@if`):

```
* Pay two stuivers.  @leave
  $add stuivers -2
* On second thought, no.  @leave
* Here are your nets.  @if item:nets @leave
```

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

An indented entry is a **step** of the task above it. Steps are added and ticked off like any
entry, but the journal writes them under their task, and they move to the "done" page with it:

```
find_fish: Find fish
  fish_suspicious: Fish is suspicious
```

```
@if journal:find_fish and fish_required
  $journal fish_suspicious     shows up under "Find fish"
```

Add a step only while its task is open (check `journal:<task>`); a step whose task isn't in the
journal is written as a task of its own. `$journal remove <task>` removes its steps too.

## Inventory

Things the player carries. A script gives one and takes it back:

```
$item cloth              puts it in the inventory (with a "Picked up" notice)
$item remove cloth       takes it out again
@if item:cloth           the player has it
```

Names and descriptions are in `dialogs/en/items.txt` and `dialogs/ru/items.txt`, one
`id: name` per line with an optional description indented under it:

```
cloth: Piece of cloth
  A scrap of coarse burlap, torn off a thorn.
```

Clicking a thing in the inventory has the author read its name and description
(`python3 tools/elevenlabs_voice.py items` records them).

The picture is `assets/art/items/<id>.png` (transparent background). `@show <id>` holds the
item up close above the text box, and `@hide <id>` puts it down.

Something lying in a location that can be picked up is a hotspot with its own **Picture**,
cut out of the background, and **HideIf** `item:<id>`, so it disappears once taken (see the
Cloth in scenes/game/locations/bushes.tscn).

### Using two things together

In the inventory, click one thing and then another. That runs
`dialogs/<lang>/combine/<a>_<b>.txt`, with the two ids in alphabetical order, which does the
rest with `$item`:

```
# combine/cloth_scissors.txt
You cut a long strip from the cloth and fold it double. A blindfold, more or less.
$item remove cloth
$item blindfold
```

No such file: "These don't go together."

### Money

The purse is the variable `stuivers` (20 stuivers make a gulden), shown in the inventory as
"1 gulden, 18 stuivers":

```
$add stuivers 40          two guldens
$add stuivers -2          pay for a pint
@if stuivers >= 2
```

For scale: in the Dutch towns a pint of beer cost about a stuiver, a master carpenter earned
about a gulden a day.

## Ledger

The harbour ledger in the office (the Ledger hotspot has **Opens** `ledger` instead of a
Dialogue) is written in `dialogs/en/ledger.txt` and `dialogs/ru/ledger.txt`:

```
== Debts outstanding                                    a new page, with its title
Debtor | For | Owed                                     columns split by |; first row = headings
Gravedigger | a lost oar | 3 stuivers
Dock Worker, for the Zwaluw | mooring | 2 guldens -> ledger/zwaluw @if !done:collect_debt
```

`-> script` makes the row clickable (it runs that script over the open book); `@if` at the very
end shows the row only while the condition holds. Pages are shown two at a time and turned
with the arrows. A hotspot can also have **Opens** `journal` or `inventory`.

## The two acts

**`tavern_keeper_dead`** is the one flag the game itself knows: setting it starts the second
half, and **night falls**. For now it is set by a placeholder option on the tavern's barrels
(`tavern/barrels.txt`); move that block wherever the death really happens.

At night:

* every location has a night picture, `assets/art/backgrounds/<id>_after.png`; so do the
  pieces cut from a background (the tavern's counter front and nets, the oak's trunk and
  stones: `<name>_after.png` beside the day one), so they keep matching it;
* people standing about are in cold moonlight, and someone speaking up close is a little
  dimmed; things lying about without a night picture (the cloth, the scissors) too;
* people who only come out at night (the priest and the gravedigger, `ShowIf =
  tavern_keeper_dead`) are painted by night already, so they aren't tinted again — the list is
  `CharacterArt.IsNightLit`; their day-lit originals are kept in `assets/art/night_src/`;
* a character spot with **Shadow** ticked casts a soft patch of shade under the feet;
* the map, journal, ledger and inventory are read by candlelight;
* the title screen shows the harbour by night once the saved game is past the death.

Characters and hotspots switch between the acts with their ShowIf / HideIf fields (e.g. the
Tavern Keeper: `HideIf = tavern_keeper_dead`). F9 flips the act for testing.

### The foxes at the Strange Oak

At night fifteen small foxes, standing like people and holding hands, dance slowly round the oak inside
the stone circle, tails swaying. They are 3D (`assets/models/fox/fox.glb`) drawn over the
painting; the **FoxDance** node in `scenes/game/locations/strange_oak.tscn` has the settings:
when they're there (`ShowIf`, `tavern_keeper_dead` by default), how many and how big
(`Count`, `FoxScale`: smaller foxes need more of them to keep hands joined), the ring's radius,
how fast it turns, and the step: `CrouchStep` (creeping, the default) or `SideStep` (upright).

Fireflies drift about the oak there too, flashing (`FireflyCount`).

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

Expressions so far: `seagull tilt` (head cocked, asking), `fisherman blindfold`.

Someone standing in a location (a CharacterSpot) can stand with an expression too
(**Expression** field), and can have a second spot elsewhere that takes over through
ShowIf / HideIf: the blindfolded fisherman in the harbour, the Tavern Keeper behind the counter
(`keeper_at_counter`) with the cat on it (`cat_on_counter`). The counter front is cut out of the
tavern background (tavern_counter_front.png) and drawn over him, so he stands behind it.

## Voice

Lines are voiced with ElevenLabs by `tools/elevenlabs_voice.py`:

```
python3 tools/elevenlabs_voice.py office/seagull                 both languages
python3 tools/elevenlabs_voice.py office/seagull --lang ru --force
python3 tools/elevenlabs_voice.py --all                          everything, items too
```

`--all` only records what's new or changed, so it's the one to run after editing.

Who speaks with which voice is in `tools/voices.json`: the author Bill, the Seagull Lily,
the Black Cat Roger, the Herb Woman Alice, the Tavern Keeper George, the Dock Worker Charlie,
the Fisherman Harry and Captain Vos Brian. It also has a
delivery direction per character and per expression (`Seagull [tilt]:` lines sound curious).
Each recording is named after its line's text, so after you edit a line it simply stops
playing until you run the tool again; recordings of deleted lines are removed. Player
choices, placeholder lines and lines with `{variables}` are not voiced. `_word_` is stressed.

## Testing (editor / debug builds)

* **F9** flips the act (sets or clears `tavern_keeper_dead`) and reloads the screen.
* **F10** opens the map from anywhere.
* **Esc** skips a cutscene.
