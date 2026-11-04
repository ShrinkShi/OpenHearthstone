using System;

[Serializable] public sealed class F024AccountEndpointSettings
{
    public string host;
    public int port;
}

[Serializable] public sealed class F024AccountCard
{
    public string card_id;
    public int normal, golden, free;
}

[Serializable] public sealed class F024AccountDeck
{
    public string deck_id, name, format;
    public int class_id, revision;
    public string[] cards;
}

[Serializable] public sealed class F024AccountPack
{
    public int set_id, count;
}

[Serializable] public sealed class F024AccountQuest
{
    public string quest_id, period, difficulty, title, description;
    public int progress, target, reward_gold;
    public bool completed, reroll_available;
}

[Serializable] public sealed class F024AccountShopItem
{
    public int set_id, price;
    public string name;
}

[Serializable] public sealed class F026Friend
{
    public string account_id, short_id, username, display_name;
    public bool online;
    public long last_seen;
}

[Serializable] public sealed class F026FriendRequest
{
    public string request_id, account_id, short_id, username, display_name;
    public long created_at;
}

[Serializable] public sealed class F026AccountPrivacy
{
    public bool hide_online, allow_friend_requests;
}

[Serializable] public sealed class F024AccountSnapshot
{
    public string account_id, username, display_name;
    public int gold, dust, revision;
    public F024AccountCard[] cards;
    public F024AccountDeck[] decks;
    public F024AccountPack[] packs;
    public F024AccountQuest[] quests;
    public F024AccountShopItem[] shop;
    public F026Friend[] friends;
    public F026FriendRequest[] incoming_friend_requests, outgoing_friend_requests;
    public F026AccountPrivacy privacy;
}

[Serializable] public sealed class F024OpenedCard
{
    public string card_id;
    public bool golden, new_card;
}

[Serializable] public sealed class F024AccountResponse
{
    public string type, operation, request_id, error, token, open_id, deck_id, message, deck_code, format;
    public int class_id;
    public string[] cards;
    public bool ok;
    public F024AccountSnapshot snapshot;
    public F024OpenedCard[] opened_cards;
}

[Serializable] public sealed class F024AccountHello
{
    public string type;
    public string[] capabilities;
}

[Serializable] public sealed class F024AccountRegisterRequest
{
    public string type = "account_register", request_id, username, password, display_name;
}

[Serializable] public sealed class F024AccountLoginRequest
{
    public string type = "account_login", request_id, username, password;
}

[Serializable] public sealed class F024AccountResumeRequest
{
    public string type = "account_resume", request_id, token;
}

[Serializable] public sealed class F024AccountLogoutRequest
{
    public string type = "account_logout", request_id;
}

[Serializable] public sealed class F024AccountStateRequest
{
    public string type = "account_state", request_id;
}

[Serializable] public sealed class F024DeckSaveRequest
{
    public string type = "account_deck_save", request_id, deck_id, name, format;
    public int class_id;
    public string[] cards;
}

[Serializable] public sealed class F028DeckCodeRequest
{
    public string type, request_id, deck_code, format;
    public int class_id;
    public string[] cards;
}

[Serializable] public sealed class F024DeckDeleteRequest
{
    public string type = "account_deck_delete", request_id, deck_id;
}

[Serializable] public sealed class F024CraftRequest
{
    public string type = "account_craft", request_id, card_id;
    public bool golden;
    public int count = 1;
}

[Serializable] public sealed class F024DisenchantRequest
{
    public string type = "account_disenchant", request_id, card_id;
    public bool golden;
    public int count = 1;
}

[Serializable] public sealed class F024ShopBuyRequest
{
    public string type = "account_shop_buy", request_id;
    public int set_id, count = 1;
}

[Serializable] public sealed class F024PackOpenRequest
{
    public string type = "account_pack_open", request_id;
    public int set_id;
}

[Serializable] public sealed class F024QuestRerollRequest
{
    public string type = "account_quest_reroll", request_id, quest_id;
}

[Serializable] public sealed class F024AuthenticatedJoinRequest
{
    public string type = "join", deck_id;
}

[Serializable] public sealed class F026FriendAddRequest
{
    public string type = "account_friend_add", request_id, username;
}

[Serializable] public sealed class F026FriendRequestAction
{
    public string type, request_id, friend_request_id;
}

[Serializable] public sealed class F026FriendRemoveRequest
{
    public string type = "account_friend_remove", request_id, account_id;
}

[Serializable] public sealed class F026PrivacyUpdateRequest
{
    public string type = "account_privacy_update", request_id;
    public bool hide_online, allow_friend_requests;
}
