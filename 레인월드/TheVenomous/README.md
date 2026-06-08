# The Venomous

Rain World mod development copy.

## Current State

- Installed mod copy: `D:\SteamLibrary\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\stwam.starter`
- Editable copy in this workspace: `TheVenomous\mod`
- Mod id: `stwam.starter`
- Display name: `The Venomous`
- Korean slugcat text: `독살이`

## What Is Already Here

- `mod\modinfo.json`: Remix mod metadata.
- `mod\plugins\StwamRainWorldMod.dll`: compiled gameplay plugin.
- `mod\plugins\StwamVenomousCampaignImage.dll`: campaign select image plugin.
- `mod\thumbnail.png`: Remix mod thumbnail using the campaign art.
- `mod\illustrations\venomous_campaign_main.png`: The Venomous campaign image.
- `mod\text\text_eng\strings.txt`: English menu text.
- `mod\text\text_kor\strings.txt`: Korean menu text.
- `mod\world\fg-rooms`: starter playable room files.
- `mod\world\cd`, `fg`, `hn`, `rsg`, `sc`: planned region skeletons.

## Next Good Targets

1. Confirm the mod appears in Rain World Remix.
2. Test the existing starter room and note crashes or missing assets.
3. Decide the next feature:
   - custom slugcat stats and food rules,
   - venom spear behavior,
   - poisoned creature curing,
   - night/day route changes,
   - region and room expansion.

## Development Notes

This workspace currently contains the compiled plugin but not the C# source project.
To change DLL behavior, install a .NET SDK and add/recover the source project.
To change text, region metadata, maps, and room assets, edit files under `mod` directly.

When a change is ready, copy `TheVenomous\mod` back into:

`D:\SteamLibrary\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\stwam.starter`
