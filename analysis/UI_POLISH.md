# UI verification, 0.2.1

Verified on 2026-10-20 in the installed Steam build 25936063, Unity 6000.2.8f1.

- The initial catalog viewport contains all 12 cards and its page controls. Content height is 402 UI units; viewport height is 406 units.
- Keyboard events for `Котик Ёжик` produced the expected input value and visible TMP glyphs. The font fallback covered every character.
- The game's `InputFieldCache.FocusingInputField` returned true for the mod's focused input.
- Two temporary in-memory preset fixtures displayed the skin with a hat and the same skin without a hat. Cyrillic names remained visible.
- Screenshots were read from the running client's framebuffer. The catalog and preset screens were visually inspected, including icon controls, neutral input surfaces, image layering and preview size.
- The test harness disabled purchase automation in memory before the game's first update. It restored config values and temporary preset fixtures, then quit its test launch. No appearance preset was applied or written during the test.

QA images are in `artifacts/ui-0.2.1/catalog.png`, `presets.png`, and `presets-panel.png`. The scratch harness and its installed DLL are removed after verification. It is not part of the release package.

The release also passes 55 local checks, covering configuration round trips, Cyrillic preset serialization, wheel movement and bounds, gift deduplication and generated control artwork. These checks do not replace exercising every cosmetic combination or theme in the game.
