# V024 PC hub / account client review

Status: `AWAITING_HUMAN_REVIEW` for the bounded R24 result; this is not overall V024 completion. Latest fixed candidate: `6dcbfab7fbc9e14a40c730a45cd64f2a02db0e07` (`refs/verification/f024-r24`). The bounded account path has Editor production-`OnGUI` event evidence plus built-Player direct-handler evidence; the footer, collection layout, and spell-text corrections have Editor captures. Full fidelity to the original PC shaders, camera/framing, animation, and pixels has not been verified; no precise old-PC comparison has been completed. Real OS or Standalone GUI mouse input is also unverified. Keep `TASK-0011` and `TASK-0012` `IN_PROGRESS` pending human review of the captures and a decision on those remaining fidelity/input boundaries.

## R22 Editor GameView account GUI review (historical; 2026-10-29)

This was a read-only source and artifact review. The root ran the serial R22 build/GPU checks and the Editor replay; this verifier did not run Unity, Player, exports, or tests, and did not alter the shared calibration file.

### Candidate and provenance

- Fixed source: `e45067a03b00adf47affc90a70fcb5f4d15fe7f3` (`refs/verification/f024-r22`). The Player build's `Assembly-CSharp.dll` SHA-256 is `C85CDA658909DF14256421A70E6FC003F4EA0AABC9FF68F760CA51163A1E8D44`. The Editor-run assemblies are `Assembly-CSharp.dll` `248AA26EA30C37E9D4CEC83EA55FD711AEF298AB0601BC7B04B9EDB704EC0D5F` and `Assembly-CSharp-Editor.dll` `BF350C2B959895C15E867213F0F44431A0F7B40BA3E46517B7F472ABB88FA86E`.
- `work/f024-account-editor-gui-r5/provenance.json` records the same source snapshot before and after the run, with 271 source files and six compiled/runtime artifact entries unchanged. The tracked Git blob identities for all 271 source records match the fixed commit. The verifier worktree has `core.autocrlf=true` and CRLF checkout bytes, so its raw working-file SHA-256 differs from the LF checkout hashes in provenance; the Git blob identities still match. The five non-calibration runtime artifact files were independently hash-checked against the provenance. The calibration entry's before/after hash is equal; the calibration file itself was not read.
- `logs/editor.log` identifies Unity `2022.3.60f1c1`, non-batch Editor Play Mode, the existing `Assets/Scenes/Main.unity`, a fixed 1920×1080 GameView target, and `PlayUnfocused`. The runner exited `0`.

### Actual Editor input and account flow

- `F024-account-editor-gui-replay.md` describes the Editor-only route. `F024AccountEditorReplayRunner` selects an already-present GameView and queues events through `EditorGUIUtility.QueueGameViewInputEvent(Event)`. `ClientApp.AccountReplayInput` observes the matching `Event.current` and `rawType` in production `ClientApp.OnGUI`; normal `GUI.Button` handling and page handlers run. The harness checks `GUIUtility.hotControl` transitions rather than setting hot control to simulate a click. Pack dragging reaches the normal MouseDown/MouseDrag/MouseUp branch in `DrawF024PacksPage`.
- The r5 report records `result=ok`, nine steps, 123 input events, 26 completed successful RPCs, and 18 screenshots. All 18 images are 1920×1080. The 123 input records are 41 MouseDown, 41 MouseDrag, and 41 MouseUp events. All were queued, observed by `OnGUI`, matched by event marker and pointer, consumed, and passed hot-control checks; there were no queue errors or event mismatches. Forty MouseUp events reached recorded production handlers. The single unhandled MouseUp is the expected pack release outside the opening slot.
- The observed RPCs are `account_register` ×2, `account_state` ×10, `account_shop_buy` ×1, `account_pack_open` ×1, `account_disenchant` ×6, `account_craft` ×1, `account_deck_save` ×1, `account_login` ×1, `account_logout` ×1, and `account_resume` ×2. Every request has `completed=true`, `ok=true`, and an empty error.
- The outside-slot release has no open handler or request. The subsequent in-slot release reaches `F024OpenPackFromDrag` and produces exactly one `account_pack_open` response with five opened cards. The report then records five reveal steps, collection checks, server-backed craft/disenchant, saved deck ID/name, persistence after logout/login, and two authenticated accounts joining the same match with server deck IDs. The match trace shows `GAME_START`; the replay ends with the primary peer disconnecting, so this is match entry evidence, not a played two-seat duel.

### Source review and scope limits

- The R22 source contains the proposed UI corrections: `MenuPresentation` explicitly activates `TheBox_TrayPacksCards` for the Hub footer and disables unsupported `Shop_Coin_shadow`; `NativeHubScene` hides the duplicate native crafting/done controls. The r5 screenshot verifies the relevant pages are rendered and their interactions work.
- `QueueGameViewInputEvent` creates synthetic Unity Editor input delivered to the GameView Player's production `OnGUI`; it is not an OS-injected mouse event. This run is not a Standalone Player run and does not supersede the separate Standalone `GUI-r5` result recorded in `TASK-0011`, which had zero RPCs. Real OS mouse behavior and Standalone account UI remain unverified.
- The r5 menu screenshot visibly contains a white footer counter square and an empty badge after enabling the tray. R23 addresses these footer artifacts (see the later review below). The R5 captures are functional evidence, not proof of exact old-PC appearance; the requested old-PC comparison remains open.

## R23 Editor GameView account GUI review (historical; 2026-10-29)

This was a read-only source and artifact review. The root ran the serial R23 build/GPU checks and Editor replay. This verifier ran the read-only evidence analyzer and inspected source and captures; it did not run Unity, Player, exports, or tests, and did not read or alter the shared calibration file.

### Candidate and provenance

- Fixed source: `f915346678286e9185615a8d73fb98f7c73e1b51` (`refs/verification/f024-r23`). The Player build's `Assembly-CSharp.dll` SHA-256 is `830ABEB3044AE7AEF230425E71EA1824EE359ED1765071038A22852071EE8318`. The Editor-run assemblies are `Assembly-CSharp.dll` `A839E3681F5B0A56428D4D45931391165431FDFDA9987E62037CF9D6F0D827B9` and `Assembly-CSharp-Editor.dll` `968C42E058A7AE1DF6F7174D075549CEFC73D48E1E3C4782B286213FAF222934`.
- `work/f024-account-editor-gui-r6/provenance.json` records the same source snapshot before and after the run, with 271 source files and six compiled/runtime artifact entries. Every recorded Git blob matches the fixed commit; source and artifact records are unchanged across the run. The five non-calibration runtime artifact files were independently hash-checked. The calibration entry's before/after hash is equal; the calibration file itself was not opened.
- `logs/editor.log` identifies Unity `2022.3.60f1c1`, `BatchMode: 0`, `Assets/Scenes/Main.unity`, a 1920×1080 GameView target, and `PlayUnfocused`. The replay exited `0`.

### Editor input and account flow

- The R6 report records `result=ok`, nine steps, 105 input events, 23 completed successful RPCs, and 19 screenshots. All 19 captures are 1920×1080. The events are 35 each of MouseDown, MouseDrag, and MouseUp. The read-only analyzer exited `0` with all checks true: native GameView receipts, production IMGUI hot-control transitions, coordinate consistency, successful requests, pack-open boundary, full-HD screenshots, and five pack reveal frames. All 105 events were queued, observed by production `OnGUI`, matched by event marker and pointer, and consumed. Thirty-four MouseUp events reached handlers; the single unhandled one is the expected release outside the pack-opening slot.
- The 23 RPCs are `account_register` ×2, `account_state` ×10, `account_shop_buy` ×1, `account_pack_open` ×1, `account_disenchant` ×3, `account_craft` ×1, `account_deck_save` ×1, `account_login` ×1, `account_logout` ×1, and `account_resume` ×2. Every request completed successfully without an error. The out-of-slot release generated no open request; the in-slot release reached `F024OpenPackFromDrag` and produced exactly one successful five-card open.
- The replay records account registration and initial state, the six daily/weekly quests, a shop purchase, pack inventory and reveal, collection/economy actions, server deck save, persistence through logout/login, and two accounts entering the same match queue with server deck IDs. The match evidence establishes entry, not a played two-seat duel.

### Source review, captures, and limits

- `MenuPresentation` now hides the unsupported `PackCount_mesh`, `ShopNewNotice`, and the tray's base renderer while preserving/restoring the captured footer state. The R6 main-menu capture no longer shows the white counter square, empty badge, or wide tray strip; the footer buttons remain visible.
- `ClientApp.HubPages` constrains the Collection grid and filters to the 659-design-unit paper region and sizes the four-column cards to fit it. The Collection overview shows eight cards, filters, search, and the four-column grid within the paper; its pagination row is aligned below the paper without the earlier horizontal overflow. The button labels and filter text are legible without the earlier clipping. The repeated updated Collection view also stays within the paper. These captures demonstrate the bounded layout correction, not exact old-PC fidelity.
- `UIText.HubPreviewRuleText` disabled outline and shadow only while rendering card preview rule text and restored the previous setting. In the R6 overview, spell rule text remained pale because the existing user calibration has a white `fillOverride`; R24 corrects this in the scoped preview branch below. No calibration data was modified.
- `--analyze-editor` reports the route as `editor-game-input-queue`: synthetic Unity Editor GameView events observed by the Player's production `OnGUI`, not OS-injected mouse input. The report says account fields were prefilled, so no keyboard entry is evidenced. This is not a Standalone Player GUI run and does not supersede the separate Standalone `GUI-r5` result in `TASK-0011`, which had zero RPCs. Real OS mouse and Standalone account UI input remain unverified.
- The requested precise comparison against the old PC reference remains open. Do not claim complete old-PC visual restoration or overall V024 PASS from this Editor flow.

## R24 final bounded review (2026-10-29)

This was a read-only review of the fixed R24 source, the Editor GUI-r7 evidence, and the Standalone direct-handler-r9 evidence. The root ran the R24 build/GPU checks with exit `0`; this verifier did not run Unity, Player, exports, or tests, and did not read or modify the shared calibration file.

### Fixed source and artifact identity

- Fixed source: `6dcbfab7fbc9e14a40c730a45cd64f2a02db0e07` (`refs/verification/f024-r24`). R24 changes only the `UIText` fill-override branch used by `HubPreviewRuleText`: preview card rule text uses the calling style's dark ink instead of the user's white `fillOverride`, while shadow and outline remain disabled for that scoped text. Other calibrated text and battle rendering continue through the original branch.
- The final Player `Assembly-CSharp.dll` SHA-256 is `DE00EF49750E154361902F6D98339C3AC243DD5C40B7D1172123A582DC931489`. Editor `Assembly-CSharp.dll` is `5EE0744B1B33F64881F9C7E075D7B9C46460316BEBA74AA84558F93F7E69139B`; `Assembly-CSharp-Editor.dll` is `968C42E058A7AE1DF6F7174D075549CEFC73D48E1E3C4782B286213FAF222934`.
- GUI-r7 provenance binds both sides of the Editor run to R24 with 271 source files and six unchanged artifacts. Every recorded Git blob matches the fixed source; all five non-calibration artifact files were independently hash-checked. Its Editor replay exited `0`.
- Standalone direct-r9 reports `result=ok`, Player exit `0`, no timeout, and no replay errors. Its `provenance.json` was written after the run and records its `before` DLL/EXE identities from the launch-time `summary.json`; the summary SHA-256 matches the file. The after identities match the same R24 build and the current Player DLL. All 18 screenshots are valid 1920×1080 PNGs.
- The root-run focused Python suite log records `41 passed` in `27.16s`. This covers the selected Python contracts and does not prove Unity rendering or GUI input.

### Editor GameView `OnGUI` evidence

- GUI-r7 records `result=ok`, nine steps, 99 mouse events (33 each of MouseDown, MouseDrag, and MouseUp), 22 successful RPCs, and 19 screenshots. All screenshots are 1920×1080. The read-only analyzer independently exited `0`: native GameView receipts, production IMGUI hot-control transitions, coordinate consistency, successful requests, pack-open boundary, full-HD screenshots, and five reveal frames all passed. All 99 events were queued, observed by production `OnGUI`, matched by marker and pointer, and consumed. The single unhandled MouseUp is the expected pack release outside the opening slot.
- The 22 RPCs are `account_register` ×2, `account_state` ×10, `account_shop_buy` ×1, `account_pack_open` ×1, `account_disenchant` ×2, `account_craft` ×1, `account_deck_save` ×1, `account_login` ×1, `account_logout` ×1, and `account_resume` ×2; each completed successfully without error.
- The R7 menu capture confirms the white counter square and empty badge are gone while the footer controls remain. The Collection overview shows the four-column grid, filters, search, and eight cards in the paper's 659-design-unit content area, with the pagination row below it. Spell rule text is now dark and readable; the smaller button and filter labels are not clipped in these captures. This verifies the targeted R23/R24 rendering fixes, not complete old-PC fidelity.

### Standalone direct-handler evidence and remaining boundary

- Direct-r9 is `input_mode=direct-production-handler`, with zero `gui_inputs`, eight steps, 14 successful requests, 18 full-HD screenshots, Player exit `0`, and no timeout or replay errors. It exercises server-backed production request handlers in the built Player and captures their rendered results. It does not exercise mouse delivery, `GUI.Button`, or Standalone IMGUI input; the R7 Editor queue remains the only current native event evidence.
- The Editor queue feeds synthetic Unity Editor GameView events into the production `OnGUI`; it is not OS-injected mouse input. R9 is direct-handler evidence, not a Standalone GUI-input pass. No keyboard entry is evidenced because the replay prefilled account fields. The old Standalone GUI-r5 run's zero-RPC result remains a separate historical failure, not superseded by these direct-handler captures.
- Recommendation: `AWAITING_HUMAN_REVIEW` for the bounded, playable R24 result. Keep `TASK-0011` and `TASK-0012` `IN_PROGRESS`; review the supplied captures against the original PC reference and decide whether further shader, camera/framing, animation, pixel-level, or OS/Standalone GUI-input work is required. Do not mark the full old-PC restoration done based on these bounded checks.

## Earlier candidate and verification history

- Reviewed candidate: `f1694d7ba8f63a0aefbf9409380768875baba0ae` (`verification/v024-hub`).
- Test-only commit: `7df2f83` adds `F024AccountStateChecks`, wires it into `BuildClient`, and tightens TCP `set_id` type assertions. No business code was changed in this verification worktree.
- The Hub Feature later reported fix commit `dbf2acdf51941d44aeb29294d97632fb11b53915` for the account-switch findings. It is not part of the fixed candidate reviewed here; the root integrator will run the Editor checks against the fixed candidate and its successor.

## Local evidence

- Command: `$env:PYTHONDONTWRITEBYTECODE='1'; & 'E:\HS\HearthstoneClone\.venv\Scripts\python.exe' -X utf8 -m pytest tests/test_accounts.py tests/test_v024_accounts_acceptance.py -q -p no:cacheprovider`
- Result: `18 passed` (15.55s). This covers the Python account service/TCP acceptance path; it does not execute Unity serialization or UI.
- Editor command on the fixed candidate: `F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe -batchmode -quit -projectPath E:/HS/HearthstoneClone/UnityClient -executeMethod F024AccountStateChecks.Run -logFile E:/HS/HearthstoneClone/work/v024-account-state-before-fix.log`
- Result: exit `1`; `20 passed` and `3 failed`: account-scoped collection cache, logout draft reset, and rotated emote hit area. The log is `work/v024-account-state-before-fix.log` in the main checkout.
- `git diff --cached --check` passed before the test-only commit.
- This verifier did not start Unity; the root ran the Editor check serially. No Player, native asset export, or local account service was started for this review.

## Account crafting API follow-up (2026-10-27)

- On the pre-fix verifier tree (`a8c3b85`), `test_core_cards_cannot_be_crafted_from_free_inventory` reproduced the server-side gap. With 1000 dust, crafting a granted CORE card returned `ok=true` for both normal and golden requests; the account changed from `(gold=1000, dust=1000, revision=1)` to `(1000, 560, 3)`.
- Independently reviewed the account fix `dcda2b08acb9ce3f8853e164054247f8666ea6e0`: `_craft` rejects rarity-2 free cards and set 1637 CORE cards before charging dust. Cherry-picked that existing fix as `42f8fc2b0887deda410f681140cfd52edd2562d0` on this verifier branch; no additional business edits were made.
- Re-ran `tests/test_accounts.py tests/test_v024_accounts_acceptance.py` with the repository venv and `-p no:cacheprovider`: `20 passed` (15.54s). The regression checks normal and golden craft of free and CORE cards, ensures wallet/revision/inventory stay unchanged, and verifies rejected request replay remains rejected.
- Scope: the CORE counterexample invokes `AccountService.process`, so this verifies the account mutation API path, not raw TCP delivery or GUI craft controls. The supplied integrated SHA `cc90be40188116220839c4113a6b59a02caf39a8` still has the old `_craft` implementation without this guard; its integrated successor must include the fix and rerun the counterexample before this result can be attributed to that candidate. Unity, Player, and GUI acceptance remain separate.

## GUI replay adapter source review (2026-10-27)

- Reviewed the input adapter added by `633d24f` in `E:/HS/agent-worktrees/f024-hub`; its current `0a6a185` worktree differs from that commit only in `AssetRegistry.cs`, outside the adapter.
- The replay queues logical MouseDown/MouseDrag/MouseUp steps but consumes them only during a real `Event.current.type == Repaint` frame. `F024ReplayGuiButtonInput` manually changes `GUIUtility.hotControl` and returns `true` for its queued MouseUp; the normal `GUI.Button` call on that repaint did not receive a synthesized Unity mouse event. The resulting `clicked`/`selected` value does reach the same page-level action branch, such as shop navigation or collection selection.
- Pack dragging has a separate injectedDown/injectedDrag/injectedUp branch in `DrawF024PacksPage`; it manually changes drag state and invokes the shared `F024OpenPackFromDrag` helper on an in-slot release. It does not exercise the ordinary `Event.current` MouseDown/MouseDrag/MouseUp branch.
- The distinction is explicit in its evidence: replay reports use `production-gui-repaint-adapter`, and each input record stores the expected mouse event alongside `current_event_type=Repaint` and `input_context=repaint-adapter`. `F024ReplayGuiEventType` is declared but unused; this candidate does not replace `Event.current.type` globally.
- Evidence boundary: a successful run can show the visible control target was registered, the adapter reached the associated page handler, and the handler produced its recorded request/state effect. It does not establish real OS mouse delivery, standard IMGUI Button behavior, or actual drag pointer movement. No Unity/Player run was performed for this source review; keep real GUI input as a separate acceptance item.

## Historical client behavior findings (candidate f1694d7)

1. **Confirmed FAIL — account switch reuses another account's collection filter.** In the fixed candidate, `ClientApp.HubPages.cs` builds `f024CollectionCacheKey` from only revision and filter/page/search values. Accounts A and B with the same revision but different opened cards leave A's cached card set visible after switching to B. The Editor check called production `F024BuildCollectionList` with equal revisions and distinct inventories; its second-account assertion failed.
2. **Confirmed FAIL — logout retains an unsaved deck draft.** The fixed candidate clears the account snapshot/token and selected deck on successful logout, but leaves `f024DraftDirty` and the old deck id/cards intact. `F024RefreshDeckDraftFromSnapshot` returns early while the draft is dirty, so the next account can continue to see the previous account's draft. The Editor check called the production account response handler and snapshot refresh; the logout reset assertion failed.
3. **Confirmed FAIL — rotated emote hit testing includes empty bounding-box corners.** `HeroEmoteMenu.CalibratedRect` converts a rotated hit rectangle to an axis-aligned bounding rectangle, and `OverMenu` uses that rectangle to keep the context menu open. The Editor check constructed a point outside all transformed hit rectangles but inside the reported bounds, then replayed the production input handler; the menu incorrectly remained open.
4. **Account replay is not GUI input replay.** `ClientApp.AccountReplay.cs` directly calls request methods, `F024OpenPackFromDrag`, and `F024AdvancePackReveal`; it does not synthesize shop/collection button clicks or the pack-list `MouseDown` → `MouseDrag` → `MouseUp` sequence. Its screenshots can document resulting pages, but they do not prove those controls are clickable or the drag target works.

## Bounded code review

- **Observed:** the client DTO declares `set_id` as `int` and `request_id`, `CardId`, and `deck_id` as strings. The Editor check exercises Unity `JsonUtility` on these DTOs; the TCP test asserts the server rejects string and boolean `set_id` and returns integer shop IDs.
- **Observed:** the account gateway retains the same JSON and request ID for retryable mutations after a connection failure; server-side tests cover idempotency and concurrent wallet/inventory invariants. Client reconnect/retry timing and no-double-charge behavior still need a live disconnect/reconnect run.
- **Observed:** logout/login UI disables endpoint fields while an account is present; passwords are cleared after request construction; the session store uses Windows DPAPI. No Player evidence was gathered for endpoint switching, token persistence, or secret leakage.
- **Observed:** the client deck validator rejects cards outside the active playable set and cards not owned by the account. The new Editor check invokes this production validator; Python acceptance tests separately cover service-side deck ownership and scope.
- **Unknown:** actual GUI rendering/input for F6 speech/emotes, pack dragging/reveal, account login/logout, collection filtering, craft/disenchant, deck editing, and quest/shop navigation; real audio playback/listener timing; complete Player account retry behavior; user-reference visual comparison.

## Earlier follow-up request (superseded in part by R9)

The Hub Feature reported `dbf2acdf51941d44aeb29294d97632fb11b53915` for the cache and logout fixes. The battle Feature committed inverse-matrix `OverMenu` hit testing as `05b1e3aa5f092ba826eedbe3bf8aae7aecbc12d5`. At the time this follow-up was written, these fixes had not been verified on an integrated successor. R9 now reports 24 `F024AccountStateChecks` passing; the actual Player GUI still needs a successful shop purchase and the remaining account interactions still lack input evidence.

## R9 historical integration evidence (2026-10-27; superseded by R14)

This verifier only read the serial artifacts listed below. It did not run Unity, Player, GPU checks, exports, or tests, and did not modify local account/profile data.

### Editor and native-scene captures

- Fixed source: `ca8051ca121117b10d692b9132207d1b294da35c`, parent `206be1bd6dffbe07b7a705ec609f435c6f9f0325`. The R9 account Player summary records the same `Assembly-CSharp.dll` SHA-256 shown above.
- `work/f024-unity-build-r9.log` records Hub Mesh `6`, Account State `24`, Battle `31`, and Native Weapon `135` checks passing, then exits batch mode with return code `0`. The weapon Editor line explicitly limits its result to source texture/tint and additive-base restoration; complete Actor lifecycle remains unverified. These checks do not establish full Player behavior or visual fidelity.
- `work/f024-hub-scenes-r2.log` exits `0`; the subsequent `work/f024-hub-native-scenes/shop.png` capture from the scene-only GPU check has no white header strip. It shows the native frame with an empty content panel and no page text/cards. The R9 Player was not rebuilt and recaptured after this scene fix, so it does not supersede the white strip seen in the R9 account GUI screenshot below.

### Account and shop input

- `work/f024-account-player-r3/summary.json` belongs to the earlier R8 DLL `a040edf8219c7287af270f7c5a958f9b1394d3aadc8aeb19151080c850e51161`. It records `input_mode=direct-production-handler`, 15 successful account requests, and `gui_inputs=[]`. Its runner failure is solely the 15 screenshots being `1680x1050` instead of the required `1920x1080`. This documents handler/API state changes, not GUI clicks or pack dragging.
- R9 `work/f024-account-gui-r2/summary.json` records `input_mode=production-event-current-injection`, Player exit `0`, no timeout, and `1920x1080` screenshots. The injected `MouseDown/MouseDrag/MouseUp` sequences for `account.register` and `menu.shop` were consumed, preserved their `hotControl`, and reached `F024Register` and `F024OpenPage(shop)`; the account registration and state requests succeeded.
- The next injected target, `shop.buy:3`, failed on `MouseDown`: the recorded point lies inside the recorded button rect, but `event_consumed=false`, `hotControl` remains `0`, and no buy handler or `account_shop_buy` request occurs. The replay has only `account_register` and `account_state`; the runner result is `failed`. The isolated R2 SQLite database confirms the account remained at `1000` gold, `0` dust, revision `1`, 185 collection entries, 11 decks, and no packs. Do not count the old R1 or this R2 shop-buy attempt as PASS.
- The R2 shop screenshot shows the store content and balances, but also a conspicuous white horizontal rectangle across the header and a black top band. The later scene-only GPU capture does not show the white rectangle, but there is no post-fix Player capture. No artifact reviewed here records actual OS mouse delivery; direct handler replay and injected `Event.current` replay remain separate from OS input.

### F6 speech and emote editor

- `work/f024-ui-r2/ui-capture-report.json` records two captures, `battle.speech` and `battle.emotes`, each with exit `0`, four hand cards, and three field cards. The screenshots visibly show speech previews and seven emote controls arranged three left/four right; the visible hero selector rows include `HERO_01` through `HERO_03` and a scroll area.
- These are UI editor captures with a rendered battle preview. The report does not record a changed calibration value saved and reloaded, selection of all eleven heroes, real battle-time emote/speech input, or verified audio playback. Do not expand capture success into those claims.

### Paladin Recruit audio and observer state

- `work/f024-paladin-recruit-audio-r2/run.json` reports a bounded Paladin Recruit run PASS on the R9 DLL. The driver evidence records one hero-power play, one summon, one Recruit summon, one source cue, one area clone and finish notification, and zero pending/unhandled/unbound actions. Its two source records are one short voice cue (`0b172af97e533a48568c40c7.wav`) and one looping area cue (`b248ece18bdc57738255ba2f.wav`). This is narrow evidence for the recruit cue and local effect path, not official fidelity.
- The driver submits end turn at frame `3825`, the next-turn state is applied at frame `3827`, and the visual tail releases at frame `4152`. At the observer, `publicHash=560f687460ae301eb7273d52b45c3a74725515f14bf3fa410a9c8546edd8b5f0` while `displayHash=15f756929a0c62dbb1f00085776e0e16ffa8ab883ee8dbfdebb2d9f5a121fa06`. The independent follow-up analysis reports the observer's old tail and next-turn-hash timing assertions failing; a stricter authority-version fix is still pending. The overall two-player timing path is therefore not PASS despite the driver's bounded run result.

### Native weapons

- `work/f024-native-weapons/summary.json` records eight Player processes with exit `0`, but `analysis.passed=false`. In both seats for `CS2_106` and `EX1_567`, slot `41/42` draw cues started, while the analysis reports mismatched or missing slot `5/7` cues and says attacks did not retain the expected class-legal weapon. Successful 41/42 playback does not satisfy the complete weapon lifecycle check.
- The R9 Editor suite separately reports 135 Native Weapon checks passing; its log says the complete Actor lifecycle remains unverified. The Player summary does not embed a DLL hash, so it is not independently self-bound to the R9 DLL. Keep the Player fixture failure open until a corrected fixture demonstrates the required weapon state and cues on both seats.

## R9 status at that review (superseded by R14)

The bounded Python account/economy acceptance remains covered by `docs/verification/V024-accounts.md`, including the corrected CORE/free-card crafting guard and 16-pack catalog. R9 adds passing Editor state checks and visible capture evidence, but the account shop interaction remains failed, actual OS input and end-to-end account UI flows are unverified, the observer tail/hash timing assertions fail, and the native-weapon Player fixture fails. TASK-0009/TASK-0011 are not fully verified from these artifacts.

## R14 integrated evidence review (2026-10-27)

This update is a read-only review of the serial R14 artifacts. The verifier did not run Unity, Player, GPU checks, exports, or tests, and did not modify account/profile data. The overall V024 remains `IN_PROGRESS` because the account shop interaction below still fails.

### Candidate, Editor checks, and calibration persistence

- Fixed candidate: `0d65f7f44d031cc980b6a18b92b43eef9cf257f7` (`refs/verification/f024-r14`); R14 `Assembly-CSharp.dll` SHA-256: `de48b3df8c8bcf96b1745e2b46d4d7ceaf984c6a92a3bb14a0c0244a384ffc49`.
- `work/f024-unity-build-r14.log` records Hub Mesh `6`, Account State `24`, Battle `32`, and Native Weapon `142` checks passing, then exits batch mode with return code `0`. These are Editor checks, not a claim of full UI or official visual fidelity.
- The 32 Battle checks exercise the minion voice policy: a minion `CardPlay` does not consume a summon voice, a bound new `Summon` does, duplicate event IDs do not replay it, and the summoned entity's `CardId` is kept across same-batch death/morph/removal cases. They include the Paladin `HERO_04bp` → `CS2_101t` token path. A temporary calibration file also round-trips own/enemy speech geometry and text plus all seven emote layouts/text styles; it does not overwrite the user's calibration file.
- `work/f024-ui-r2/ui-capture-report.json` is earlier R9 Player capture evidence. It shows the rendered speech and seven emote controls, but does not bind those screenshots to R14, prove all eleven hero selectors, or show real battle-time inputs. Keep the R14 save/reload Editor result separate from visual/input acceptance.
- The Native Weapon Editor run checks authority batches for the existing Rogue `CS2_082` weapon and generic card IDs `CS2_106`/`EX1_567`. It observes each batch twice and asserts one cue per expected source event, including equip/wear/break/replacement, source clip decode/playback, and reset cleanup. This confirms the adapter's event count, not overlap with every independent Rogue-specific audio route. Its log explicitly says complete Actor lifecycle remains unverified. The R14 Player run below does not include a Rogue-specific Player overlap case.

### Paladin Recruit summon voice and authority timing

- `work/f024-paladin-recruit-audio-r2/evidence-index.json` is the strict analyzer output for the two Player roles. The underlying run identifies DLL SHA-256 `9c1aae8186488ec4d119dba59aa81712141378ee0bbcbda5f7ed1e2590c89784` (R9), not the R14 DLL. The R9→R14 production summon/voice files (`NativeMinionSummonVoices.cs`, `AudioEventRegistry.cs`, `NativeAreaPlayback.cs`, `NativeBattleSummons.cs`, `NativeAudio.cs`, and `NativeAudioPlayback.cs`) are unchanged; therefore this is source-equivalent evidence for that bounded route, not an exact R14 Paladin binary run.
- The driver role records one `HERO_04bp` power, one `CS2_101t` Summon, one Recruit source voice, and one local area source; both source records are played, and the bounded area clone/finish path completes without unhandled or unbound actions. The driver can submit end turn after source completion while the visual tail remains active; version `8`/the next turn arrives before tail release and the display hash synchronizes before the tail is released.
- The observer's `event_order_valid=false` and `recruit_summon_count=0` are role-correct: that client did not initiate a card/power action. The analyzer only accepts its in-tail public/display hash difference after checking the authority journal's unique accepted power transition (`v6→v7`, hash `15f756…`), accepted end turn (`v7→v8`, hash `560f68…`), and a matching `TURN_START` frame while the source tail is active and input is locked. The index marks `observer_authority_advanced_during_area_tail=true`; do not interpret the observer's zero self-initiated action counters as a failed summon.
- The source voice is identified by the Player `VOICE_CS2_101t_PLAY_0` logs on both clients and the source PCM hash `34674c4dfe872d677bbb30def687a8ecf9125e46bb078fc353412d305ecd4613`. The Listener capture is explicitly the full Unity `AudioListener` scene mix, not an isolated track; its envelope correlation is only a mixed-PCM similarity indicator. Do not use it as proof of a separated voice stem or official fidelity.

### Generic weapon source audio

- `work/f024-native-weapons-r2/summary.json` reports eight Player processes with exit code `0` and `analysis.passed=true`. Both seats for `CS2_106` and `EX1_567` record source cue playback for equip slots `41/42`, durability slot `7`, and break slot `5`; both attack records identify `weapon_card_id_source="before-snapshot"`. Each per-seat report has no analysis errors.
- The Player summary does not embed a DLL hash. Root's serial run record confirms the `build/client` DLL was checked as R14 `DE48…` before and after this R2 run, with no parallel build; the R2 files were written after the R14 DLL. This attribution is based on that serial build/run record and is not self-contained in the Player summary. The runner should embed the DLL hash on the next run.
- The Player `listener_audio` records are full scene mixes. The source-specific `weapon_audio` records identify the cue files and `playback_started` timestamps; no isolated Listener stem is claimed. The Player run covers the two generic weapon fixtures, not an actual Rogue-specific duplicate-play scenario. The Rogue `CS2_082` no-repeat behavior is covered only by the passing Editor authority-batch checks described above.

### TASK-0011 account/shop replay remains failed

- `work/f024-account-gui-r4/summary.json` records the R14 DLL hash above, Player exit `0`, no timeout, and two `1920x1080` captures. Its mode is `production-event-current-injection`: register and shop navigation consume their injected events and reach their handlers; registration and state requests succeed.
- The next `shop.buy:3` event is ignored. The target point is inside the enabled control and scroll hit area, but the native control reports `Ignore`, `control_id=0`, and `event_consumed=false`; there is no buy handler or shop-buy request. The replay contains only `account_register` and `account_state`, so this is not a successful purchase. No artifact records real OS input. Keep TASK-0011 and overall V024 `IN_PROGRESS` until the purchase path succeeds.

### Current bounded conclusion

TASK-0009's bounded summon/source, calibration persistence, and two generic weapon paths pass on the evidence above, subject to the R9 Paladin source-equivalence and R2 Player hash-attribution limits. Rogue `CS2_082` no-repeat behavior is only checked at the Editor adapter level; there is no Player result for overlap with an independent Rogue-specific route. The Listener captures are not isolated audio. These results do not establish official fidelity or every hero/UI path. TASK-0011 remains failed at `shop.buy:3`; account-side register/state works, but no purchased pack is evidenced. Therefore V024 remains `IN_PROGRESS`.
