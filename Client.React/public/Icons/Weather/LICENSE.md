# Weather Icons

The SVGs in `svg/` are from **Weather Icons 2.0.10** by Erik Flowers
(http://erikflowers.github.io/weather-icons), licensed under the **SIL Open Font License 1.1** —
copyright notice and full license text in `OFL.txt` alongside. (Upstream's MIT/CC BY terms cover
its CSS and docs, which aren't vendored.)

The one exception is `svg/stadium.svg` (shown for indoor/dome games): Google's Material Icons
"Stadium" (outlined) glyph, licensed under the **Apache License 2.0**
(https://github.com/google/material-design-icons/blob/master/LICENSE).

The set is pruned to the icons `Client.React/src/utils/weather.ts` maps to. Add a file back only
together with the mapping that uses it — `weather.test.ts` pins this folder's contents.
