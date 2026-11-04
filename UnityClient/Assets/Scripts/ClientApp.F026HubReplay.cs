using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class ClientApp
{
    F024AccountResponse f026ReplayPeerResponse;

    IEnumerator F026ReplayAction(string target, string handler, Action direct)
    {
        if (F024ReplayGuiEnabled) yield return F024ReplayGuiClick(target, handler);
        else direct();
    }

    IEnumerator F026ReplayHubControls()
    {
        yield return F026ReplayAction("settings.open", "OpenSettings", () => F025OpenSettings(false));
        yield return F024ReplayCapture("account-replay-game-menu");
        yield return F026ReplayAction("settings.options", "OpenOptions", () => f025SettingsTab = 1);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayCapture("account-replay-options");
        int oldFrameRate = f026ClientOptions.targetFrameRate;
        int testRate = oldFrameRate == 120 ? 60 : 120;
        yield return F026ReplayAction("settings.display.fps:" + testRate, "SetFrameRate", () =>
        {
            f026ClientOptions.targetFrameRate = testRate;
            F026MarkOptionsChanged("", "");
        });
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return new WaitForSecondsRealtime(.6f);
        var savedOptions = ClientOptions.Load(profilePath);
        if (Application.targetFrameRate != testRate || savedOptions.targetFrameRate != testRate)
        {
            f024ReplayFailure = "设置页帧率选项未实际应用或未持久保存";
            yield break;
        }
        yield return F026ReplayAction("settings.display.fps:" + oldFrameRate, "SetFrameRate", () =>
        {
            f026ClientOptions.targetFrameRate = oldFrameRate;
            F026MarkOptionsChanged("", "");
        });
        yield return F026ReplayAction("settings.close", "CloseSettings", F025CloseSettings);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        f024ReplayReport.steps.Add("游戏菜单进入双列选项，实际修改帧率并保存重读；未自动切换分辨率或全屏");

        string accountBefore = JsonUtility.ToJson(f024Account);
        int requestsBefore = f024ReplayReport.requests.Count;
        uiEditor.SelectView("quests");
        uiEditor.Active = true;
        yield return F024ReplayCapture("account-replay-quests-f6");
        var ids = calibration.Targets.Select(item => item.id).ToArray();
        string missing = String.Join(",", F026QuestCalibration.TargetIds.Where(id => !ids.Contains(id)));
        uiEditor.Active = false;
        uiEditor.SelectView("card.minion");
        if (!String.IsNullOrEmpty(missing) || JsonUtility.ToJson(f024Account) != accountBefore ||
            f024ReplayReport.requests.Count != requestsBefore)
        {
            f024ReplayFailure = "F6任务预览目标缺失或产生账号副作用: " + missing;
            yield break;
        }
        f024ReplayReport.steps.Add("实际F6离线任务页绘出4+4任务及76个布局目标，账号快照和请求数保持不变");

        yield return F026ReplayFriends();
    }

    IEnumerator F026ReplayRefreshFriends()
    {
        if (f026FriendsOpen)
            yield return F026ReplayAction("social.close", "CloseFriendsPanel", () => f026FriendsOpen = false);
        yield return F026ReplayAction("social.open", "OpenFriendsAndRefresh", () =>
        {
            f026FriendsOpen = true;
            F024RequestAccountState();
        });
        yield return F024ReplayWaitForSettled(20);
    }

    IEnumerator F026ReplayPeerSend(Connection peer, object request, string operation, string requestId)
    {
        f026ReplayPeerResponse = null;
        F024AccountReplayRecordExternalRequest(requestId, operation);
        peer.Send(JsonUtility.ToJson(request));
        yield return F024ReplayWaitRaw(peer, "account_result", requestId);
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        f026ReplayPeerResponse = JsonUtility.FromJson<F024AccountResponse>(f024ReplayRawResponse);
        F024AccountReplayRecordExternalResponse(f026ReplayPeerResponse);
        if (f026ReplayPeerResponse == null || !f026ReplayPeerResponse.ok)
            f024ReplayFailure = "好友回放第二账号请求失败: " + operation;
    }

    IEnumerator F026ReplayFriends()
    {
        var peer = new Connection();
        try
        {
            peer.Connect(host, port);
            yield return F024ReplayWaitRaw(peer, "hello", "");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            var hello = JsonUtility.FromJson<F024AccountHello>(f024ReplayRawResponse);
            if (hello == null || !(hello.capabilities ?? new string[0]).Contains("account_social_v1"))
            {
                f024ReplayFailure = "好友回放服务未声明account_social_v1";
                yield break;
            }
            string username = "f026s" + Guid.NewGuid().ToString("N").Substring(0, 12);
            string id = F024NewRequestId();
            yield return F026ReplayPeerSend(peer, new F024AccountRegisterRequest
            {
                request_id = id, username = username, display_name = "好友回放",
                password = "R" + Guid.NewGuid().ToString("N") + "!"
            }, "account_register", id);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            string peerAccount = f026ReplayPeerResponse.snapshot.account_id;
            yield return F026ReplayRefreshFriends();
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (F024ReplayGuiEnabled)
                yield return F024ReplayGuiClick("social.add.open", "ToggleFriendAdd");
            f026FriendName = username;
            yield return F026ReplayAction("social.add", "AddFriend", () => F026RequestFriendAdd(username));
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F024ReplayWaitForSettled(20);
            var outgoing = (f024Account.outgoing_friend_requests ?? new F026FriendRequest[0])
                .FirstOrDefault(item => item.account_id == peerAccount);
            if (outgoing == null)
            {
                f024ReplayFailure = "添加好友没有产生真实申请";
                yield break;
            }
            yield return F024ReplayCapture("account-replay-friend-outgoing");
            id = F024NewRequestId();
            yield return F026ReplayPeerSend(peer, new F026FriendRequestAction
            {
                type = "account_friend_accept", request_id = id, friend_request_id = outgoing.request_id
            }, "account_friend_accept", id);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F026ReplayRefreshFriends();
            var friend = (f024Account.friends ?? new F026Friend[0]).FirstOrDefault(item => item.account_id == peerAccount);
            if (friend == null || !friend.online)
            {
                f024ReplayFailure = "已接受好友关系未显示为在线";
                yield break;
            }
            yield return F024ReplayCapture("account-replay-friend-online");
            id = F024NewRequestId();
            yield return F026ReplayPeerSend(peer, new F024AccountLogoutRequest { request_id = id }, "account_logout", id);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            yield return F026ReplayRefreshFriends();
            friend = (f024Account.friends ?? new F026Friend[0]).FirstOrDefault(item => item.account_id == peerAccount);
            if (friend == null || friend.online || friend.last_seen <= 0)
            {
                f024ReplayFailure = "好友注销后未显示离线与最后在线时间";
                yield break;
            }
            yield return F024ReplayCapture("account-replay-friend-offline");
            if (F024ReplayGuiEnabled)
                yield return F024ReplayGuiClick("social.select:" + peerAccount, "SelectFriend");
            yield return F026ReplayAction("social.remove:" + peerAccount, "RemoveFriend", () => F026RequestFriendRemove(peerAccount));
            yield return F024ReplayWaitForSettled(20);
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if ((f024Account.friends ?? new F026Friend[0]).Any(item => item.account_id == peerAccount))
            {
                f024ReplayFailure = "移除好友未写回服务器快照";
                yield break;
            }
            yield return F026ReplayAction("social.close", "CloseFriendsPanel", () => f026FriendsOpen = false);
            f024ReplayReport.steps.Add("好友GUI真实申请、第二账号接受、在线/注销离线、最后在线时间与移除通过");
        }
        finally { peer.Dispose(); }
    }

    IEnumerator F026ReplayGuiBuy(int setId, int count, bool proveCancel = false)
    {
        int beforeGold = f024Account.gold;
        int beforePacks = F024ReplayPackCount(setId);
        int beforeRequests = f024ReplayReport.requests.Count(item => item.operation == "account_shop_buy");
        string target = "shop.buy:" + setId;
        yield return F024ReplayGuiClick(target, "OpenShopBuyModal");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayCapture("account-replay-buy-modal");
        if (proveCancel)
        {
            yield return F024ReplayGuiClick("shop.confirm.count.plus", "ShopBuyCountUp");
            yield return F024ReplayGuiClick("shop.confirm.count.minus", "ShopBuyCountDown");
            yield return F024ReplayGuiClick("shop.confirm.cancel", "CloseShopBuyModal");
            if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
            if (f024Account.gold != beforeGold || F024ReplayPackCount(setId) != beforePacks ||
                f024ReplayReport.requests.Count(item => item.operation == "account_shop_buy") != beforeRequests)
            {
                f024ReplayFailure = "打开、调整数量、取消购包弹窗意外改变钱包或发出购买请求";
                yield break;
            }
            yield return F024ReplayGuiClick(target, "OpenShopBuyModal");
        }
        for (int index = 1; index < count && String.IsNullOrEmpty(f024ReplayFailure); index++)
            yield return F024ReplayGuiClick("shop.confirm.count.plus", "ShopBuyCountUp");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        yield return F024ReplayCapture("account-replay-buy-modal-quantity-" + count);
        yield return F024ReplayGuiClick("shop.confirm:" + setId, "ConfirmShopBuy");
        if (!String.IsNullOrEmpty(f024ReplayFailure)) yield break;
        if (f024ReplayReport.requests.Count(item => item.operation == "account_shop_buy") != beforeRequests + 1)
            f024ReplayFailure = "购包确认没有恰好发出一次权威购买请求";
    }
}
