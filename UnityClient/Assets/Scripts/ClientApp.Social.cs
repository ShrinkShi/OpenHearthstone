using System;

public partial class ClientApp
{
    void F026RequestFriendAdd(string username)
    {
        if (String.IsNullOrWhiteSpace(username)) return;
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_friend_add",
            new F026FriendAddRequest { request_id = requestId, username = username.Trim() });
    }

    void F026RequestFriendAccept(string friendRequestId)
    {
        F026RequestFriendAction("account_friend_accept", friendRequestId);
    }

    void F026RequestFriendReject(string friendRequestId)
    {
        F026RequestFriendAction("account_friend_reject", friendRequestId);
    }

    void F026RequestFriendCancel(string friendRequestId)
    {
        F026RequestFriendAction("account_friend_cancel", friendRequestId);
    }

    void F026RequestFriendAction(string operation, string friendRequestId)
    {
        if (String.IsNullOrWhiteSpace(friendRequestId)) return;
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, operation,
            new F026FriendRequestAction
            {
                type = operation,
                request_id = requestId,
                friend_request_id = friendRequestId
            });
    }

    void F026RequestFriendRemove(string accountId)
    {
        if (String.IsNullOrWhiteSpace(accountId)) return;
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_friend_remove",
            new F026FriendRemoveRequest { request_id = requestId, account_id = accountId });
    }

    void F026RequestPrivacyUpdate(bool hideOnline, bool allowFriendRequests)
    {
        string requestId = F024NewRequestId();
        F024SendAuthenticatedRequest(requestId, "account_privacy_update",
            new F026PrivacyUpdateRequest
            {
                request_id = requestId,
                hide_online = hideOnline,
                allow_friend_requests = allowFriendRequests
            });
    }
}
