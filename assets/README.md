# Brand assets

| File | Use it for |
|---|---|
| `logo.svg` | Horizontal lockup on a light background |
| `logo-dark.svg` | Horizontal lockup on a dark background |
| `logo-mark.svg` | The mark alone, above ~24px: avatars, docs, slides |
| `logo-favicon.svg` | The mark at or below ~24px: favicons, tab icons, inline chips |
| `logo-128.png` | Raster copy of the mark, 128×128, transparent. Exists because NuGet's `PackageIcon` rejects SVG |

## The idea

An anvil. SkillForge makes and checks skills, so the mark says *this is where skills are made*.

**It is deliberately not a badge.** No shield, no seal, no tick. SkillForge never certifies a skill as safe
(ADR-006) — it reports diagnostics and lets the reader decide. A logo shaped like a security seal would
promise the one thing the tool refuses to promise.

## Colour

The ember gradient is the only colour, which is what lets one file sit on a light or a dark background
without a second variant:

| Stop | Hex |
|---|---|
| 0% | `#FFB13C` |
| 50% | `#F26D2B` |
| 100% | `#D62F3F` |

Wordmark: `#16202E` on light, `#F1F5F9` on dark. "Forge" is a **solid** `#EC5B26` (`#FF7A3D` on dark), not
the gradient — gradient text collapses towards its darkest stop at small sizes, and two gradients in one
lockup stopped the mark being the focal point.

## Two cuts of the same mark, on purpose

`logo-mark.svg` carries a horn taper and a spark. Below about 24px both turn to mush, so
`logo-favicon.svg` drops the spark, shortens the horn, thickens every bar and fills more of the square.
Same silhouette, less detail — which is all a favicon can actually show. Rendered side by side at 16, 24, 32
and 48px, the difference is obvious; that comparison is why the second file exists rather than a guess that
one file would do.

## Proportions

Three ratios carry the whole mark, and each was wrong before it was right:

- the stem is about a quarter of the plate's width — wider, and it stops reading as an anvil;
- the foot is narrower than the plate — matching it turns the silhouette into a letter T;
- the spark sits just above the plate — out in the corner it read as a separate object, and three sparks in
  a row read as a crown.

Ink spans x 5–56 and y 10–53 in the 64-unit square, which centres the mark optically rather than
numerically.

## Regenerating the PNG

The raster copy is produced from the SVG, so it can never drift from it by hand:

```powershell
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
$html = '<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:transparent}</style>' +
        '<img src="logo-mark.svg" width="128" height="128">'
Set-Content icon.html -Value $html -Encoding utf8NoBOM
& $edge --headless=new --disable-gpu --default-background-color=00000000 `
        --screenshot=logo-128.png --window-size=128,128 (Resolve-Path icon.html).Path
```

Any SVG rasteriser works; the flag that matters is `--default-background-color=00000000`, without which the
PNG gets an opaque white background.

## Typography

The wordmark uses a system font stack — `Inter, 'Segoe UI', system-ui, -apple-system, Arial, sans-serif` —
and is real `<text>`, not outlines. Shipping a webfont, or converting letters to paths, would add weight and
a licence to a repository whose whole point is a small local CLI. The consequence is that the wordmark
renders in whatever the reader's system offers; the mark carries the identity where typography cannot
follow. If a fixed wordmark ever matters more than that trade, outline the text then.

## Licence

Same as the project: [MIT](../LICENSE).
