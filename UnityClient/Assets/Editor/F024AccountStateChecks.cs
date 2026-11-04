using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class F024AccountStateChecks
{
    static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    static int passed;

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("F024 account state check: " + message);
        passed++;
    }

    static GameObject NewClient(out ClientApp app)
    {
        var root = new GameObject("F024 account state checks") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        app = root.AddComponent<ClientApp>();
        return root;
    }

    static FieldInfo Field(string name)
    {
        var field = typeof(ClientApp).GetField(name, InstancePrivate);
        if (field == null) throw new MissingFieldException(typeof(ClientApp).FullName, name);
        return field;
    }

    static void Set(ClientApp app, string name, object value) { Field(name).SetValue(app, value); }
    static T Get<T>(ClientApp app, string name) { return (T)Field(name).GetValue(app); }

    static object Call(ClientApp app, string name, params object[] args)
    {
        var method = typeof(ClientApp).GetMethod(name, InstancePrivate);
        if (method == null) throw new MissingMethodException(typeof(ClientApp).FullName, name);
        return method.Invoke(app, args);
    }

    static F024AccountSnapshot Snapshot(string accountId, int revision, params F024AccountCard[] cards)
    {
        return new F024AccountSnapshot
        {
            account_id = accountId,
            username = accountId,
            display_name = accountId,
            revision = revision,
            cards = cards ?? new F024AccountCard[0],
            decks = new F024AccountDeck[0]
        };
    }

    public static void Run()
    {
        passed = 0;
        var failures = new List<string>();
        RunCheck("wire types", VerifyWireTypes, failures);
        RunCheck("account-scoped collection cache", VerifyCollectionCacheTracksAccount, failures);
        RunCheck("logout draft reset", VerifyLogoutClearsDraft, failures);
        RunCheck("deck request locks and rejected resume", VerifyDeckRequestLocks, failures);
        RunCheck("deck completion waits for matching success", VerifyDeckCompletionResponse, failures);
        RunCheck("revision ordering", VerifyOlderSnapshotIsIgnored, failures);
        RunCheck("pack result and reveal deduplication", VerifyPackResponseUpdatesCollectionAndDeduplicatesReveal, failures);
        RunCheck("craft and disenchant costs", VerifyCraftAndDisenchantCosts, failures);
        RunCheck("owned and playable deck validation", VerifyDeckValidationUsesOwnershipAndPlayability, failures);
        RunCheck("rotated emote hit area", VerifyRotatedEmoteHitArea, failures);
        if (failures.Count > 0)
            throw new Exception("F024 account state checks failed (" + passed + " passed): " + String.Join(" | ", failures));
        Debug.Log("F024 ACCOUNT STATE CHECKS PASSED: " + passed);
    }

    static void VerifyDeckCompletionResponse()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "page", "collection");
            Set(app, "f024CollectionTab", 1);
            Set(app, "f024DraftDirty", true);
            Set(app, "f024DraftName", "draft");
            Set(app, "f028PendingSaveRequestId", "save-1");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_deck_save", request_id = "other", ok = true
            });
            Check(Get<int>(app, "f024CollectionTab") == 1 && Get<bool>(app, "f024DraftDirty") &&
                Get<string>(app, "f028PendingSaveRequestId") == "save-1", "unrelated success preserves draft and editor");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "error", operation = "account_deck_save", request_id = "save-1", error = "rejected"
            });
            Check(Get<int>(app, "f024CollectionTab") == 1 && Get<bool>(app, "f024DraftDirty") &&
                Get<string>(app, "f024DraftName") == "draft" && Get<string>(app, "f028PendingSaveRequestId") == "",
                "save failure preserves draft and editor");
            Set(app, "f028PendingSaveRequestId", "save-2");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_deck_save", request_id = "save-2", ok = true
            });
            Check(Get<int>(app, "f024CollectionTab") == 0 && !Get<bool>(app, "f024DraftDirty") &&
                Get<string>(app, "f028PendingSaveRequestId") == "", "matching success returns to collection");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyDeckRequestLocks()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "f024DraftDirty", true);
            Set(app, "f024DraftName", "未保存草稿");
            Set(app, "f028PendingSaveRequestId", "save-pending");
            Set(app, "f028PendingCodeRequestId", "decode-pending");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_state", request_id = "other", ok = true
            });
            Check(Get<string>(app, "f028PendingSaveRequestId") == "save-pending" &&
                Get<string>(app, "f028PendingCodeRequestId") == "decode-pending",
                "unrelated account response must not release pending deck requests");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_resume", request_id = "resume", ok = false, error = "expired"
            });
            Check(Get<string>(app, "f028PendingSaveRequestId") == "" && Get<string>(app, "f028PendingCodeRequestId") == "",
                "rejected resume clears pending deck request locks");
            Check(Get<bool>(app, "f024DraftDirty") && Get<string>(app, "f024DraftName") == "未保存草稿",
                "rejected resume preserves the unsaved draft");
            Set(app, "f028PendingSaveRequestId", "save-pending");
            Set(app, "f028PendingCodeRequestId", "decode-pending");
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "error", operation = "account_resume", request_id = "resume", error = "expired"
            });
            Check(Get<string>(app, "f028PendingSaveRequestId") == "" && Get<string>(app, "f028PendingCodeRequestId") == "",
                "error-form resume rejection clears pending deck request locks");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void RunCheck(string name, Action action, List<string> failures)
    {
        try { action(); }
        catch (Exception error)
        {
            var cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
            failures.Add(name + ": " + cause.Message);
        }
    }

    static void VerifyWireTypes()
    {
        var buy = new F024ShopBuyRequest { request_id = "request-1", set_id = 7, count = 1 };
        string json = JsonUtility.ToJson(buy);
        var parsed = JsonUtility.FromJson<F024ShopBuyRequest>(json);
        Check(json.Contains("\"set_id\":7") && parsed.set_id == 7,
            "shop set_id serializes as a JSON integer");

        var deck = new F024DeckSaveRequest
        {
            request_id = "request-2", deck_id = "deck-opaque", name = "wire check",
            class_id = 2, cards = new[] { "CARD_001", "CARD_002" }
        };
        var parsedDeck = JsonUtility.FromJson<F024DeckSaveRequest>(JsonUtility.ToJson(deck));
        Check(parsedDeck.request_id == deck.request_id && parsedDeck.deck_id == deck.deck_id &&
            parsedDeck.cards.SequenceEqual(deck.cards), "request_id, deck_id, and CardId values remain strings");
    }

    static void VerifyRotatedEmoteHitArea()
    {
        var previous = UICalibration.Current;
        var calibration = new UICalibration("../work/f024-emote-hit-check.json");
        try
        {
            UICalibration.Current = calibration;
            calibration.Set(new UIAdjustment { id = "battle.emotes/THANKS", angle = 45, scaleX = 1.4f, scaleY = .8f });
            var menu = new HeroEmoteMenu();
            var hero = new Rect(865, 700, 190, 200);
            Check(menu.ReplayRightClick(hero.center, hero, "rotation-check"), "hero right-click opens the emote menu");

            var imageRects = (Rect[])typeof(HeroEmoteMenu).GetField("imageRects", InstancePrivate).GetValue(menu);
            var hitRects = (Rect[])typeof(HeroEmoteMenu).GetField("hitRects", InstancePrivate).GetValue(menu);
            var actualMatrices = new Matrix4x4[HeroEmoteMenu.Options.Length];
            var bounds = new Rect[HeroEmoteMenu.Options.Length];
            for (int i = 0; i < actualMatrices.Length; i++)
            {
                actualMatrices[i] = calibration.Get("battle.emotes/" + HeroEmoteMenu.Options[i])
                    .Matrix(imageRects[i], Vector2.one);
                bounds[i] = menu.OptionHitRect(HeroEmoteMenu.Options[i]);
            }

            Vector2 outside = Vector2.zero;
            bool found = false;
            for (int i = 0; i < bounds.Length && !found; i++)
            {
                for (float x = bounds[i].xMin + 2; x < bounds[i].xMax - 1 && !found; x += 3)
                for (float y = bounds[i].yMin + 2; y < bounds[i].yMax - 1; y += 3)
                {
                    var point = new Vector2(x, y);
                    if (hero.Contains(point) || !bounds.Any(rect => rect.Contains(point))) continue;
                    bool inActualBubble = false;
                    for (int target = 0; target < hitRects.Length; target++)
                    {
                        var local = actualMatrices[target].inverse.MultiplyPoint3x4(point);
                        if (hitRects[target].Contains(local)) { inActualBubble = true; break; }
                    }
                    if (inActualBubble) continue;
                    outside = point;
                    found = true;
                    break;
                }
            }
            Check(found, "rotated emote bounds expose a point outside every transformed hit rectangle");

            bool consumed;
            menu.ReplayInput(EventType.MouseDown, 1, KeyCode.None, outside, hero, "rotation-check", out consumed);
            Check(consumed && !menu.IsOpen,
                "right-click in the rotated bounds' empty corner closes the menu instead of hitting an emote");
        }
        finally { UICalibration.Current = previous; }
    }

    static void VerifyCollectionCacheTracksAccount()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "database", new CardDB
            {
                cards = new[]
                {
                    new CardDef { CardId = "CARD_A", Name = "A", Collectible = true, Type = 4, Cost = 1 },
                    new CardDef { CardId = "CARD_B", Name = "B", Collectible = true, Type = 5, Cost = 1 }
                }
            });
            Set(app, "f024CollectionClass", -1);
            Set(app, "f024CollectionMana", -1);
            Set(app, "f024CollectionOwnedMode", 1);
            Set(app, "f024CollectionPage", 0);
            Set(app, "f024CollectionSearch", "");
            Set(app, "f024Account", Snapshot("account-A", 9,
                new F024AccountCard { card_id = "CARD_A", normal = 1 }));
            Call(app, "F024BuildCollectionList");
            Check(Get<CardDef[]>(app, "f024FilteredCards").Select(card => card.CardId).SequenceEqual(new[] { "CARD_A" }),
                "owned filter reflects the first account inventory");

            Set(app, "f024Account", Snapshot("account-B", 9,
                new F024AccountCard { card_id = "CARD_B", normal = 1 }));
            Call(app, "F024BuildCollectionList");
            Check(Get<CardDef[]>(app, "f024FilteredCards").Select(card => card.CardId).SequenceEqual(new[] { "CARD_B" }),
                "same-revision account switch rebuilds the owned filter from the new inventory");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyLogoutClearsDraft()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "f024Account", Snapshot("account-A", 10));
            Set(app, "f024Token", "token-A");
            Set(app, "f024GatewayAuthenticated", true);
            Set(app, "f024DraftDirty", true);
            Set(app, "f024DraftId", "deck-A");
            Set(app, "f024DraftName", "A draft");
            Set(app, "f024DraftClassId", 2);
            Get<List<string>>(app, "f024DraftCards").Add("CARD_A");
            const string logoutWire = "{\"type\":\"account_result\",\"operation\":\"account_logout\",\"request_id\":\"logout-check\",\"ok\":true,\"snapshot\":null}";
            var logoutResponse = JsonUtility.FromJson<F024AccountResponse>(logoutWire);
            string logoutSnapshot = logoutResponse.snapshot == null ? "null" :
                "account_id=" + logoutResponse.snapshot.account_id + ", revision=" + logoutResponse.snapshot.revision;
            Debug.Log("F024 logout JSON snapshot parse: " + logoutSnapshot);
            Call(app, "HandleF024AccountResponse", logoutResponse);

            Check(!Get<bool>(app, "f024DraftDirty") && String.IsNullOrEmpty(Get<string>(app, "f024DraftId")) &&
                Get<List<string>>(app, "f024DraftCards").Count == 0,
                "successful logout clears the old account's unsaved deck draft");
            Check(!Get<bool>(app, "f024SnapshotRefreshQueued") && !Get<bool>(app, "f024GatewayAuthenticated") &&
                String.IsNullOrEmpty(Get<string>(app, "f024Token")) && Get<F024AccountSnapshot>(app, "f024Account") == null,
                "a successful logout with JSON snapshot:null does not queue an unauthenticated refresh");
            Set(app, "f024Account", Snapshot("account-A", 12));
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_craft", request_id = "late-mutation-check", ok = true,
                snapshot = Snapshot("account-A", 11)
            });
            Check(!Get<bool>(app, "f024SnapshotRefreshQueued"),
                "a late stale mutation cannot queue a refresh after logout cleared authentication");
            Set(app, "f024SnapshotRefreshQueued", true);
            Call(app, "F024RequestAccountState");
            Check(!Get<bool>(app, "f024SnapshotRefreshQueued"),
                "an account-state request without a session clears stale refresh-queued state");

            var deck = new F024AccountDeck { deck_id = "deck-B", name = "B starter", class_id = 3, cards = new[] { "CARD_B" } };
            Set(app, "f024Account", new F024AccountSnapshot
            {
                account_id = "account-B", revision = 1, decks = new[] { deck }, cards = new F024AccountCard[0]
            });
            Call(app, "F024OnSnapshotUpdated");
            Check(Get<string>(app, "f024DraftId") == "deck-B" && Get<string>(app, "f024DraftName") == "B starter" &&
                Get<List<string>>(app, "f024DraftCards").SequenceEqual(new[] { "CARD_B" }),
                "the new account snapshot becomes the active deck draft");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyOlderSnapshotIsIgnored()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "f024Token", "token-A");
            Set(app, "f024GatewayAuthenticated", true);
            Set(app, "f024Account", Snapshot("account-A", 12,
                new F024AccountCard { card_id = "CARD_A", normal = 1 }));
            Call(app, "HandleF024AccountResponse", new F024AccountResponse
            {
                type = "account_result", operation = "account_state", request_id = "old-state", ok = true,
                snapshot = Snapshot("account-A", 11, new F024AccountCard { card_id = "CARD_B", normal = 1 })
            });
            var current = Get<F024AccountSnapshot>(app, "f024Account");
            Check(current.revision == 12 && current.cards.Single().card_id == "CARD_A",
                "an older state response cannot replace the newer revision");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyPackResponseUpdatesCollectionAndDeduplicatesReveal()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            Set(app, "f024Token", "token-A");
            Set(app, "f024GatewayAuthenticated", true);
            Set(app, "f024Account", Snapshot("account-A", 12));
            var opened = Enumerable.Range(0, 5).Select(index => new F024OpenedCard
            {
                card_id = "PACK_CARD_" + index, new_card = index == 0
            }).ToArray();
            var response = new F024AccountResponse
            {
                type = "account_result", operation = "account_pack_open", request_id = "open-request", ok = true,
                open_id = "open-once", opened_cards = opened,
                snapshot = Snapshot("account-A", 13,
                    new F024AccountCard { card_id = "PACK_CARD_0", normal = 1 })
            };
            Call(app, "HandleF024AccountResponse", response);
            Check(Get<F024AccountSnapshot>(app, "f024Account").cards.Single().card_id == "PACK_CARD_0" &&
                Get<bool>(app, "f024PackRevealActive") && Get<F024OpenedCard[]>(app, "f024RevealCards").Length == 5,
                "pack response applies the server inventory before beginning the five-card reveal");
            Call(app, "F024AdvancePackReveal");
            Check(Get<int>(app, "f024RevealIndex") == 0, "the first opened card is revealable");
            Call(app, "HandleF024AccountResponse", response);
            Check(Get<int>(app, "f024RevealIndex") == 0 && Get<string>(app, "f024LastOpenedId") == "open-once",
                "a repeated open_id cannot restart or duplicate the reveal");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyCraftAndDisenchantCosts()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            int[] rarities = { 1, 3, 4, 5 };
            int[] craft = { 40, 100, 400, 1600 }, goldenCraft = { 400, 800, 1600, 3200 };
            int[] disenchant = { 5, 20, 100, 400 }, goldenDisenchant = { 50, 100, 400, 1600 };
            for (int i = 0; i < rarities.Length; i++)
            {
                var card = new CardDef { Rarity = rarities[i] };
                Check((int)Call(app, "F024CraftDust", card, false) == craft[i] &&
                    (int)Call(app, "F024CraftDust", card, true) == goldenCraft[i],
                    "normal and golden craft costs match rarity " + rarities[i]);
                Check((int)Call(app, "F024DisenchantDust", card, false) == disenchant[i] &&
                    (int)Call(app, "F024DisenchantDust", card, true) == goldenDisenchant[i],
                    "normal and golden disenchant values match rarity " + rarities[i]);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void VerifyDeckValidationUsesOwnershipAndPlayability()
    {
        ClientApp app;
        var root = NewClient(out app);
        try
        {
            var definitions = Enumerable.Range(0, 30).Select(index => new CardDef
            {
                CardId = "DECK_CARD_" + index, Class = 2, Rarity = 1, Collectible = true, Playable = true
            }).ToArray();
            Set(app, "database", new CardDB { classes = new[] { new ClassDef { id = 2, available = true } }, cards = definitions });
            var cardMap = Get<Dictionary<string, CardDef>>(app, "cards");
            foreach (var card in definitions) cardMap.Add(card.CardId, card);
            var deck = definitions.Select(card => card.CardId).ToArray();
            Set(app, "f024Account", Snapshot("account-A", 1,
                definitions.Select(card => new F024AccountCard { card_id = card.CardId, normal = 1 }).ToArray()));
            Check((string)Call(app, "F024ValidateOwnedDeck", deck, 2) == "",
                "a full deck of owned, supported cards passes client validation");
            Check((string)Call(app, "F028ValidateDeckDraft", new string[0], 2) == "",
                "an empty draft may be saved");
            Check((string)Call(app, "F028ValidateDeckDraft", deck.Take(3).ToArray(), 2) == "",
                "an incomplete owned draft may be saved");
            Check((string)Call(app, "F024ValidateOwnedDeck", deck.Take(3).ToArray(), 2) == "卡组必须恰好 30 张",
                "an incomplete draft remains invalid for a match");

            definitions[0].Playable = false;
            Check((string)Call(app, "F024ValidateOwnedDeck", deck, 2) == "收藏中的未实现卡牌不能加入当前对战卡组",
                "an unsupported collection card is rejected from an active deck");
            definitions[0].Playable = true;

            Set(app, "f024Account", Snapshot("account-A", 2,
                definitions.Skip(1).Select(card => new F024AccountCard { card_id = card.CardId, normal = 1 }).ToArray()));
            Check((string)Call(app, "F024ValidateOwnedDeck", deck, 2) == "卡组使用了未拥有的卡牌",
                "a supported but unowned card is rejected from an active deck");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
