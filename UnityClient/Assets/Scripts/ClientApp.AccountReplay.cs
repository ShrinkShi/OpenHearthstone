using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class ClientApp
{
    [Serializable] sealed class F024ReplayRequestRecord
    {
        public string request_id, operation, error;
        public bool completed, ok, expected_failure;
        public int revision, opened_card_count;
    }

    [Serializable] sealed class F024ReplayGuiInputRecord
    {
        public string target, coordinate_target, expected_event_type, current_event_type, event_type_after, input_context, handler, request_id;
        public string raw_event_type, control_event_type, control_event_type_after, control_raw_event_type;
        public string queue_api, coordinate_space, event_marker, observed_event_marker, queue_error, event_match;
        public string submitted_event_type, submitted_raw_event_type;
        public string editor_dispatch_schedule;
        public string candidate_event_type, candidate_raw_event_type, candidate_event_marker;
        public string scroll_event_before, scroll_event_after;
        public bool event_consumed, control_contains_mouse, control_enabled;
        public bool scroll_before_contains_mouse, scroll_after_contains_mouse;
        public bool native_event_queued, native_event_observed, event_marker_matches, root_pointer_matches;
        public bool editor_send_event_invoked, editor_send_event_returned;
        public bool editor_send_event_scheduled;
        public bool editor_input_scheduled, editor_input_dispatch_invoked;
        public bool editor_game_input_queue_invoked, editor_game_input_queue_returned;
        public int control_id, frame, queue_frame, candidate_frame, current_event_button, hot_control_before, hot_control_after;
        public int control_hot_before, control_hot_after;
        public int editor_dispatch_frame;
        public int editor_target_display;
        public float screen_x, screen_y, gui_x, gui_y, design_x, design_y;
        public float queued_mouse_x, queued_mouse_y, current_mouse_x, current_mouse_y, pointer_delta_x, pointer_delta_y;
        public float candidate_mouse_x, candidate_mouse_y;
        public float rect_x, rect_y, rect_width, rect_height;
        public float control_mouse_x, control_mouse_y, control_rect_x, control_rect_y, control_rect_width, control_rect_height;
        public float scroll_x_before, scroll_y_before, scroll_mouse_x_before, scroll_mouse_y_before;
        public float scroll_x_after, scroll_y_after, scroll_mouse_x_after, scroll_mouse_y_after;
        public float scroll_screen_x_after, scroll_screen_y_after;
        public float player_screen_width, player_screen_height;
        public float game_view_target_width, game_view_target_height, game_view_mouse_scale;
        public float game_view_rt_width, game_view_rt_height;
        public float game_view_mouse_offset_x, game_view_mouse_offset_y, game_view_event_mouse_x, game_view_event_mouse_y;
        public float game_view_view_x, game_view_view_y, game_view_view_width, game_view_view_height;
        public float game_view_target_x, game_view_target_y, game_view_target_view_width, game_view_target_view_height;
    }

    [Serializable] sealed class F024ReplayReport
    {
        public string started_utc, completed_utc, result, error, input_mode;
        public List<string> steps = new List<string>();
        public List<string> screenshots = new List<string>();
        public List<F024ReplayRequestRecord> requests = new List<F024ReplayRequestRecord>();
        public List<F024ReplayGuiInputRecord> gui_inputs = new List<F024ReplayGuiInputRecord>();
    }

    [Serializable] sealed class F024ReplayResponse
    {
        public string request_id, operation, error, open_id, deck_id;
        public bool ok;
        public int revision;
        public F024OpenedCard[] opened_cards;
    }

    [Serializable] sealed class F024ReplayHeader
    {
        public string type, request_id, operation, message;
    }

    F024ReplayReport f024ReplayReport;
    readonly Dictionary<string, F024ReplayResponse> f024ReplayResponses = new Dictionary<string, F024ReplayResponse>();
    Connection f024ReplayPeerConnection;
    string f024LastAccountRequestId = "", f024ReplayRawResponse = "", f024ReplayFailure = "", f024ReplayUsername = "", f024ReplayPassword = "", f024ReplayPeerMatchId = "";
    bool f024AccountReplayActive;

    void F024AccountReplayObserveRequest(string requestId, string operation)
    {
        if (!f024AccountReplayActive || f024ReplayReport == null) return;
        F024ReplayGuiAttachRequest(requestId);
        if (operation != "account_resume" && operation != "account_state") f024LastAccountRequestId = requestId ?? "";
        if (f024ReplayReport.requests.Any(item => item.request_id == requestId)) return;
        f024ReplayReport.requests.Add(new F024ReplayRequestRecord { request_id = requestId, operation = operation });
    }

    void F024AccountReplayObserveResponse(F024AccountResponse response)
    {
        if (response == null || !f024AccountReplayActive || f024ReplayReport == null) return;
        var safe = new F024ReplayResponse
        {
            request_id = response.request_id,
            operation = response.operation,
            error = response.error,
            open_id = response.open_id,
            deck_id = response.deck_id,
            ok = response.ok,
            revision = response.snapshot == null ? 0 : response.snapshot.revision,
            opened_cards = response.opened_cards
        };
        if (!String.IsNullOrEmpty(safe.request_id)) f024ReplayResponses[safe.request_id] = safe;
        F024AccountReplayRecordResponse(safe);
    }

    void F024AccountReplayRecordResponse(F024ReplayResponse response)
    {
        if (response == null || f024ReplayReport == null) return;
        var item = f024ReplayReport.requests.FirstOrDefault(entry => entry.request_id == response.request_id);
        if (item == null) return;
        item.completed = true;
        item.ok = response.ok;
        item.error = response.error ?? "";
        item.revision = response.revision;
        item.opened_card_count = response.opened_cards == null ? 0 : response.opened_cards.Length;
    }

    IEnumerator RunF024AccountReplay()
    {
        bool editorGuiReplayRequested = Flag("--account-editor-gui-replay");
        bool editorNativeInputRequested = Flag("--account-editor-native-input");
        bool editorInputRequested = editorGuiReplayRequested || editorNativeInputRequested;
#if UNITY_EDITOR
        f024AccountEditorGuiReplayMode = editorInputRequested;
        f024AccountEditorNativeInputMode = editorNativeInputRequested;
#else
        f024AccountEditorGuiReplayMode = false;
        f024AccountEditorNativeInputMode = false;
#endif
        f024AccountGuiReplayMode = Flag("--account-gui-replay") || f024AccountEditorGuiReplayMode;
        f024ReplayReport = new F024ReplayReport
        {
            started_utc = DateTime.UtcNow.ToString("o"),
            result = "running",
            input_mode = editorNativeInputRequested ? "editor-game-input-queue" :
                (editorGuiReplayRequested ? "editor-game-view-send-event" :
                    (f024AccountGuiReplayMode ? "unity-event-queue/native-OnGUI" : "direct-production-handler"))
        };
        f024AccountReplayActive = true;
        f024ReplayFailure = "";
        f024ReplayGuiTargets.Clear();
        f024ReplayGuiCurrent = null;
        f024ReplayExpectedHotControl = 0;
        f024ReplayGuiSequence = 0;
        f024ReplayGuiHasLastPosition = false;
        f024ReplayLastGuiHandler = "";
        if (editorGuiReplayRequested && editorNativeInputRequested)
        {
            F024FinishAccountReplay(false, "Editor GUI replay 只能选择 --account-editor-gui-replay 或 --account-editor-native-input 其中一个");
            yield break;
        }
#if !UNITY_EDITOR
        if (editorInputRequested)
        {
            F024FinishAccountReplay(false, "Editor GUI replay 参数仅支持 Unity Editor Play Mode");
            yield break;
        }
#endif
        Screen.SetResolution(1920, 1080, false);
        float resolutionStarted = Time.realtimeSinceStartup;
        while ((Screen.width != 1920 || Screen.height != 1080) &&
               Time.realtimeSinceStartup - resolutionStarted < 15f)
            yield return null;
        if (Screen.width != 1920 || Screen.height != 1080)
        {
            F024FinishAccountReplay(false,
                "回放窗口无法设置为1920x1080，当前 " + Screen.width + "x" + Screen.height);
            yield break;
        }
        if (!String.IsNullOrEmpty(f024Token))
        {
            F024FinishAccountReplay(false, "请为 --account-replay 使用没有已保存会话的独立 --profile");
            yield break;
        }

        f024GatewayPort = port.ToString();
        f024Username = "f024r" + Guid.NewGuid().ToString("N").Substring(0, 12);
        f024ReplayUsername = f024Username;
        string primaryPassword = "R" + Guid.NewGuid().ToString("N") + "!";
        f024ReplayPassword = primaryPassword;
        string requestId = "";
        F024ReplayResponse response = null;
        f024DisplayName = "账号回放";
        f024Password = primaryPassword;
        f024RegisterMode = true;
        page = "account";
        if (F024ReplayGuiEnabled)
        {
            page = "menu";
            yield return F025ReplayGuiOpenAccountSettings();
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayCapture("account-replay-settings-register");
            yield return F024ReplayGuiClick("account.register", "F024Register");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024Register();
        primaryPassword = null;
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        if (!F024ReplayResponseSucceeded(response) || !F024HasAccount)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "注册账号失败"));
            yield break;
        }
        f024ReplayReport.steps.Add("注册账号并收到服务器初始收藏、卡组与钱包快照");
        yield return F024ReplayCapture("account-replay-registered");

        if (F024ReplayGuiEnabled)
        {
            yield return F024ReplayGuiNavigate("menu.quests", "F024OpenPage(quests)", "quests");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024OpenPage("quests");
        requestId = F024ReplayLatestRequestId("account_state");
        if (String.IsNullOrEmpty(requestId))
        {
            F024FinishAccountReplay(false, "打开任务页没有请求服务器账号快照");
            yield break;
        }
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        if (!F024ReplayResponseSucceeded(response) || !F024ReplayQuestSnapshotMatches())
        {
            F024FinishAccountReplay(false, F024ReplayError(response,
                "任务快照未包含4个日常和4个周常，或金币奖励档位不符"));
            yield break;
        }
        f024ReplayReport.steps.Add(F024ReplayGuiEnabled
            ? "通过任务入口读取并显示4个日常（40/60/80金币）与4个周常（80/120/160金币）任务"
            : "通过F024OpenPage(quests)读取4个日常（40/60/80金币）与4个周常（80/120/160金币）任务");
        yield return F024ReplayCapture("account-replay-quests");
        var rerollQuest = (f024Account.quests ?? new F024AccountQuest[0])
            .FirstOrDefault(item => item != null && item.period == "daily" && item.reroll_available);
        if (rerollQuest == null)
        {
            F024FinishAccountReplay(false, "新账号没有可供实际重抽的每日任务");
            yield break;
        }
        int questGoldBefore = f024Account.gold;
        yield return F026ReplayAction("quests/reroll:" + rerollQuest.quest_id, "RerollQuest",
            () => F024RequestQuestReroll(rerollQuest.quest_id));
        yield return F024ReplayWaitForSettled(20);
        var rerolledQuest = (f024Account.quests ?? new F024AccountQuest[0])
            .FirstOrDefault(item => item != null && item.quest_id == rerollQuest.quest_id);
        if (!String.IsNullOrEmpty(f024ReplayFailure) || rerolledQuest == null || rerolledQuest.reroll_available ||
            rerolledQuest.description == rerollQuest.description || f024Account.gold != questGoldBefore || !F024ReplayQuestSnapshotMatches())
        {
            F024FinishAccountReplay(false, "任务重抽图标未完成权威替换，或意外改变金币/四槽结构");
            yield break;
        }
        f024ReplayReport.steps.Add("任务图标重抽实际改变服务器任务，保留4+4槽和钱包");
        yield return F024ReplayCapture("account-replay-quests-rerolled");
        if (F024ReplayGuiEnabled)
        {
            yield return F024ReplayGuiClick("page.back", "ReturnToMenuAndRefreshAccount");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayWaitForSettled(20);
        }
        else page = "menu";
        yield return F024ReplayCapture("account-replay-menu");
        yield return F026ReplayHubControls();
        if (!String.IsNullOrEmpty(f024ReplayFailure))
        {
            F024FinishAccountReplay(false, f024ReplayFailure);
            yield break;
        }

        var shopItem = ((f024Account.shop ?? new F024AccountShopItem[0]).Where(item => item != null && item.price == 100)
            .OrderBy(item => item.set_id).FirstOrDefault());
        if (shopItem == null)
        {
            F024FinishAccountReplay(false, "服务器没有100金币扩展卡包商品");
            yield break;
        }

        int selectedPackSet = shopItem.set_id;
        if (F024ReplayGuiEnabled)
        {
            F024ReplayPrepareShopViewport(selectedPackSet);
            yield return F024ReplayGuiClick("menu.shop", "F024OpenPage(shop)");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayWaitForSettled(20);
        }
        else page = "shop";
        yield return F024ReplayCapture("account-replay-shop");
        f024ShopScroll = Vector2.zero;
        yield return F024ReplayCapture("account-replay-shop-top");
        f024ShopScroll = new Vector2(0, float.MaxValue);
        yield return F024ReplayCapture("account-replay-shop-bottom");
        f024ShopScroll = new Vector2(0, f024ShopScroll.y * .5f);
        yield return F024ReplayCapture("account-replay-shop-middle");
        F024ReplayPrepareShopViewport(selectedPackSet);
        yield return null;
        yield return null;
        int initialGold = f024Account.gold;
        int initialPackCount = F024ReplayPackCount(selectedPackSet);
        if (F024ReplayGuiEnabled)
        {
            yield return F026ReplayGuiBuy(selectedPackSet, 2, true);
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024RequestShopBuy(selectedPackSet, 2);
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        if (!F024ReplayResponseSucceeded(response) || f024Account.gold != initialGold - 200 ||
            F024ReplayPackCount(selectedPackSet) != initialPackCount + 2)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "一次购买两包、扣除200金币失败"));
            yield break;
        }
        yield return F024ReplayCapture("account-replay-shop-purchased");
        f024ReplayReport.steps.Add("一次权威购买请求扣除200金币并增加2包库存；GUI取消弹窗不扣款");
        var firstPackResult = false;
        int openedPacks = 0;
        var allOpened = new List<F024OpenedCard>();
        CardDef craftCandidate = null;
        while (openedPacks < 5 && craftCandidate == null)
        {
            if (openedPacks > 0 && F024ReplayPackCount(selectedPackSet) == 0)
            {
                if (f024Account.gold < 100)
                {
                    F024FinishAccountReplay(false, "余额不足，无法为合成回放再购买卡包");
                    yield break;
                }
                int goldBeforePurchase = f024Account.gold;
                int packsBeforePurchase = F024ReplayPackCount(selectedPackSet);
                if (F024ReplayGuiEnabled)
                {
                    F024ReplayPrepareShopViewport(selectedPackSet);
                    yield return F024ReplayGuiNavigate("menu.shop", "F024OpenPage(shop)", "shop");
                    if (!String.IsNullOrEmpty(f024ReplayFailure))
                    {
                        F024FinishAccountReplay(false, f024ReplayFailure);
                        yield break;
                    }
                    yield return F026ReplayGuiBuy(selectedPackSet, 1);
                }
                else F024RequestShopBuy(selectedPackSet, 1);
                if (!String.IsNullOrEmpty(f024ReplayFailure))
                {
                    F024FinishAccountReplay(false, f024ReplayFailure);
                    yield break;
                }
                requestId = f024LastAccountRequestId;
                yield return F024ReplayWaitForResponse(requestId, 30);
                response = F024ReplayTakeResponse(requestId);
                yield return F024ReplayWaitForSettled(20);
                if (!F024ReplayResponseSucceeded(response) || f024Account.gold != goldBeforePurchase - 100 ||
                    F024ReplayPackCount(selectedPackSet) != packsBeforePurchase + 1)
                {
                    F024FinishAccountReplay(false, F024ReplayError(response, "购买扩展卡包失败"));
                    yield break;
                }
            }

            if (F024ReplayGuiEnabled)
            {
                F024ReplayPreparePackViewport(selectedPackSet);
                yield return F024ReplayGuiNavigate("menu.packs", "F024OpenPage(packs)", "packs");
                if (!String.IsNullOrEmpty(f024ReplayFailure))
                {
                    F024FinishAccountReplay(false, f024ReplayFailure);
                    yield break;
                }
            }
            else page = "packs";

            if (openedPacks == 0)
            {
                if (f024PackRevealActive || F024ReplayPackCount(selectedPackSet) <= 0)
                {
                    F024FinishAccountReplay(false, "购买后的卡包库存页状态不正确，无法截图");
                    yield break;
                }
                yield return F024ReplayCapture("account-replay-packs-inventory");
            }

            if (F024ReplayGuiEnabled && openedPacks == 0)
            {
                int openedRequestsBefore = f024ReplayReport.requests.Count(item => item.operation == "account_pack_open");
                int ownedPacksBefore = F024ReplayPackCount(selectedPackSet);
                yield return F024ReplayGuiPackDrag(selectedPackSet, false);
                if (!String.IsNullOrEmpty(f024ReplayFailure) ||
                    f024ReplayReport.requests.Count(item => item.operation == "account_pack_open") != openedRequestsBefore ||
                    F024ReplayPackCount(selectedPackSet) != ownedPacksBefore || f024AccountBusy)
                {
                    F024FinishAccountReplay(false, String.IsNullOrEmpty(f024ReplayFailure)
                        ? "槽外释放卡包错误地触发了开包" : f024ReplayFailure);
                    yield break;
                }
                f024ReplayReport.steps.Add("卡包在槽外松开时没有触发开包请求");
            }

            int copiesBeforeOpen = F024ReplayTotalOwnedCopies();
            if (F024ReplayGuiEnabled)
            {
                int openedRequestsBefore = f024ReplayReport.requests.Count(item => item.operation == "account_pack_open");
                yield return F024ReplayGuiPackDrag(selectedPackSet, true);
                if (!String.IsNullOrEmpty(f024ReplayFailure))
                {
                    F024FinishAccountReplay(false, f024ReplayFailure);
                    yield break;
                }
                if (f024ReplayReport.requests.Count(item => item.operation == "account_pack_open") != openedRequestsBefore + 1)
                {
                    F024FinishAccountReplay(false, "槽内松开卡包没有恰好触发一次开包请求");
                    yield break;
                }
            }
            else F024OpenPackFromDrag(selectedPackSet);
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            if (!F024ReplayResponseSucceeded(response) || response.opened_cards == null || response.opened_cards.Length != 5)
            {
                F024FinishAccountReplay(false, F024ReplayError(response, "服务器没有返回恰好5张开包卡牌"));
                yield break;
            }
            if (F024ReplayTotalOwnedCopies() != copiesBeforeOpen + 5 || !f024PackRevealActive)
            {
                F024FinishAccountReplay(false, "开包结果没有同步进收藏或翻牌界面");
                yield break;
            }
            if (!firstPackResult)
            {
                f024ReplayReport.steps.Add(F024ReplayGuiEnabled
                    ? "从商店购买100金币卡包，拖入开包槽并确认5张卡牌已进入服务器收藏"
                    : "从商店购买100金币卡包，提交开包请求并确认5张卡牌已进入服务器收藏");
                firstPackResult = true;
            }
            openedPacks++;
            allOpened.AddRange(response.opened_cards);
            yield return F024ReplayCapture("account-replay-pack-" + openedPacks + "-ready");
            for (int cardIndex = 0; cardIndex < 5; cardIndex++)
            {
                if (F024ReplayGuiEnabled)
                {
                    yield return F024ReplayGuiClick("packs.reveal", "F024AdvancePackReveal");
                    if (!String.IsNullOrEmpty(f024ReplayFailure) || f024RevealIndex != cardIndex)
                    {
                        F024FinishAccountReplay(false, String.IsNullOrEmpty(f024ReplayFailure)
                            ? "卡牌翻开按钮没有推进到预期位置" : f024ReplayFailure);
                        yield break;
                    }
                }
                else F024AdvancePackReveal();
                yield return F024ReplayCapture("account-replay-pack-" + openedPacks + "-card-" + (cardIndex + 1));
            }
            if (F024ReplayGuiEnabled)
            {
                yield return F024ReplayGuiClick("packs.reveal", "F024AdvancePackReveal");
                if (!String.IsNullOrEmpty(f024ReplayFailure) || f024PackRevealActive)
                {
                    F024FinishAccountReplay(false, String.IsNullOrEmpty(f024ReplayFailure)
                        ? "完成按钮没有关闭开包揭晓" : f024ReplayFailure);
                    yield break;
                }
            }
            else F024AdvancePackReveal();
            yield return F024ReplayWaitForSettled(20);
            if (openedPacks == 1)
            {
                if (F024ReplayGuiEnabled)
                {
                    yield return F024ReplayGuiNavigate("menu.collection", "F024OpenPage(collection)", "collection");
                    if (!String.IsNullOrEmpty(f024ReplayFailure))
                    {
                        F024FinishAccountReplay(false, f024ReplayFailure);
                        yield break;
                    }
                    yield return F024ReplayCapture("account-replay-collection-overview");
                    yield return F024ReplayGuiVerifyCollectionFilters();
                    if (!String.IsNullOrEmpty(f024ReplayFailure))
                    {
                        F024FinishAccountReplay(false, f024ReplayFailure);
                        yield break;
                    }
                    yield return F024ReplayGuiSelectCollectionCard(response.opened_cards[0].card_id);
                    if (!String.IsNullOrEmpty(f024ReplayFailure))
                    {
                        F024FinishAccountReplay(false, f024ReplayFailure);
                        yield break;
                    }
                }
                else
                {
                    f024CollectionTab = 0;
                    f025CollectionDetailOpen = false;
                    page = "collection";
                    yield return F024ReplayCapture("account-replay-collection-overview");
                    f024SelectedCardId = response.opened_cards[0].card_id;
                    f025CollectionDetailOpen = true;
                }
                yield return F024ReplayCapture("account-replay-collection-updated");
                if (F024ReplayGuiEnabled)
                {
                    string detailCard = f024SelectedCardId;
                    yield return F025ReplayGuiCloseOverlays();
                    if (!String.IsNullOrEmpty(f024ReplayFailure) || f025CollectionDetailOpen ||
                        page != "collection" || f024SelectedCardId != detailCard)
                    {
                        F024FinishAccountReplay(false, String.IsNullOrEmpty(f024ReplayFailure)
                            ? "关闭卡牌详情未保留收藏页与选中卡牌" : f024ReplayFailure);
                        yield break;
                    }
                    yield return F024ReplayCapture("account-replay-collection-detail-closed");
                }
            }

            craftCandidate = allOpened.Select(item => item == null || !cards.ContainsKey(item.card_id ?? "") ? null : cards[item.card_id])
                .Where(card => F024CanCraftOrDisenchant(card))
                .OrderBy(card => F024CraftDust(card, false)).FirstOrDefault();
            if (craftCandidate == null) continue;
            foreach (var opened in allOpened)
            {
                if (opened == null || f024Account.dust >= F024CraftDust(craftCandidate, false)) break;
                CardDef definition;
                if (!cards.TryGetValue(opened.card_id ?? "", out definition) || !F024CanCraftOrDisenchant(definition)) continue;
                var inventory = F024FindAccountCard(opened.card_id);
                if (inventory == null || (opened.golden ? inventory.golden : inventory.normal) <= 0) continue;
                int dustBefore = f024Account.dust;
                if (F024ReplayGuiEnabled)
                {
                    if (page != "collection")
                    {
                        yield return F024ReplayGuiNavigate("menu.collection", "F024OpenPage(collection)", "collection");
                        if (!String.IsNullOrEmpty(f024ReplayFailure))
                        {
                            F024FinishAccountReplay(false, f024ReplayFailure);
                            yield break;
                        }
                    }
                    yield return F024ReplayGuiSelectCollectionCard(opened.card_id, 1);
                    f024GoldenCraft = opened.golden;
                    yield return F024ReplayGuiClick("collection.disenchant", "F024RequestDisenchant");
                    if (!String.IsNullOrEmpty(f024ReplayFailure))
                    {
                        F024FinishAccountReplay(false, f024ReplayFailure);
                        yield break;
                    }
                }
                else F024RequestDisenchant(opened.card_id, opened.golden);
                requestId = f024LastAccountRequestId;
                yield return F024ReplayWaitForResponse(requestId, 30);
                response = F024ReplayTakeResponse(requestId);
                yield return F024ReplayWaitForSettled(20);
                if (!F024ReplayResponseSucceeded(response) || f024Account.dust <= dustBefore)
                {
                    F024FinishAccountReplay(false, F024ReplayError(response, "分解开包卡牌未增加奥尘"));
                    yield break;
                }
            }
            if (f024Account.dust < F024CraftDust(craftCandidate, false)) craftCandidate = null;
        }
        if (craftCandidate == null || f024Account.dust < F024CraftDust(craftCandidate, false))
        {
            F024FinishAccountReplay(false, "五包以内未获得可用于回放制作的卡牌与奥尘");
            yield break;
        }
        f024ReplayReport.steps.Add("逐张翻开服务器卡包结果，并在收藏中核对新卡数量");
        if (F024ReplayGuiEnabled)
        {
            if (page != "collection")
            {
                yield return F024ReplayGuiNavigate("menu.collection", "F024OpenPage(collection)", "collection");
                if (!String.IsNullOrEmpty(f024ReplayFailure))
                {
                    F024FinishAccountReplay(false, f024ReplayFailure);
                    yield break;
                }
            }
            yield return F024ReplayGuiSelectCollectionCard(craftCandidate.CardId, 0);
            f024GoldenCraft = false;
        }
        else
        {
            f024CollectionTab = 0;
            f024SelectedCardId = craftCandidate.CardId;
            f025CollectionDetailOpen = true;
            page = "collection";
        }
        int dustBeforeCraft = f024Account.dust;
        int ownedBeforeCraft = F024OwnedCardCount(craftCandidate.CardId);
        if (F024ReplayGuiEnabled)
        {
            yield return F024ReplayGuiClick("collection.craft", "F024RequestCraft");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024RequestCraft(craftCandidate.CardId, false);
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        if (!F024ReplayResponseSucceeded(response) || f024Account.dust >= dustBeforeCraft || F024OwnedCardCount(craftCandidate.CardId) <= ownedBeforeCraft)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "制作卡牌没有改变奥尘或收藏数量"));
            yield break;
        }
        yield return F024ReplayCapture("account-replay-crafted");

        int dustBeforeDisenchant = f024Account.dust;
        int ownedBeforeDisenchant = F024OwnedCardCount(craftCandidate.CardId);
        if (F024ReplayGuiEnabled)
        {
            yield return F024ReplayGuiClick("collection.disenchant", "F024RequestDisenchant");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024RequestDisenchant(craftCandidate.CardId, false);
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        if (!F024ReplayResponseSucceeded(response) || f024Account.dust <= dustBeforeDisenchant || F024OwnedCardCount(craftCandidate.CardId) >= ownedBeforeDisenchant)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "分解制作卡牌没有改变奥尘或收藏数量"));
            yield break;
        }
        f024ReplayReport.steps.Add("制作与分解均通过账号服务并更新收藏和奥尘");
        yield return F024ReplayCapture("account-replay-disenchanted");

        var starterDeck = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null);
        if (starterDeck == null)
        {
            F024FinishAccountReplay(false, "注册快照没有可保存的服务器卡组");
            yield break;
        }
        string savedDeckId = starterDeck.deck_id;
        string savedDeckName = (starterDeck.name ?? "卡组");
        if (savedDeckName.Length > 24) savedDeckName = savedDeckName.Substring(0, 24);
        savedDeckName += " Replay";
        if (!F024ReplayGuiEnabled) f025CollectionDetailOpen = false;
        if (F024ReplayGuiEnabled)
        {
            yield return F025ReplayGuiCloseOverlays();
            if (f024CollectionTab != 1)
            {
                yield return F024ReplayGuiClick("collection.deck:" + savedDeckId, "EditSelectedCollectionDeck");
                if (!String.IsNullOrEmpty(f024ReplayFailure))
                {
                    F024FinishAccountReplay(false, f024ReplayFailure);
                    yield break;
                }
            }
            f024DraftName = savedDeckName;
            f024DraftDirty = true;
            // 通过不存在的编号验证服务端拒绝，随后恢复原编号重试。
            f024DraftId = "f029-replay-missing-deck";
            yield return F024ReplayGuiClick("collection.crafting-mode", "ToggleCollectionCraftingMode");
            yield return F024ReplayGuiClick("collection.filter.ownership.2", "ChangeCollectionOwnershipFilter");
            yield return F024ReplayGuiClick("collection.deck-complete", "F024RequestDeckSave");
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            if (!String.IsNullOrEmpty(f024ReplayFailure) || response == null || response.ok ||
                response.error != "卡组不存在" || f024CollectionTab != 1 || !f024DraftDirty ||
                f024DraftName != savedDeckName || !f029CraftingMode || f024CollectionOwnedMode != 2)
            {
                F024FinishAccountReplay(false, "保存失败后没有保留编辑状态和草稿");
                yield break;
            }
            var rejectedSave = f024ReplayReport.requests.First(item => item.request_id == requestId);
            rejectedSave.expected_failure = true;
            f024ReplayReport.steps.Add("保存失败后保留卡组草稿及编辑页面");
            f024DraftId = savedDeckId;
            yield return F024ReplayGuiClick("collection.deck-complete", "F024RequestDeckSave");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else
        {
            f024DraftId = savedDeckId;
            f024DraftName = savedDeckName;
            f024DraftClassId = starterDeck.class_id;
            f024DraftCards.Clear();
            f024DraftCards.AddRange(starterDeck.cards ?? new string[0]);
            f024DraftDirty = true;
            F024RequestDeckSave(savedDeckId, savedDeckName, starterDeck.class_id, starterDeck.cards);
        }
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        var savedDeck = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null && item.deck_id == savedDeckId);
        if (!F024ReplayResponseSucceeded(response) || savedDeck == null || savedDeck.name != savedDeckName)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "服务器卡组保存结果未反映到快照"));
            yield break;
        }
        f024ReplayReport.steps.Add("保存服务器卡组并确认服务端返回的deck_id与新名称");
        yield return F024ReplayCapture("account-replay-deck-saved");

        if (F024ReplayGuiEnabled)
        {
            if (f024CollectionTab != 0 || f029CraftingMode || f024CollectionOwnedMode != 0)
            {
                F024FinishAccountReplay(false, "保存成功后没有回到卡册");
                yield break;
            }
            yield return F024ReplayGuiClick("collection.deck:" + savedDeckId, "EditSelectedCollectionDeck");
            int cardsBeforeCraftingMode = f024DraftCards.Count;
            yield return F024ReplayGuiClick("collection.crafting-mode", "ToggleCollectionCraftingMode");
            if (String.IsNullOrEmpty(f024ReplayFailure) && !f029CraftingMode)
                f024ReplayFailure = "编辑套牌时无法进入卡牌制作";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.filter.ownership.2", "ChangeCollectionOwnershipFilter");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f024CollectionOwnedMode != 2)
                f024ReplayFailure = "编辑套牌时无法筛选未拥有卡牌";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.crafting-mode", "ToggleCollectionCraftingMode");
            if (String.IsNullOrEmpty(f024ReplayFailure) &&
                (f029CraftingMode || f024CollectionOwnedMode != 0 || f024DraftCards.Count != cardsBeforeCraftingMode))
                f024ReplayFailure = "退出制作后套牌或筛选状态异常";
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayGuiClick("collection.deck-code", "CopyDraftDeckCode");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            if (!F024ReplayResponseSucceeded(response) || String.IsNullOrEmpty(f028ReplayClipboard))
            {
                F024FinishAccountReplay(false, F024ReplayError(response, "复制卡组代码失败"));
                yield break;
            }
            yield return F024ReplayGuiClick("collection.deck-format", "ChangeDraftDeckFormat");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f028DraftFormat != "standard")
                f024ReplayFailure = "卡组没有切换为标准格式";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.deck-format", "ChangeDraftDeckFormat");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f028DraftFormat != "wild")
                f024ReplayFailure = "卡组没有切换回狂野格式";
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            string cardId = f024DraftCards.Distinct().OrderBy(id => cards.ContainsKey(id) ? cards[id].Cost : 99).First();
            int initialCount = f024DraftCards.Count(id => id == cardId);
            f024CollectionSearch = cardId;
            f024CollectionSet = f024CollectionMana = -1;
            f024CollectionOwnedMode = 0;
            f024CollectionPage = 0;
            yield return F024ReplayGuiClick("collection.deck-card:" + cardId, "RemoveDraftCard");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f024DraftCards.Count(id => id == cardId) != initialCount - 1)
                f024ReplayFailure = "点击套牌条目没有移除卡牌";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.card:" + cardId, "AddDraftCard");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f024DraftCards.Count(id => id == cardId) != initialCount)
                f024ReplayFailure = "点击收藏没有加入卡牌";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F028ReplayCardDrag("collection.deck-card:" + cardId, "collection.book-drop", "RemoveDraftCard");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f024DraftCards.Count(id => id == cardId) != initialCount - 1)
                f024ReplayFailure = "拖出套牌没有移除卡牌";
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F028ReplayCardDrag("collection.card:" + cardId, "collection.deck-drop", "AddDraftCard");
            if (String.IsNullOrEmpty(f024ReplayFailure) && f024DraftCards.Count(id => id == cardId) != initialCount)
                f024ReplayFailure = "拖入套牌没有加入卡牌";
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayGuiClick("collection.deck-complete", "F024RequestDeckSave");
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            if (!F024ReplayResponseSucceeded(response) || f024CollectionTab != 0 ||
                f029CraftingMode || f024CollectionOwnedMode != 0)
                f024ReplayFailure = F024ReplayError(response, "完成套牌后没有回到卡册");
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.deck-new", "CreateCollectionDeck");
            if (String.IsNullOrEmpty(f024ReplayFailure) && (f024DraftId != "" || f024DraftCards.Count != 0 || !f024DraftDirty))
                f024ReplayFailure = "新建套牌没有创建空草稿";
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            if (!f028OfferClipboardImport)
            {
                F024FinishAccountReplay(false, "新建卡组时没有提示导入测试剪贴板代码");
                yield break;
            }
            yield return F024ReplayCapture("account-replay-deck-import-offer");
            yield return F024ReplayGuiClick("collection.deck-import", "ImportDeckCode");
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            if (!F024ReplayResponseSucceeded(response) || f024DraftClassId != savedDeck.class_id ||
                !f024DraftCards.OrderBy(id => id).SequenceEqual(savedDeck.cards.OrderBy(id => id)))
            {
                F024FinishAccountReplay(false, F024ReplayError(response, "导入卡组代码没有恢复职业和卡牌"));
                yield break;
            }
            f028ReplayClipboard = "";
            f024ReplayReport.steps.Add("卡组代码复制、剪贴板提示及导入使用隔离测试剪贴板回放");
            // 先保存空草稿，再补齐并删除临时套牌。
            f024DraftName = "Replay temporary";
            f024DraftClassId = savedDeck.class_id;
            f024DraftCards.Clear();
            yield return F024ReplayGuiClick("collection.deck-complete", "F024RequestDeckSave");
            requestId = f024LastAccountRequestId;
            yield return F024ReplayWaitForResponse(requestId, 30);
            response = F024ReplayTakeResponse(requestId);
            yield return F024ReplayWaitForSettled(20);
            string temporaryId = response == null ? "" : response.deck_id;
            if (!F024ReplayResponseSucceeded(response) || String.IsNullOrEmpty(temporaryId) || temporaryId == savedDeckId)
                f024ReplayFailure = F024ReplayError(response, "新建套牌未由服务器保存");
            var emptyDeck = (f024Account.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null && item.deck_id == temporaryId);
            if (String.IsNullOrEmpty(f024ReplayFailure) && (emptyDeck == null || (emptyDeck.cards ?? new string[0]).Length != 0))
                f024ReplayFailure = "新建空套牌没有保存到服务器";
            if (String.IsNullOrEmpty(f024ReplayFailure))
            {
                yield return F024ReplayGuiClick("collection.deck:" + temporaryId, "EditSelectedCollectionDeck");
                f024DraftCards.AddRange(savedDeck.cards);
                f024DraftDirty = true;
                yield return F024ReplayGuiClick("collection.deck-complete", "F024RequestDeckSave");
                requestId = f024LastAccountRequestId;
                yield return F024ReplayWaitForResponse(requestId, 30);
                response = F024ReplayTakeResponse(requestId);
                yield return F024ReplayWaitForSettled(20);
                if (!F024ReplayResponseSucceeded(response) || response.deck_id != temporaryId)
                    f024ReplayFailure = F024ReplayError(response, "补齐套牌没有更新原草稿");
            }
            if (String.IsNullOrEmpty(f024ReplayFailure))
                yield return F024ReplayGuiClick("collection.deck-delete:" + temporaryId, "F024RequestDeckDelete");
            if (String.IsNullOrEmpty(f024ReplayFailure))
            {
                requestId = f024LastAccountRequestId;
                yield return F024ReplayWaitForResponse(requestId, 30);
                response = F024ReplayTakeResponse(requestId);
                yield return F024ReplayWaitForSettled(20);
                if (!F024ReplayResponseSucceeded(response) || (f024Account.decks ?? new F024AccountDeck[0]).Any(item => item != null && item.deck_id == temporaryId))
                    f024ReplayFailure = F024ReplayError(response, "删除套牌未反映到服务器快照");
            }
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            f024ReplayReport.steps.Add("收藏与套牌之间的点击和拖放、套牌入口及新建草稿均经过真实 GUI 输入");
        }

        int expectedGold = f024Account.gold;
        int expectedDust = f024Account.dust;
        int expectedOwnedCopies = F024ReplayTotalOwnedCopies();
        if (F024ReplayGuiEnabled)
        {
            yield return F025ReplayGuiOpenAccountSettings();
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayCapture("account-replay-settings-account");
            yield return F024ReplayGuiClick("account.logout", "F024Logout");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024Logout();
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        if (!F024ReplayResponseSucceeded(response) || F024HasAccount || !String.IsNullOrEmpty(f024Token))
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "账号退出未清空本地会话"));
            yield break;
        }
        F024FinishReplayLoginIdentity();
        f024Password = f024ReplayPassword;
        f024RegisterMode = false;
        if (F024ReplayGuiEnabled)
        {
            yield return F025ReplayGuiOpenAccountSettings();
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayGuiClick("account.login", "F024Login");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else F024Login();
        f024ReplayPassword = "";
        requestId = f024LastAccountRequestId;
        yield return F024ReplayWaitForResponse(requestId, 30);
        response = F024ReplayTakeResponse(requestId);
        yield return F024ReplayWaitForSettled(20);
        savedDeck = ((f024Account == null ? null : f024Account.decks) ?? new F024AccountDeck[0])
            .FirstOrDefault(item => item != null && item.deck_id == savedDeckId);
        if (!F024ReplayResponseSucceeded(response) || !F024HasAccount || f024Account.gold != expectedGold ||
            f024Account.dust != expectedDust || F024ReplayTotalOwnedCopies() != expectedOwnedCopies || savedDeck == null || savedDeck.name != savedDeckName)
        {
            F024FinishAccountReplay(false, F024ReplayError(response, "注销后重新登录未恢复金币、奥尘、收藏或卡组"));
            yield break;
        }
        f024ReplayReport.steps.Add("注销后重新登录，并核对钱包、收藏和卡组持久化");
        if (F024ReplayGuiEnabled)
        {
            yield return F025ReplayGuiCloseOverlays();
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        yield return F024ReplayCapture("account-replay-persisted");

        f024SelectedDeckId = savedDeckId;
        if (F024ReplayGuiEnabled)
        {
            yield return F024ReplayGuiNavigate("menu.traditional", "F024OpenPage(decks)", "decks");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayGuiWaitForDeck(savedDeckId);
            yield return F024ReplayGuiClick("decks.select:" + savedDeckId, "F024SelectServerDeck");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
            yield return F024ReplayGuiClick("decks.start", "Connect");
            if (!String.IsNullOrEmpty(f024ReplayFailure))
            {
                F024FinishAccountReplay(false, f024ReplayFailure);
                yield break;
            }
        }
        else
        {
            page = "decks";
            F024SelectServerDeck(savedDeckId);
            Connect();
        }
        yield return F024ReplayWaitForQueue(25);
        if (!String.IsNullOrEmpty(f024ReplayFailure))
        {
            F024FinishAccountReplay(false, f024ReplayFailure);
            yield break;
        }
        yield return F024ReplayJoinSecondAccount();
        if (!String.IsNullOrEmpty(f024ReplayFailure))
        {
            F024FinishAccountReplay(false, f024ReplayFailure);
            yield break;
        }
        yield return F024ReplayWaitForPrimaryBattle(30);
        if (!String.IsNullOrEmpty(f024ReplayFailure))
        {
            F024FinishAccountReplay(false, f024ReplayFailure);
            yield break;
        }
        f024ReplayReport.steps.Add("两个独立账号在同一对局队列中通过account_resume后以deck_id入场");
        yield return F024ReplayCapture("account-replay-two-account-match");
        primaryPassword = null;
        f024Password = "";
        F024FinishAccountReplay(true, "账号链路回放完成");
    }

    void F024FinishReplayLoginIdentity()
    {
        // The generated username is reconstructed from the registration request's private in-memory value.
        // The replay request log stores IDs and operation names only.
        f024Username = f024ReplayUsername;
    }

    IEnumerator F024ReplayWaitForResponse(string requestId, float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (!f024ReplayResponses.ContainsKey(requestId) && String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if (!f024ReplayResponses.ContainsKey(requestId) && String.IsNullOrEmpty(f024ReplayFailure))
            f024ReplayFailure = "等待账号服务响应超时：" + requestId;
    }

    IEnumerator F024ReplayWaitForSettled(float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while ((f024AccountBusy || f024SnapshotRefreshQueued) && String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if ((f024AccountBusy || f024SnapshotRefreshQueued) && String.IsNullOrEmpty(f024ReplayFailure))
            f024ReplayFailure = "等待账号快照刷新超时 (busy=" + f024AccountBusy + ", refreshQueued=" + f024SnapshotRefreshQueued + ")";
    }

    IEnumerator F024ReplayCapture(string name)
    {
        yield return Capture(name);
        if (f024ReplayReport != null)
        {
            string captureDirectory = String.IsNullOrEmpty(screenshotDir)
                ? Path.Combine(profilePath, "screenshots") : screenshotDir;
            f024ReplayReport.screenshots.Add(Path.Combine(captureDirectory, profile + "-" + name + ".png"));
        }
    }

    F024ReplayResponse F024ReplayTakeResponse(string requestId)
    {
        F024ReplayResponse response;
        if (!f024ReplayResponses.TryGetValue(requestId, out response)) return null;
        f024ReplayResponses.Remove(requestId);
        return response;
    }

    bool F024ReplayResponseSucceeded(F024ReplayResponse response)
    {
        return String.IsNullOrEmpty(f024ReplayFailure) && response != null && response.ok;
    }

    string F024ReplayError(F024ReplayResponse response, string fallback)
    {
        if (!String.IsNullOrEmpty(f024ReplayFailure)) return f024ReplayFailure;
        if (response != null && !String.IsNullOrEmpty(response.error)) return response.error;
        return fallback;
    }

    int F024ReplayTotalOwnedCopies()
    {
        return ((f024Account == null ? null : f024Account.cards) ?? new F024AccountCard[0])
            .Where(item => item != null).Sum(item => Mathf.Max(0, item.normal) + Mathf.Max(0, item.golden) + Mathf.Max(0, item.free));
    }

    int F024ReplayPackCount(int setId)
    {
        var item = ((f024Account == null ? null : f024Account.packs) ?? new F024AccountPack[0])
            .FirstOrDefault(pack => pack != null && pack.set_id == setId);
        return item == null ? 0 : item.count;
    }

    bool F024ReplayQuestSnapshotMatches()
    {
        var quests = ((f024Account == null ? null : f024Account.quests) ?? new F024AccountQuest[0])
            .Where(item => item != null).ToArray();
        var dailyRewards = quests
            .Where(item => String.Equals(item.period, "daily", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.reward_gold).OrderBy(value => value).ToArray();
        var weeklyRewards = quests
            .Where(item => String.Equals(item.period, "weekly", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.reward_gold).OrderBy(value => value).ToArray();
        return dailyRewards.Length == 4 && weeklyRewards.Length == 4 &&
            dailyRewards.All(value => value == 40 || value == 60 || value == 80) &&
            weeklyRewards.All(value => value == 80 || value == 120 || value == 160) &&
            dailyRewards.Distinct().Count() == 3 && weeklyRewards.Distinct().Count() == 3;
    }

    string F024ReplayLatestRequestId(string operation)
    {
        if (f024ReplayReport == null || f024ReplayReport.requests == null) return "";
        for (int i = f024ReplayReport.requests.Count - 1; i >= 0; i--)
        {
            var request = f024ReplayReport.requests[i];
            if (request != null && String.Equals(request.operation, operation, StringComparison.Ordinal))
                return request.request_id ?? "";
        }
        return "";
    }

    IEnumerator F024ReplayWaitRaw(Connection source, string expectedType, string requestId)
    {
        f024ReplayRawResponse = "";
        float started = Time.realtimeSinceStartup;
        while (String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < 30)
        {
            string line;
            while (source.Incoming.TryDequeue(out line))
            {
                F024ReplayHeader header;
                try { header = JsonUtility.FromJson<F024ReplayHeader>(line); }
                catch { continue; }
                if (header == null) continue;
                if (header.type == "disconnected")
                {
                    f024ReplayFailure = String.IsNullOrEmpty(header.message) ? "第二客户端连接已断开" : header.message;
                    yield break;
                }
                bool requestMatches = String.IsNullOrEmpty(requestId) || header.request_id == requestId;
                if (header.type == expectedType && requestMatches)
                {
                    f024ReplayRawResponse = line;
                    yield break;
                }
                if (header.type == "error" && requestMatches)
                {
                    f024ReplayRawResponse = line;
                    yield break;
                }
            }
            yield return null;
        }
        if (String.IsNullOrEmpty(f024ReplayFailure)) f024ReplayFailure = "等待第二客户端消息超时：" + expectedType;
    }

    IEnumerator F024ReplayJoinSecondAccount()
    {
        f024ReplayPeerConnection = new Connection();
        f024ReplayPeerConnection.Connect(host, port);
        yield return F024ReplayWaitRaw(f024ReplayPeerConnection, "hello", "");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        var hello = JsonUtility.FromJson<F024AccountHello>(f024ReplayRawResponse);
        if (hello == null || !(hello.capabilities ?? new string[0]).Contains("accounts_v1"))
        {
            f024ReplayFailure = "第二客户端服务器未启用accounts_v1";
            yield break;
        }

        string peerUsername = "f024r" + Guid.NewGuid().ToString("N").Substring(0, 12);
        string peerPassword = "R" + Guid.NewGuid().ToString("N") + "!";
        string requestId = F024NewRequestId();
        var register = new F024AccountRegisterRequest
        {
            request_id = requestId,
            username = peerUsername,
            password = peerPassword,
            display_name = "回放对手"
        };
        F024AccountReplayRecordExternalRequest(requestId, "account_register");
        f024ReplayPeerConnection.Send(JsonUtility.ToJson(register));
        register.password = "";
        peerPassword = "";
        yield return F024ReplayWaitRaw(f024ReplayPeerConnection, "account_result", requestId);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        var response = JsonUtility.FromJson<F024AccountResponse>(f024ReplayRawResponse);
        F024AccountReplayRecordExternalResponse(response);
        if (response == null || !response.ok || String.IsNullOrEmpty(response.token) || response.snapshot == null)
        {
            f024ReplayFailure = response == null || String.IsNullOrEmpty(response.error) ? "第二账号注册失败" : response.error;
            peerPassword = "";
            yield break;
        }
        var peerDeck = (response.snapshot.decks ?? new F024AccountDeck[0]).FirstOrDefault(item => item != null);
        string peerToken = response.token;
        if (peerDeck == null)
        {
            f024ReplayFailure = "第二账号没有服务器卡组";
            yield break;
        }

        requestId = F024NewRequestId();
        F024AccountReplayRecordExternalRequest(requestId, "account_resume");
        f024ReplayPeerConnection.Send(JsonUtility.ToJson(new F024AccountResumeRequest { request_id = requestId, token = peerToken }));
        yield return F024ReplayWaitRaw(f024ReplayPeerConnection, "account_result", requestId);
        peerToken = "";
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        response = JsonUtility.FromJson<F024AccountResponse>(f024ReplayRawResponse);
        F024AccountReplayRecordExternalResponse(response);
        if (response == null || !response.ok)
        {
            f024ReplayFailure = response == null || String.IsNullOrEmpty(response.error) ? "第二账号会话认证失败" : response.error;
            yield break;
        }

        f024ReplayPeerConnection.Send(JsonUtility.ToJson(new F024AuthenticatedJoinRequest { deck_id = peerDeck.deck_id }));
        yield return F024ReplayWaitRaw(f024ReplayPeerConnection, "state", "");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        var peerState = JsonUtility.FromJson<StateView>(f024ReplayRawResponse);
        f024ReplayPeerMatchId = peerState == null ? "" : peerState.match_id;
        if (peerState == null || peerState.type != "state" || String.IsNullOrEmpty(f024ReplayPeerMatchId))
            f024ReplayFailure = "第二客户端没有进入对局状态";
    }

    void F024AccountReplayRecordExternalRequest(string requestId, string operation)
    {
        if (f024ReplayReport == null) return;
        if (!f024ReplayReport.requests.Any(item => item.request_id == requestId))
            f024ReplayReport.requests.Add(new F024ReplayRequestRecord { request_id = requestId, operation = operation });
    }

    void F024AccountReplayRecordExternalResponse(F024AccountResponse response)
    {
        if (response == null) return;
        F024AccountReplayRecordResponse(new F024ReplayResponse
        {
            request_id = response.request_id,
            operation = response.operation,
            error = response.error,
            deck_id = response.deck_id,
            ok = response.ok,
            revision = response.snapshot == null ? 0 : response.snapshot.revision,
            opened_cards = response.opened_cards
        });
    }

    IEnumerator F024ReplayWaitForQueue(float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while (page != "battle" && !(page == "queue" && status == "等待另一位玩家加入……") &&
            !connectionLost && String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if (connectionLost && String.IsNullOrEmpty(f024ReplayFailure)) f024ReplayFailure = String.IsNullOrEmpty(status) ? "主客户端连接丢失" : status;
        else if (page != "battle" && !(page == "queue" && status == "等待另一位玩家加入……") && String.IsNullOrEmpty(f024ReplayFailure))
            f024ReplayFailure = String.IsNullOrEmpty(status) ? "主客户端没有进入匹配队列" : status;
    }

    IEnumerator F024ReplayWaitForPrimaryBattle(float timeout)
    {
        float started = Time.realtimeSinceStartup;
        while ((page != "battle" || state == null || state.players == null || state.players.Length < 2 ||
            (!String.IsNullOrEmpty(f024ReplayPeerMatchId) && state.match_id != f024ReplayPeerMatchId)) &&
            String.IsNullOrEmpty(f024ReplayFailure) && Time.realtimeSinceStartup - started < timeout)
            yield return null;
        if ((page != "battle" || state == null || state.players == null || state.players.Length < 2 ||
            (!String.IsNullOrEmpty(f024ReplayPeerMatchId) && state.match_id != f024ReplayPeerMatchId)) && String.IsNullOrEmpty(f024ReplayFailure))
            f024ReplayFailure = "主客户端未收到双人对局状态";
    }

    void F024FinishAccountReplay(bool success, string error)
    {
        if (f024ReplayReport == null) return;
        f024ReplayReport.result = success ? "ok" : "failed";
        f024ReplayReport.error = success ? "" : (error ?? "回放失败");
        f024ReplayReport.completed_utc = DateTime.UtcNow.ToString("o");
        f024AccountReplayActive = false;
        f024AccountGuiReplayMode = false;
        f024AccountEditorGuiReplayMode = false;
        f024AccountEditorNativeInputMode = false;
        f024ReplayResponses.Clear();
        f024Password = "";
        f024ReplayPassword = "";
        if (!success)
        {
            f024ReplayPeerConnection?.Dispose();
            f024ReplayPeerConnection = null;
            if (page == "queue")
            {
                connection?.Dispose();
                joining = false;
                connectionLost = true;
                page = "decks";
            }
        }
        try
        {
            string reportPath = Path.Combine(profilePath, "account-replay.json");
            string reportJson = JsonUtility.ToJson(f024ReplayReport, true);
            File.WriteAllText(reportPath, reportJson);
            string externalReportPath = Argument("--account-replay-report");
            if (!String.IsNullOrEmpty(externalReportPath))
            {
                externalReportPath = Path.GetFullPath(externalReportPath);
                if (!String.Equals(externalReportPath, Path.GetFullPath(reportPath), StringComparison.OrdinalIgnoreCase))
                {
                    string reportDirectory = Path.GetDirectoryName(externalReportPath);
                    if (!String.IsNullOrEmpty(reportDirectory)) Directory.CreateDirectory(reportDirectory);
                    File.WriteAllText(externalReportPath, reportJson);
                }
            }
            status = success ? "账号回放完成 · " + reportPath : "账号回放失败 · " + f024ReplayReport.error;
        }
        catch (Exception writeError)
        {
            status = "账号回放报告写入失败 · " + writeError.Message;
        }
        if (Flag("--account-replay-exit"))
        {
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}
