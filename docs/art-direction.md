# Visual direction and asset provenance

The September 2026 cabinet pass studied the public presentation of the playable games and math tools on [GameMathemagics](https://gamemathemagics.com/), especially the separation between cabinet and analysis views, compact control hierarchy, readable reel window, and theme-led framing. Those are design references rather than copied components.

Aether Loom's Blazor markup, CSS, interaction code, cabinet layout, and analysis display were implemented in this repository. No GameMathemagics source code or artwork is included.

Two original raster assets were generated specifically for this project with OpenAI's built-in image-generation tool, then resized and converted to WebP for the deployed client:

- `src/SlotGame.Playable/wwwroot/images/symbols-atlas.webp`: an exact 4×2 transparent atlas containing a crown-shaped thread spool, silk moth, weaving shuttle, dye bottle, linen roll, cord knot, aether crystal, and Spark rune. The prompt requested hand-painted mechanical-fantasy slot art, consistent framing, tactile materials, transparent cells, and no text, logos, characters, coins, or existing game imagery.
- `src/SlotGame.Playable/wwwroot/images/loom-workshop.webp`: a wide dark weaving workshop with brass machinery and quiet central space for the reels. The prompt requested an original mechanical-fantasy environment with dark walnut, oxidized brass, muted teal, warm practical light, and no text, UI, people, or recognizable existing game imagery.

The sprite atlas is used through CSS background positioning so the same compressed asset serves the reel window, compact analysis board, and paytable. The workshop image is shared by the page and cabinet frame with separate overlays. These images do not affect outcome generation or payout evaluation.
