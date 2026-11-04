# F026 Native Hub Rendering Notes

Status: fixed implementation 0f379fa069342f96a871f6dd8aac806f9ff8439b; root serial build r7 and final r6 Editor/Windows replay passed. Independent V026 b7830d0 accepted this bounded local scope; full original lighting, controller and pixel fidelity remain open.

## Pack opening crop and tray target

`SCENE_PACK_OPENING` is rendered with its bounds camera, then the projected `frameBounds` rectangle is used as the visible UV crop for both the page draw and diagnostic capture. This removes the horizontal clear margins created when the native scene bounds (about 1.23 aspect) are drawn into a wide screen rectangle. `TryGetPackOpeningLayout` maps its projected source bounds through the same crop.

The tray layout is the `PackTray` renderer group clipped to the visible page. The drop target is the projected bounds of `PackOpening/SocketTray/PackOpeningTray_Disk`, which is the circular recess; it is a visual target derived from source geometry, not a recovered collider. The source `BackButton` group keeps its source transform; the scene ends 65 design units above the screen bottom to reserve the shared dark footer. Moving the button in r5 had exposed a baked English Back button in the tray texture, so that attempted adaptation was rejected. The title uses the source `UberText_HeaderText` anchor with local font rendering, and the back label uses local text. Draw and input share these projections.

For `SidePackOpeningTray`, the original `Texture2D` is `SidePackOpeningTray`, asset `c344939515b83d62d4b9f349`, bundle `initial_base_global-8411aad9-texture-13.unity3d`, CAB `CAB-51315388aaeaca768a550d02d88ba4c3`, pathID `-6581863386966491286`. Its serialized `m_TextureSettings` has U/V/W wrap values `0`, which is `TextureWrapMode.Repeat`; the local material clone now restores that source setting. Static neutral `_Tint` on the three tray groups remains an appearance reconstruction. The original shader consumes Unity direct light and spherical-harmonic inputs; those runtime light values and the original active camera state remain unknown.

## Bulk-buy frame composition

The failed `ShopDetailsBooster` prefab is not used as the modal surface. `modal.bulkBuy.frame` is composed from exact indexed textures and a nine-slice layout:

- `Shop_Detail_Frame` (asset `0f7638f41c309dd56bae6fd7`, bundle `essential_base_global-texture-1.unity3d`, CAB `CAB-e02b69b1bbe893a6736884865127e637`, pathID `1593616158842758052`) provides the outer wood/metal edge and paper crop.
- `Shop_DetailBundle_Frame` (asset `c2cabe4e14642f5b14388556`, bundle `initial_base_global-8411aad9-texture-28.unity3d`, CAB `CAB-45fb0d51ff05737c7b176dc0094841fa`, pathID `-7663725261492350269`) provides the quantity-panel edge.
- `ShopBackingPlate` (asset `e0fd679602ccee9a48f8370a`, bundle `essential_base_shop_global-content-0.unity3d`, CAB `CAB-de5771ecfb00e5aa7273d4f9cd73ba71`, pathID `3387382257238294434`) provides the purple backing.
- `Shop_Detail_ConversionFrame` (asset `9956ac8b86a02344d257637c`, bundle `essential_base_shop_global-content-0.unity3d`, CAB `CAB-de5771ecfb00e5aa7273d4f9cd73ba71`, pathID `-8700645518898738320`) is retained in the export manifest with provenance but is not drawn in this composition.

The widget's normalized layout relative to its outer rect is preview `[0.052, 0.071, 0.897, 0.624]`, parchment description `[0.040, 0.706, 0.676, 0.269]`, and quantity/price panel `[0.718, 0.718, 0.245, 0.259]`. Pixel source rectangles and nine-slice borders are recorded in `manifests/hub-native-widgets.json`. This is a native-texture reconstruction; it does not claim a recovered original modal GameObject or exact original screen transform.

## Social flyout derived buttons

`social.flyout` renders only `FriendListFrame/FriendListFrame_Root/FlyoutMenu/Frame`. The exported FlyoutMenu button transforms extend outside that frame in the isolated initial state, so the output draws the verified `menu.button` three-piece gold button four times in derived equal slots, ordered Add, RAF, Remove, Privacy. `TryGetSocialFlyoutLayout` returns those same four rectangles for GUI hitboxes. The positions are derived layout, not recovered source hitboxes.

The static pack page disables the `Socket`, `SocketAccent`, and `Hover` FX subtrees, plus the unsupported Multiply fakeShadow. The retained Disk has a net radius about 32.05 world units while the outer wood frame's height is about 58.1; including the whole SocketTray in the frame exposed gray disk edges outside the wood. Camera bounds now use `PackTray`, the exact `SocketTray/PackOpeningTray` renderer, and `BackButton`; visible UV height is additionally clipped to that wood renderer, while width includes the side tray. Draw, header, back, and input projections share the crop. The rejected full wood-frame sprite backdrop was removed because it added a second title plate. These are bounded static composition adaptations, not restoration of the original live camera/controller.
