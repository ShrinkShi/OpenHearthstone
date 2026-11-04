using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

public sealed partial class ClientApp
{
    sealed class F024DeferredRequest
    {
        public string requestId, operation, json;
    }

    F024AccountGateway f024AccountGateway;
    F024ProtectedSessionStore f024SessionStore;
    readonly Queue<F024DeferredRequest> f024DeferredRequests = new Queue<F024DeferredRequest>();
    readonly Dictionary<string, F024DeferredRequest> f024RetryableMutations = new Dictionary<string, F024DeferredRequest>();
    readonly HashSet<string> f024RetryableInFlight = new HashSet<string>();
    F024AccountSnapshot f024Account;
    string f024Token = "", f024GatewayHost = "";
    string f024GatewayPort = "";
    string f024Username = "", f024Password = "", f024DisplayName = "";
    string f024AccountMessage = "", f024SelectedDeckId = "", f024GameResumeRequestId = "";
    int f024GatewayPortNumber;
    int f024ReconnectAttempt;
    float f024ReconnectAt;
    bool f024RegisterMode, f024GatewayAuthenticated, f024AwaitingGatewayResume, f024AccountBusy, f024GuestDiagnosticReplay;
    bool f024SnapshotRefreshQueued;

    bool F024HasAccount
    {
        get { return f024Account != null && !String.IsNullOrEmpty(f024Token); }
    }

    bool F024HasGuestDiagnosticReplayFlag()
    {
        return Flag("--fireblast-replay") || Flag("--hero-power-replay") ||
            Flag("--hero-attack-replay") || Flag("--hero-speech-replay") ||
            Flag("--h10-player-replay") || Flag("--h11-player-replay");
    }

    bool F024IsLoopbackHost()
    {
        string endpoint = (host ?? "").Trim();
        if (String.Equals(endpoint, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        IPAddress address;
        return IPAddress.TryParse(endpoint, out address) && IPAddress.IsLoopback(address);
    }

    bool F024AllowsLoopbackGuestReplay()
    {
        return f024GuestDiagnosticReplay && F024IsLoopbackHost();
    }

    void InitializeF024AccountSession()
    {
        if (f024GuestDiagnosticReplay)
        {
            f024Token = "";
            f024Account = null;
            f024SelectedDeckId = "";
            f024AccountMessage = "本机访客诊断回放";
            return;
        }
        LoadF024AccountEndpoint();
        f024SessionStore = new F024ProtectedSessionStore(System.IO.Path.Combine(profilePath, "account-session.bin"));
        if (f024SessionStore.TryLoad(out f024Token))
        {
            f024AccountMessage = "正在恢复已保存的加密会话……";
            F024EnsureAccountGateway();
            F024SendGatewayResume();
        }
        else f024AccountMessage = "尚未登录账号";
    }

    void PumpF024AccountGateway()
    {
        if (f024AccountGateway != null) f024AccountGateway.Pump();
        if (String.IsNullOrEmpty(f024Token) || f024GatewayAuthenticated || f024AwaitingGatewayResume ||
            f024ReconnectAt <= 0 || Time.unscaledTime < f024ReconnectAt) return;
        f024ReconnectAt = 0;
        F024SendGatewayResume();
    }

    void F024EnsureAccountGateway()
    {
        int endpointPort = port;
        if (f024AccountGateway != null && !f024AccountGateway.IsFailed &&
            f024GatewayHost == host && f024GatewayPortNumber == endpointPort) return;
        if (f024AccountGateway != null)
        {
            f024AccountGateway.Dispose();
            f024AccountGateway = null;
        }
        f024AccountGateway = new F024AccountGateway();
        f024AccountGateway.ResponseReceived += HandleF024AccountResponse;
        f024AccountGateway.ConnectionFailed += HandleF024AccountConnectionFailure;
        f024GatewayHost = host;
        f024GatewayPortNumber = endpointPort;
        f024GatewayAuthenticated = false;
        f024AwaitingGatewayResume = false;
        f024DeferredRequests.Clear();
        f024AccountGateway.Connect(host, endpointPort);
    }

    string F024NewRequestId() { return Guid.NewGuid().ToString("N"); }

    void F024SendGatewayRequest(string requestId, string operation, object request)
    {
        F024EnsureAccountGateway();
        F024AccountReplayObserveRequest(requestId, operation);
        try
        {
            f024AccountGateway.Send(requestId, operation, JsonUtility.ToJson(request));
            f024AccountBusy = true;
        }
        catch (Exception error)
        {
            f024AccountBusy = false;
            f024AccountMessage = "账号请求失败：" + error.Message;
            status = f024AccountMessage;
        }
    }

    void F024SendAuthenticatedRequest(string requestId, string operation, object request)
    {
        if (!F024HasAccount)
        {
            f024AccountMessage = "请先登录账号";
            F025OpenSettings(true);
            return;
        }
        F024EnsureAccountGateway();
        var json = JsonUtility.ToJson(request);
        F024AccountReplayObserveRequest(requestId, operation);
        if (F024IsRetryableMutation(operation))
            f024RetryableMutations[requestId] = new F024DeferredRequest { requestId = requestId, operation = operation, json = json };
        if (f024GatewayAuthenticated)
        {
            F024SendGatewayJson(requestId, operation, json);
            return;
        }
        f024DeferredRequests.Enqueue(new F024DeferredRequest { requestId = requestId, operation = operation, json = json });
        if (!f024AwaitingGatewayResume) F024SendGatewayResume();
    }

    void F024SendGatewayJson(string requestId, string operation, string json)
    {
        try
        {
            f024AccountGateway.Send(requestId, operation, json);
            if (f024RetryableMutations.ContainsKey(requestId)) f024RetryableInFlight.Add(requestId);
            f024AccountBusy = true;
        }
        catch (Exception error)
        {
            f024RetryableInFlight.Remove(requestId);
            if (requestId == f028PendingCodeRequestId) f028PendingCodeRequestId = "";
            f024AccountBusy = f024RetryableMutations.Count > 0;
            f024AccountMessage = "账号请求失败：" + error.Message;
            status = f024AccountMessage;
        }
    }

    void F024SendGatewayResume()
    {
        if (String.IsNullOrEmpty(f024Token)) return;
        F024EnsureAccountGateway();
        f024AwaitingGatewayResume = true;
        f024AccountBusy = true;
        string requestId = F024NewRequestId();
        F024SendGatewayRequest(requestId, "account_resume",
            new F024AccountResumeRequest { request_id = requestId, token = f024Token });
    }

    void FlushF024DeferredRequests()
    {
        while (f024GatewayAuthenticated && f024DeferredRequests.Count > 0)
        {
            var request = f024DeferredRequests.Dequeue();
            F024SendGatewayJson(request.requestId, request.operation, request.json);
            request.json = null;
        }
        RetryOutstandingF024Mutations();
    }

    void RetryOutstandingF024Mutations()
    {
        if (!f024GatewayAuthenticated) return;
        foreach (var request in f024RetryableMutations.Values.ToArray())
        {
            if (request == null || f024RetryableInFlight.Contains(request.requestId)) continue;
            F024SendGatewayJson(request.requestId, request.operation, request.json);
        }
    }

    void HandleF024AccountResponse(F024AccountResponse response)
    {
        if (response == null) return;
        F024AccountReplayObserveResponse(response);
        string responseRequestId = response.request_id ?? "";
        bool completesDeck = !String.IsNullOrEmpty(f028PendingSaveRequestId) &&
            responseRequestId == f028PendingSaveRequestId && response.operation == "account_deck_save";
        if (completesDeck) f028PendingSaveRequestId = "";
        f024RetryableInFlight.Remove(responseRequestId);
        f024RetryableMutations.Remove(responseRequestId);
        f024AccountBusy = false;
        if (response.type == "error")
        {
            if (responseRequestId == f028PendingCodeRequestId) f028PendingCodeRequestId = "";
            f024AccountMessage = String.IsNullOrEmpty(response.error) ? response.message : response.error;
            if (response.operation == "account_resume")
            {
                f024AwaitingGatewayResume = false;
                f024GatewayAuthenticated = false;
                f024RetryableMutations.Clear();
                f024RetryableInFlight.Clear();
                f024DeferredRequests.Clear();
                f028PendingSaveRequestId = "";
                f028PendingCodeRequestId = "";
            }
            if (response.operation == "account_state") f024SnapshotRefreshQueued = false;
            status = f024AccountMessage;
            return;
        }
        bool logoutResponse = response.operation == "account_logout";
        bool authenticationResponse = response.operation == "account_register" || response.operation == "account_login" ||
            response.operation == "account_resume";
        bool canApplySnapshot = !logoutResponse && (authenticationResponse || (F024HasAccount && f024GatewayAuthenticated));
        bool staleSnapshot = canApplySnapshot && response.snapshot != null && f024Account != null &&
            response.snapshot.revision < f024Account.revision;
        if (canApplySnapshot && response.snapshot != null && !staleSnapshot)
        {
            f024Account = response.snapshot;
            F024OnSnapshotUpdated();
        }
        if (!response.ok)
        {
            if (responseRequestId == f028PendingCodeRequestId) f028PendingCodeRequestId = "";
            f024AccountMessage = String.IsNullOrEmpty(response.error) ? "账号请求失败" : response.error;
            if (response.operation == "account_resume")
            {
                f024AwaitingGatewayResume = false;
                f024GatewayAuthenticated = false;
                f024Token = "";
                f024Account = null;
                f024RetryableMutations.Clear();
                f024RetryableInFlight.Clear();
                f024DeferredRequests.Clear();
                f028PendingSaveRequestId = "";
                f028PendingCodeRequestId = "";
                if (f024SessionStore != null)
                {
                    try { f024SessionStore.Clear(); }
                    catch (Exception error) { Debug.LogWarning("Could not clear saved account session: " + error.Message); }
                }
            }
            status = f024AccountMessage;
            return;
        }

        if (response.operation == "account_register" || response.operation == "account_login" || response.operation == "account_resume")
        {
            f024SnapshotRefreshQueued = false;
            if (!String.IsNullOrEmpty(response.token))
            {
                f024Token = response.token;
                bool persisted = f024SessionStore != null && f024SessionStore.TrySave(f024Token);
                f024AccountMessage = persisted ? "账号已登录，会话已加密保存" : "账号已登录；此环境无法加密保存会话";
            }
            f024GatewayAuthenticated = true;
            f024AwaitingGatewayResume = false;
            f024ReconnectAttempt = 0;
            f024ReconnectAt = 0;
            if (f024Account != null)
            {
                playerName = String.IsNullOrEmpty(f024Account.display_name) ? f024Account.username : f024Account.display_name;
                NativeWindow.SetTitle("HearthstoneClone · " + playerName);
            }
            if (response.operation == "account_register")
            {
                f024AccountMessage = "注册完成：已领取基础卡牌和1000金币";
                if (f025SettingsOpen) F025CloseSettings();
            }
            else if (response.operation == "account_login") f024AccountMessage = "登录成功";
            else if (String.IsNullOrEmpty(f024AccountMessage)) f024AccountMessage = "会话恢复成功";
            if (page == "account") page = "menu";
            FlushF024DeferredRequests();
        }
        else if (response.operation == "account_logout")
        {
            f024SnapshotRefreshQueued = false;
            if (f024SessionStore != null)
            {
                try { f024SessionStore.Clear(); }
                catch (Exception error) { Debug.LogWarning("Could not clear saved account session: " + error.Message); }
            }
            f024Token = "";
            f024Account = null;
            f024AccountBusy = false;
            f024GatewayAuthenticated = false;
            f024RetryableMutations.Clear();
            f024RetryableInFlight.Clear();
            f024SelectedDeckId = "";
            f024DraftId = "";
            f024DraftName = "";
            f024DraftClassId = 0;
            f024DraftCards.Clear();
            f024DraftDirty = false;
            f028PendingSaveRequestId = "";
            f028PendingCodeRequestId = "";
            f024CollectionCacheKey = "";
            f024FilteredCards = new CardDef[0];
            f024SelectedCardId = "";
            f024AccountMessage = "已退出账号";
            if (page == "account" && !f025SettingsOpen) page = "menu";
        }
        else if (response.operation == "account_pack_open")
        {
            f024AccountMessage = "卡包已由服务器开启，卡牌已写入收藏";
            BeginF024PackReveal(response.opened_cards, response.open_id);
        }
        else if (response.operation == "account_deck_save" && completesDeck)
        {
            f024DraftDirty = false;
            if (!staleSnapshot)
            {
                string savedDeckId = response.deck_id;
                if (String.IsNullOrEmpty(savedDeckId))
                {
                    var saved = ((f024Account == null ? null : f024Account.decks) ?? new F024AccountDeck[0])
                        .FirstOrDefault(item => item != null && item.name == f024DraftName && item.class_id == f024DraftClassId &&
                            (item.cards ?? new string[0]).SequenceEqual(f024DraftCards));
                    savedDeckId = saved == null ? "" : saved.deck_id;
                }
                if (!String.IsNullOrEmpty(savedDeckId)) f024SelectedDeckId = f024DraftId = savedDeckId;
                F024RefreshDeckDraftFromSnapshot();
            }
            f024AccountMessage = staleSnapshot ? "卡组已保存，正在同步较新的账号数据" : "卡组已保存到服务器";
            if (page == "collection" && f024CollectionTab == 1)
            {
                f024CollectionTab = 0;
                f024CollectionPage = 0;
                f024CollectionSearch = "";
                f029CraftingMode = false;
                f024CollectionOwnedMode = 0;
                f024CollectionCacheKey = "";
            }
        }
        else if (response.operation == "account_deck_encode" && responseRequestId == f028PendingCodeRequestId)
        {
            f028PendingCodeRequestId = "";
            try
            {
                if (f024AccountReplayActive) f028ReplayClipboard = response.deck_code ?? "";
                else GUIUtility.systemCopyBuffer = response.deck_code ?? "";
                f024AccountMessage = "套牌代码已复制到剪贴板";
            }
            catch (Exception error) { f024AccountMessage = "复制套牌代码失败：" + error.Message; }
        }
        else if (response.operation == "account_deck_decode" && responseRequestId == f028PendingCodeRequestId)
        {
            f028PendingCodeRequestId = "";
            if (page == "collection" && f024CollectionTab == 1 && String.IsNullOrEmpty(f024DraftId))
            {
                f024DraftClassId = response.class_id;
                f028DraftFormat = response.format == "standard" ? "standard" : "wild";
                f024DraftCards.Clear();
                f024DraftCards.AddRange(response.cards ?? new string[0]);
                f024DraftName = F024ClassName(response.class_id) + "套牌";
                f024DraftDirty = true;
                f024CollectionPage = 0;
                f024CollectionCacheKey = "";
                f024AccountMessage = "套牌代码已导入，请检查卡牌和格式后保存";
            }
        }
        else f024AccountMessage = "已同步账号数据";
        status = f024AccountMessage;
        if (staleSnapshot && response.operation != "account_state" && F024HasAccount &&
            f024GatewayAuthenticated && !f024SnapshotRefreshQueued)
        {
            f024SnapshotRefreshQueued = true;
            F024RequestAccountState();
        }
        else if (response.operation == "account_state") f024SnapshotRefreshQueued = false;
    }

    void HandleF024AccountConnectionFailure(string message)
    {
        f028PendingCodeRequestId = "";
        f024RetryableInFlight.Clear();
        f024AccountBusy = f024RetryableMutations.Count > 0;
        f024GatewayAuthenticated = false;
        f024AwaitingGatewayResume = false;
        f024AccountMessage = String.IsNullOrEmpty(message) ? "账号服务连接失败" : message;
        if (!String.IsNullOrEmpty(f024Token) && f024AccountMessage.IndexOf("accounts_v1", StringComparison.OrdinalIgnoreCase) < 0)
        {
            f024ReconnectAttempt = Mathf.Min(f024ReconnectAttempt + 1, 6);
            f024ReconnectAt = Time.unscaledTime + Mathf.Min(2f * f024ReconnectAttempt, 20f);
            f024AccountMessage += "；将在稍后尝试恢复会话";
        }
        else if (f024AccountMessage.IndexOf("accounts_v1", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            f024RetryableMutations.Clear();
            f024RetryableInFlight.Clear();
            f024AccountBusy = false;
            f028PendingSaveRequestId = "";
        }
        if (f024AccountReplayActive) f024ReplayFailure = f024AccountMessage;
        status = f024AccountMessage;
    }

    bool F024IsRetryableMutation(string operation)
    {
        return operation == "account_shop_buy" || operation == "account_pack_open" ||
            operation == "account_craft" || operation == "account_disenchant" ||
            operation == "account_deck_save" || operation == "account_deck_delete" ||
            operation == "account_quest_reroll";
    }

    void F024Register()
    {
        if (!F024ApplyAccountEndpoint()) return;
        if (String.IsNullOrWhiteSpace(f024Username) || String.IsNullOrEmpty(f024Password) || String.IsNullOrWhiteSpace(f024DisplayName))
        {
            f024AccountMessage = "请填写账号名、显示名称和密码";
            return;
        }
        string requestId = F024NewRequestId();
        var request = new F024AccountRegisterRequest
        {
            request_id = requestId,
            username = f024Username.Trim(),
            password = f024Password,
            display_name = f024DisplayName.Trim()
        };
        f024Password = "";
        F024SendGatewayRequest(requestId, "account_register", request);
    }

    void F024Login()
    {
        if (!F024ApplyAccountEndpoint()) return;
        if (String.IsNullOrWhiteSpace(f024Username) || String.IsNullOrEmpty(f024Password))
        {
            f024AccountMessage = "请填写账号名和密码";
            return;
        }
        string requestId = F024NewRequestId();
        var request = new F024AccountLoginRequest
        {
            request_id = requestId,
            username = f024Username.Trim(),
            password = f024Password
        };
        f024Password = "";
        F024SendGatewayRequest(requestId, "account_login", request);
    }

    void F024Logout()
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_logout",
            new F024AccountLogoutRequest { request_id = requestId });
    }

    void F024RequestAccountState()
    {
        if (!F024HasAccount)
        {
            f024SnapshotRefreshQueued = false;
            return;
        }
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_state",
            new F024AccountStateRequest { request_id = requestId });
    }

    void F024RequestDeckSave(string deckId, string name, int classId, string[] cardIds)
    {
        string requestId = F024NewRequestId();
        if (F024HasAccount) f028PendingSaveRequestId = requestId;
        F024SendAuthenticatedRequest(requestId, "account_deck_save", new F024DeckSaveRequest
        {
            request_id = requestId,
            deck_id = deckId ?? "",
            name = name ?? "",
            class_id = classId,
            format = f028DraftFormat,
            cards = cardIds ?? new string[0]
        });
    }

    void F028RequestDeckCode()
    {
        string requestId = F024NewRequestId();
        f028PendingCodeRequestId = requestId;
        F024SendAuthenticatedRequest(requestId, "account_deck_encode", new F028DeckCodeRequest
        {
            type = "account_deck_encode", request_id = requestId, class_id = f024DraftClassId,
            cards = f024DraftCards.ToArray(), format = f028DraftFormat
        });
    }

    void F028RequestDeckImport(string code)
    {
        string requestId = F024NewRequestId();
        f028PendingCodeRequestId = requestId;
        F024SendAuthenticatedRequest(requestId, "account_deck_decode", new F028DeckCodeRequest
        {
            type = "account_deck_decode", request_id = requestId, deck_code = code ?? ""
        });
    }

    void F024RequestDeckDelete(string deckId)
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_deck_delete",
            new F024DeckDeleteRequest { request_id = requestId, deck_id = deckId ?? "" });
    }

    void F024RequestCraft(string cardId, bool golden)
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_craft",
            new F024CraftRequest { request_id = requestId, card_id = cardId, golden = golden, count = 1 });
    }

    void F024RequestDisenchant(string cardId, bool golden)
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_disenchant",
            new F024DisenchantRequest { request_id = requestId, card_id = cardId, golden = golden, count = 1 });
    }

    void F024RequestShopBuy(int setId, int count)
    {
        if (setId < 0 || count <= 0) return;
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_shop_buy",
            new F024ShopBuyRequest { request_id = requestId, set_id = setId, count = count });
    }

    void F024RequestPackOpen(int setId)
    {
        if (setId < 0) return;
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_pack_open",
            new F024PackOpenRequest { request_id = requestId, set_id = setId });
    }

    void F024RequestQuestReroll(string questId)
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_quest_reroll",
            new F024QuestRerollRequest { request_id = requestId, quest_id = questId });
    }

    void F024OpenPage(string target)
    {
        page = target;
        if (target == "collection" || target == "packs" || target == "shop" || target == "quests" || target == "decks")
            F024RequestAccountState();
    }

    bool F024ApplyAccountEndpoint()
    {
        host = (host ?? "").Trim();
        int selectedPort;
        if (String.IsNullOrEmpty(host) || !Int32.TryParse(f024GatewayPort, out selectedPort) || selectedPort < 1 || selectedPort > 65535)
        {
            f024AccountMessage = "服务器地址或端口无效";
            return false;
        }
        port = selectedPort;
        f024GatewayPort = selectedPort.ToString();
        SaveF024AccountEndpoint();
        return true;
    }

    void LoadF024AccountEndpoint()
    {
        f024GatewayPort = port.ToString();
        string path = System.IO.Path.Combine(profilePath, "account-endpoint.json");
        if (!System.IO.File.Exists(path)) return;
        try
        {
            var saved = JsonUtility.FromJson<F024AccountEndpointSettings>(System.IO.File.ReadAllText(path));
            if (saved == null || String.IsNullOrWhiteSpace(saved.host) || saved.port < 1 || saved.port > 65535) return;
            host = saved.host.Trim();
            port = saved.port;
            f024GatewayPort = port.ToString();
        }
        catch (Exception error) { Debug.LogWarning("Could not read saved account endpoint: " + error.Message); }
    }

    void SaveF024AccountEndpoint()
    {
        try
        {
            var saved = new F024AccountEndpointSettings { host = host, port = port };
            System.IO.File.WriteAllText(System.IO.Path.Combine(profilePath, "account-endpoint.json"), JsonUtility.ToJson(saved, true));
        }
        catch (Exception error) { Debug.LogWarning("Could not save account endpoint: " + error.Message); }
    }

    void F024OnSnapshotUpdated()
    {
        if (f024Account == null) return;
        var serverDecks = f024Account.decks ?? new F024AccountDeck[0];
        if (!serverDecks.Any(item => item != null && item.deck_id == f024SelectedDeckId))
            f024SelectedDeckId = serverDecks.FirstOrDefault(item => item != null)?.deck_id ?? "";
        F024RefreshDeckDraftFromSnapshot();
    }

    int F024OwnedCardCount(string cardId)
    {
        var owned = (f024Account == null ? null : f024Account.cards) ?? new F024AccountCard[0];
        var entry = owned.FirstOrDefault(item => item != null && item.card_id == cardId);
        return entry == null ? 0 : Mathf.Max(0, entry.normal) + Mathf.Max(0, entry.golden) + Mathf.Max(0, entry.free);
    }

    string F024ValidateOwnedDeck(string[] cardIds, int classId)
    {
        if (cardIds == null || cardIds.Length != 30) return "卡组必须恰好 30 张";
        return F028ValidateDeckDraft(cardIds, classId);
    }

    string F028ValidateDeckDraft(string[] cardIds, int classId)
    {
        if (cardIds == null || cardIds.Length > 30) return "卡组最多 30 张";
        int classPosition = Array.FindIndex(database.classes, item => item.id == classId);
        if (classPosition < 0 || !database.classes[classPosition].available) return "该职业暂不能用于对战";
        foreach (var group in cardIds.GroupBy(id => id ?? ""))
        {
            CardDef definition;
            if (!cards.TryGetValue(group.Key, out definition) || !definition.Collectible) return "卡组含有无效卡牌";
            if (!definition.Playable) return "收藏中的未实现卡牌不能加入当前对战卡组";
            if (definition.Class != 12 && definition.Class != classId) return "卡牌职业不合法";
            if (group.Count() > (definition.Rarity == 5 ? 1 : 2)) return "卡牌数量超过限制";
            if (group.Count() > F024OwnedCardCount(group.Key)) return "卡组使用了未拥有的卡牌";
        }
        if (classId == 1 && cardIds.Length > 0)
        {
            var deckDefinitions = cardIds.Select(id => cards[id]).ToArray();
            if (deckDefinitions.Max(item => item.Blood) + deckDefinitions.Max(item => item.Frost) + deckDefinitions.Max(item => item.Unholy) > 3)
                return "卡组需要超过三个符文";
        }
        return "";
    }

    bool HandleF024GameHello(StateView hello)
    {
        var capabilities = hello.capabilities ?? new string[0];
        if (!capabilities.Contains("accounts_v1"))
        {
            if (F024AllowsLoopbackGuestReplay() && !F024HasAccount) return false;
            joining = false;
            connectionLost = true;
            status = "服务器未启用账号服务；正常对局需要登录账号";
            return true;
        }
        if (!F024HasAccount || String.IsNullOrEmpty(f024SelectedDeckId))
        {
            joining = false;
            connectionLost = true;
            status = "请先登录并选择服务器卡组";
            return true;
        }
        f024GameResumeRequestId = F024NewRequestId();
        F024AccountReplayRecordExternalRequest(f024GameResumeRequestId, "account_resume");
        connection.Send(JsonUtility.ToJson(new F024AccountResumeRequest
        {
            request_id = f024GameResumeRequestId,
            token = f024Token
        }));
        return true;
    }

    void HandleF024GameResume(F024AccountResponse response)
    {
        if (response == null || response.operation != "account_resume" || response.request_id != f024GameResumeRequestId) return;
        F024AccountReplayRecordExternalResponse(response);
        joining = false;
        if (!response.ok || response.snapshot == null)
        {
            status = String.IsNullOrEmpty(response.error) ? "对局账号认证失败" : response.error;
            connectionLost = true;
            return;
        }
        if (f024Account == null || response.snapshot.revision >= f024Account.revision) f024Account = response.snapshot;
        if (!String.IsNullOrEmpty(response.token))
        {
            f024Token = response.token;
            if (f024SessionStore != null) f024SessionStore.TrySave(f024Token);
        }
        F024OnSnapshotUpdated();
        var serverDeck = (f024Account.decks ?? new F024AccountDeck[0])
            .FirstOrDefault(item => item != null && item.deck_id == f024SelectedDeckId);
        if (serverDeck == null)
        {
            status = "所选服务器卡组已不存在，请重新选择";
            connectionLost = true;
            return;
        }
        try
        {
            connection.Send(JsonUtility.ToJson(new F024AuthenticatedJoinRequest { deck_id = serverDeck.deck_id }));
            status = "账号已认证，正在加入传统对战";
        }
        catch (Exception error)
        {
            status = "加入对局失败：" + error.Message;
            connectionLost = true;
        }
    }

    bool F024CanStartMatch()
    {
        if (!F024HasAccount)
        {
            if (F024AllowsLoopbackGuestReplay()) return true;
            f024AccountMessage = "请先登录账号";
            F025OpenSettings(true);
            return false;
        }
        var serverDeck = (f024Account.decks ?? new F024AccountDeck[0])
            .FirstOrDefault(item => item != null && item.deck_id == f024SelectedDeckId);
        if (serverDeck == null)
        {
            f024AccountMessage = "请选择一套服务器卡组";
            page = "decks";
            F024RequestAccountState();
            return false;
        }
        deck = (serverDeck.cards ?? new string[0]).ToList();
        int classPosition = Array.FindIndex(database.classes, item => item.id == serverDeck.class_id);
        if (classPosition >= 0) classIndex = classPosition;
        status = ValidateDeck();
        if (status != "") return false;
        if (F024ValidateOwnedDeck(serverDeck.cards, serverDeck.class_id) != "")
        {
            status = F024ValidateOwnedDeck(serverDeck.cards, serverDeck.class_id);
            return false;
        }
        return true;
    }

    void OnF024MenuReset()
    {
        if (F024HasAccount) F024RequestAccountState();
    }
}
