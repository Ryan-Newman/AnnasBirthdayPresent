# AnnasBirthdayPresent
Website just to celebrate my wife Anna's brithday!

A banana-themed birthday page for Anna Banana. Banana confetti falls when the page opens, and the
headline is set in **Banana Bunch**, a font made for the occasion where every stroke of every
letter is one banana.

## What's here

| File | What it is |
| --- | --- |
| `index.html` | The birthday page. Open it in a browser, or publish it with GitHub Pages. |
| `BananaBunch.ttf` | The Banana Bunch font. Double-click it and choose **Install**. |
| `source/BuildBananaBunch.cs` | The program that draws the bananas and writes the font. |
| `source/index.template.html` | The page before the banana shapes are filled in. |
| `build.ps1` | Rebuilds the font and the page from `source/`. |

## The page

- "HAPPY BIRTHDAY ANNA BANANA!" is drawn from the same bananas as the font. The bananas drop into
  place on load and scatter when your mouse or finger gets close.
- Banana confetti rains down on load. **Throw more bananas** starts another shower, and clicking
  or tapping anywhere throws a handful.
- A different name can go after `#` in the address, for example `index.html#Anna_Banana`.
  Underscores become spaces.

## The font

- One banana per stroke: H is three bananas, T is two, E is four, O is two bananas joined at the stem.
- Covers A–Z, 0–9 and `! ? . , ' - : + /`. Lowercase letters type as capitals.
- It is a colour font (COLR/CPAL). Apps that support colour fonts, including web browsers, show
  yellow bananas with stems and tips. Other apps show the same shapes in your text colour.

## Rebuilding

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Letter shapes are defined in
`GlyphSet.Build()` in `source/BuildBananaBunch.cs`, one curve per banana.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

This writes `BananaBunch.ttf` and `index.html`, plus `build/font-test.html` for checking the font
in a browser.
