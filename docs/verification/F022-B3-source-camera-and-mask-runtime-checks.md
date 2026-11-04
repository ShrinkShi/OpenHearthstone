# F022-B3: source camera projection and H04 mask runtime checks

## Source camera basis

The checked-in original-battle-cameras.json Main Camera profile is perspective, with vertical field of view about 34.870445 degrees and Y position 32. Its approximate vertical board span at the board plane is 2 * 32 * tan(FOV/2) ~= 20.1 world units. The generic native preview path uses a 32-unit orthographic span. This explains the roughly 32 / 20.1 ~= 1.59x size difference without changing source particles or applying a screenshot scale.

The existing NativeAreaPlayback and NativeSuperSpellPlayback already apply scene.SourceCamera to their production effect cameras. NativeSuperSpellBoard.FromPresentation also uses the same camera to project GUI points into the board plane. The ClientApp scene callback therefore needs to pass the existing Main Camera profile for HERO_04bp and HERO_07bp, while retaining the existing HERO_08bp route.

Callback condition: (data.card_id=="HERO_08bp"||data.card_id=="HERO_04bp"||data.card_id=="HERO_07bp")?fireblastCamera:null

## Editor checks added

- H04 production-route checks pass the original camera to both scene projection and the area effect, then assert the runtime camera uses the source projection. The initialized and first-forward-Seek hero projections are logged in logical pixels and checked against a two-pixel tolerance.
- Existing H04 SourceProjection coverage tests both controller sides and the original camera projection at the actual hero GUI point.
- H04 node 6 and 7 checks read back instance mesh colors in [0,1], verify the shared AssetRegistry meshes retain their original byte-valued colors, and verify the instance clones are released with playback disposal.
- H07 source-root checks use the same Main Camera profile and measure the projected source point against both players' GUI hero-power points within two logical pixels.

## Verification status

The checks are source changes only; Unity/Player was not run in this worktree. Root should merge the ClientApp callback condition and run the Editor checks serially, followed by H04/H07 screenshot comparisons. The camera profile is source-confirmed; final visual match still requires the serialized replay.
