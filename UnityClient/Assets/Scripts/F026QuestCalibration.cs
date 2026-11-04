using System;
using System.Collections.Generic;
using UnityEngine;

// F6 任务页预览和正式页面共用的夹具、目标 ID 与设计坐标。
public static class F026QuestCalibration
{
    public const string PageId = "quests";
    public static Rect PageRect { get { return new Rect(0, 0, 1600, 1000); } }

    public static F024AccountQuest[] FixtureQuests
    {
        get
        {
            return new[]
            {
                Fixture("daily", 0, "easy", "完成 2 场对战", 1, 2, 40),
                Fixture("daily", 1, "normal", "使用 3 张法术牌", 2, 3, 60),
                Fixture("daily", 2, "hard", "赢得 5 场对战", 2, 5, 80),
                Fixture("daily", 3, "normal", "召唤 10 个随从", 6, 10, 60),
                Fixture("weekly", 0, "easy", "完成 10 场对战", 4, 10, 80),
                Fixture("weekly", 1, "normal", "打出 30 张随从牌", 12, 30, 120),
                Fixture("weekly", 2, "hard", "赢得 15 场对战", 7, 15, 160),
                Fixture("weekly", 3, "normal", "造成 100 点伤害", 48, 100, 120)
            };
        }
    }

    public static Rect TitleRect(bool weekly)
    {
        return new Rect(70, weekly ? 512 : 142, 1460, 42);
    }

    public static string TitleId(bool weekly)
    {
        return PageId + (weekly ? "/title.weekly" : "/title.daily");
    }

    public static Rect TileRect(bool weekly, int index)
    {
        ValidateIndex(index);
        const float gridX = 70, gap = 14;
        float tileWidth = (1460 - gap * 3) / 4f;
        return new Rect(gridX + index * (tileWidth + gap), weekly ? 560 : 190, tileWidth, 286);
    }

    public static string TileId(bool weekly, int index)
    {
        ValidateIndex(index);
        return PageId + (weekly ? "/weekly/tile." : "/daily/tile.") + index;
    }

    public static string RerollId(string questId)
    {
        if (String.IsNullOrEmpty(questId)) throw new ArgumentException("任务 ID 不能为空。");
        return PageId + "/reroll:" + questId;
    }

    public static string ChildId(string tileId, string child)
    {
        if (String.IsNullOrEmpty(tileId) || String.IsNullOrEmpty(child))
            throw new ArgumentException("任务目标 ID 不能为空。");
        return tileId + "/" + child;
    }

    public static Rect IconRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 12, tileRect.y + 12, 32, 32);
    }

    public static Rect TileTitleRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 52, tileRect.y + 14, tileRect.width - 104, 36);
    }

    public static Rect TileTextRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 24, tileRect.y + 74, tileRect.width - 48, 80);
    }

    public static Rect RewardRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 20, tileRect.y + 162, tileRect.width - 40, 42);
    }

    public static Rect ProgressRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 50, tileRect.yMax - 57, tileRect.width - 100, 34);
    }

    public static Rect StatusRect(Rect tileRect)
    {
        return new Rect(tileRect.x + 20, tileRect.yMax - 22, tileRect.width - 40, 28);
    }

    public static Rect ButtonRect(Rect tileRect)
    {
        return new Rect(tileRect.xMax - 64, tileRect.y + 8, 52, 32);
    }

    // 预览绘制只读取夹具。生产页面须通过同一门控才能发出重抽请求。
    public static bool TryReroll(bool preview, Action reroll)
    {
        if (preview || reroll == null) return false;
        reroll();
        return true;
    }

    public static string[] TargetIds
    {
        get
        {
            var ids = new List<string> { PageId, ChildId(PageId, "background"), TitleId(false), TitleId(true) };
            string[] children = { "icon", "title", "text", "reward", "progress", "status", "button" };
            for (int period = 0; period < 2; period++)
            {
                bool weekly = period == 1;
                for (int index = 0; index < 4; index++)
                {
                    string tileId = TileId(weekly, index);
                    ids.Add(tileId);
                    foreach (string child in children) ids.Add(ChildId(tileId, child));
                }
            }
            foreach (var quest in FixtureQuests) ids.Add(RerollId(quest.quest_id));
            return ids.ToArray();
        }
    }

    static F024AccountQuest Fixture(string period, int index, string difficulty, string description, int progress, int target, int reward)
    {
        return new F024AccountQuest
        {
            quest_id = "preview-" + period + "-" + index,
            period = period,
            difficulty = difficulty,
            title = "预览任务 " + (index + 1),
            description = description,
            progress = progress,
            target = target,
            reward_gold = reward,
            completed = false,
            reroll_available = true
        };
    }

    static void ValidateIndex(int index)
    {
        if (index < 0 || index >= 4) throw new ArgumentOutOfRangeException("index");
    }
}
