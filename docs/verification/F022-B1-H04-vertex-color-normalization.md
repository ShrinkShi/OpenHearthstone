# F022-B1: H04 native mask vertex colors

## Finding

The H04 offscreen mask uses two native mesh renderers. The installed source `ReuseFX_Holy_Untargeted_01` confirms both meshes store vertex colors in `m_VertexData` channel 3 with `format=2` (`UNorm8`, four components, 100 vertices each):

- `FX_Hero_Mask` → source mesh `FX_Hero_Mask`, CAB `CAB-8a1f07d9e06200f3772e6647fecfce97`, path ID `-3943821637359471946`.
- `FX_Hero_VertexGlow` → source mesh `FX_Hero_FadeMask`, same CAB, path ID `-474445178604948562`.

The checked-in mesh cache stores these channels as byte-valued floats (`0` and `255`). `AssetRegistry.MeshFile` forwards them to `Mesh.colors` without dividing by 255. That differs from the original UNorm8 vertex contract. The mask shader computes `1 + alpha * (color - 1)` before its serialized `Zero / SrcColor` blend, so a byte-valued interpolated alpha can turn the intended smooth mask into a hard negative color factor.

## Change

The H04-only profile now validates the exact node, mesh cache ID, shader, blend factors, render queue and sorting order. It clones the two meshes per area instance and converts only integral `0..255` vertex channels to `0..1`; already normalized channels pass through. The shared AssetRegistry meshes and cache files are not changed. The clones are released with the area playback instance.

No global shader, source JSON, or other hero-power mesh behavior changes in this commit.

## Evidence and verification

- Cache value summary: mask mesh has 100 vertices and 400 channel values with unique values `{0,255}`; glow mesh has 100 vertices and 400 channel values with unique values `{0,255}`.
- Source blend contracts: mask `src=0,dst=3,srcAlpha=0,dstAlpha=3`; glow `src=5,dst=1,srcAlpha=5,dstAlpha=1`; both render queue `3000`, sorting order `0`.
- Existing r9 GPU diagnostic: without mask, offscreen green mean was `146.2`; with mask it was `90.6`; mask-only reduced red mean from `7.1` to `0.1`. This demonstrates the mask is active, but does not establish whether its edge interpolation matches the source UNorm8 input.
- Unity/Player rendering was not run from this worktree. Root should perform the serialized H04 GPU/Player A/B and inspect the mid-frame halo before treating the visual result as confirmed.

External runtime injection of the hero Actor remains behavior-reconstruction `Unknown`; this change addresses only the recovered local mesh/color contract.
