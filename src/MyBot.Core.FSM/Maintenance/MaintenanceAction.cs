namespace MyBot.Core.FSM.Maintenance;

/// <summary>
/// 24 action bảo trì, giữ đúng thứ tự và giá trị của enum AutoIt.
///
/// parity: Global Enum $eMaintenance* @ MyBot.run.au3:64 — thứ tự khai báo quyết
/// định ID, nên không được chèn/xoá giữa chừng.
/// </summary>
public enum MaintenanceAction
{
    /// <summary><c>$eMaintenanceCollect</c> — thu tài nguyên.</summary>
    Collect = 0,

    /// <summary><c>$eMaintenanceCollectCCGold</c> — thu vàng clan castle.</summary>
    CollectCCGold = 1,

    /// <summary><c>$eMaintenanceCheckTombs</c> — dọn mộ.</summary>
    CheckTombs = 2,

    /// <summary><c>$eMaintenanceCleanYard</c> — dọn sân.</summary>
    CleanYard = 3,

    /// <summary><c>$eMaintenanceAchievements</c> — nhận thành tựu.</summary>
    Achievements = 4,

    /// <summary><c>$eMaintenanceMagicItems</c> — nhận vật phẩm phép thuật miễn phí.</summary>
    MagicItems = 5,

    /// <summary><c>$eMaintenanceDailyChallenge</c> — thử thách ngày.</summary>
    DailyChallenge = 6,

    /// <summary><c>$eMaintenanceReplayShare</c> — chia sẻ replay.</summary>
    ReplayShare = 7,

    /// <summary><c>$eMaintenanceNotifyReport</c> — gửi báo cáo ra ngoài.</summary>
    NotifyReport = 8,

    /// <summary><c>$eMaintenanceRequestCC</c> — xin quân clan castle.</summary>
    RequestCC = 9,

    /// <summary><c>$eMaintenanceHeroStatus</c> — kiểm tra trạng thái anh hùng.</summary>
    HeroStatus = 10,

    /// <summary><c>$eMaintenanceLaboratory</c> — phòng thí nghiệm.</summary>
    Laboratory = 11,

    /// <summary><c>$eMaintenancePetHouse</c> — nhà thú cưng.</summary>
    PetHouse = 12,

    /// <summary><c>$eMaintenanceHelperHut</c> — lều trợ thủ.</summary>
    HelperHut = 13,

    /// <summary><c>$eMaintenanceUpgradeHeroes</c> — nâng cấp anh hùng.</summary>
    UpgradeHeroes = 14,

    /// <summary><c>$eMaintenanceUpgradeBuilding</c> — nâng cấp công trình.</summary>
    UpgradeBuilding = 15,

    /// <summary><c>$eMaintenanceAutoUpgrade</c> — nâng cấp tự động.</summary>
    AutoUpgrade = 16,

    /// <summary><c>$eMaintenanceBlacksmith</c> — lò rèn.</summary>
    Blacksmith = 17,

    /// <summary><c>$eMaintenanceForge</c> — rèn vàng clan capital.</summary>
    Forge = 18,

    /// <summary><c>$eMaintenanceAutoUpgradeCC</c> — nâng cấp tự động clan capital.</summary>
    AutoUpgradeCC = 19,

    /// <summary><c>$eMaintenanceUpgradeWall</c> — nâng cấp tường.</summary>
    UpgradeWall = 20,

    /// <summary><c>$eMaintenanceClanGames</c> — clan games.</summary>
    ClanGames = 21,

    /// <summary><c>$eMaintenanceBuilderBase</c> — bảo trì builder base.</summary>
    BuilderBase = 22,

    /// <summary><c>$eMaintenanceAutoUpgradeBB</c> — nâng cấp tự động builder base.</summary>
    AutoUpgradeBB = 23
}

/// <summary>
/// Dữ liệu tĩnh của bộ bảo trì: tên action, 8 dải thời gian và 9 nhóm.
///
/// parity: <c>$g_asMaintenanceActionNames</c>, <c>$g_iMaintenance*Min/Max</c> và
/// các mảng <c>$g_aiMaintenance*</c> @ MyBot.run.au3:73-99.
/// </summary>
public static class MaintenanceData
{
    /// <summary>Số action (<c>$eMaintenanceCount</c>).</summary>
    public const int Count = 24;

    /// <summary>Dải "Fast" — action nhanh (phút).</summary>
    public const int FastMin = 2;
    /// <summary>Dải "Fast" — cận trên (phút).</summary>
    public const int FastMax = 4;

    /// <summary>Dải "Status" — kiểm tra trạng thái (phút).</summary>
    public const int StatusMin = 5;
    /// <summary>Dải "Status" — cận trên (phút).</summary>
    public const int StatusMax = 10;

    /// <summary>Dải "Upgrade" — nâng cấp (phút).</summary>
    public const int UpgradeMin = 15;
    /// <summary>Dải "Upgrade" — cận trên (phút).</summary>
    public const int UpgradeMax = 30;

    /// <summary>Dải "Cleanup" — dọn dẹp (phút).</summary>
    public const int CleanupMin = 30;
    /// <summary>Dải "Cleanup" — cận trên (phút).</summary>
    public const int CleanupMax = 60;

    /// <summary>Dải "BuilderBase" — bảo trì builder base (phút).</summary>
    public const int BuilderBaseMin = 15;
    /// <summary>Dải "BuilderBase" — cận trên (phút).</summary>
    public const int BuilderBaseMax = 30;

    /// <summary>Dải "BuilderBaseAttack" — đánh builder base (phút).</summary>
    public const int BuilderBaseAttackMin = 5;
    /// <summary>Dải "BuilderBaseAttack" — cận trên (phút).</summary>
    public const int BuilderBaseAttackMax = 10;

    /// <summary>Dải "Retry" — thử lại action lỗi (phút).</summary>
    public const int RetryMin = 1;
    /// <summary>Dải "Retry" — cận trên (phút).</summary>
    public const int RetryMax = 2;

    /// <summary>Dải "ClanGames" — clan games (phút).</summary>
    public const int ClanGamesMin = 5;
    /// <summary>Dải "ClanGames" — cận trên (phút).</summary>
    public const int ClanGamesMax = 10;

    /// <summary>Tên hàm AutoIt tương ứng từng ID (<c>$g_asMaintenanceActionNames</c>).</summary>
    public static readonly string[] ActionNames =
    {
        "Collect",
        "CollectCCGold",
        "CheckTombs",
        "CleanYard",
        "CollectAchievements",
        "CollectFreeMagicItems",
        "DailyChallenge",
        "ReplayShare",
        "NotifyReport",
        "RequestCC",
        "HeroStatus",
        "Laboratory",
        "PetHouse",
        "HelperHut",
        "UpgradeHeroes",
        "UpgradeBuilding",
        "AutoUpgrade",
        "Blacksmith",
        "ForgeClanCapitalGold",
        "AutoUpgradeCC",
        "UpgradeWall",
        "ClanGames",
        "BuilderBaseMaintenance",
        "AutoUpgradeBBMaintenance"
    };

    /// <summary><c>$g_aiMaintenanceCollect</c>.</summary>
    public static readonly MaintenanceAction[] Collect =
    {
        MaintenanceAction.Collect,
        MaintenanceAction.CollectCCGold
    };

    /// <summary><c>$g_aiMaintenanceCleanup</c>.</summary>
    public static readonly MaintenanceAction[] Cleanup =
    {
        MaintenanceAction.CheckTombs,
        MaintenanceAction.CleanYard,
        MaintenanceAction.Achievements,
        MaintenanceAction.MagicItems,
        MaintenanceAction.DailyChallenge
    };

    /// <summary><c>$g_aiMaintenanceCommunication</c>.</summary>
    public static readonly MaintenanceAction[] Communication =
    {
        MaintenanceAction.ReplayShare,
        MaintenanceAction.NotifyReport,
        MaintenanceAction.RequestCC
    };

    /// <summary><c>$g_aiMaintenanceStatus</c>.</summary>
    public static readonly MaintenanceAction[] Status =
    {
        MaintenanceAction.Laboratory,
        MaintenanceAction.PetHouse
    };

    /// <summary><c>$g_aiMaintenanceUpgrade</c>.</summary>
    public static readonly MaintenanceAction[] Upgrade =
    {
        MaintenanceAction.UpgradeBuilding,
        MaintenanceAction.Blacksmith,
        MaintenanceAction.Forge,
        MaintenanceAction.AutoUpgradeCC
    };

    /// <summary><c>$g_aiMaintenanceAllUpgrades</c>.</summary>
    public static readonly MaintenanceAction[] AllUpgrades =
    {
        MaintenanceAction.UpgradeBuilding,
        MaintenanceAction.AutoUpgrade,
        MaintenanceAction.Blacksmith,
        MaintenanceAction.Forge,
        MaintenanceAction.AutoUpgradeCC,
        MaintenanceAction.UpgradeHeroes,
        MaintenanceAction.UpgradeWall,
        MaintenanceAction.AutoUpgradeBB
    };

    /// <summary><c>$g_aiMaintenanceInitialStatus</c>.</summary>
    public static readonly MaintenanceAction[] InitialStatus =
    {
        MaintenanceAction.Laboratory,
        MaintenanceAction.PetHouse,
        MaintenanceAction.HelperHut
    };

    /// <summary><c>$g_aiMaintenanceInitialCleanup</c>.</summary>
    public static readonly MaintenanceAction[] InitialCleanup =
    {
        MaintenanceAction.Achievements,
        MaintenanceAction.MagicItems
    };

    /// <summary><c>$g_aiMaintenanceInitialUpgrade</c>.</summary>
    public static readonly MaintenanceAction[] InitialUpgrade =
    {
        MaintenanceAction.Blacksmith,
        MaintenanceAction.UpgradeHeroes,
        MaintenanceAction.UpgradeBuilding,
        MaintenanceAction.AutoUpgrade,
        MaintenanceAction.Forge,
        MaintenanceAction.AutoUpgradeBB
    };

    /// <summary>Lấy tên AutoIt của action; ID lạ trả về số.</summary>
    /// <param name="action">Action cần lấy tên.</param>
    /// <returns>Tên hàm AutoIt hoặc số ID.</returns>
    public static string NameOf(MaintenanceAction action)
    {
        int index = (int)action;
        return index >= 0 && index < ActionNames.Length ? ActionNames[index] : index.ToString();
    }
}
