using System;
using System.Collections.Generic;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>Kích thước/diamond tham chiếu của một phong cảnh làng.</summary>
/// <param name="Size">Kích thước làng tham chiếu (px).</param>
/// <param name="Left">Mép trái diamond trong.</param>
/// <param name="Right">Mép phải diamond trong.</param>
/// <param name="Top">Mép trên diamond trong.</param>
/// <param name="Bottom">Mép dưới diamond trong.</param>
/// <param name="SizeAlt">Kích thước tham chiếu khi dùng bộ ảnh <c>2tree</c>.</param>
/// <param name="AdjLeft">Mở rộng trái của diamond ngoài.</param>
/// <param name="AdjRight">Mở rộng phải của diamond ngoài.</param>
/// <param name="AdjTop">Mở rộng trên của diamond ngoài.</param>
/// <param name="AdjBottom">Mở rộng dưới của diamond ngoài.</param>
public readonly record struct VillageReference(
    double Size,
    int Left,
    int Right,
    int Top,
    int Bottom,
    double SizeAlt,
    int AdjLeft,
    int AdjRight,
    int AdjTop,
    int AdjBottom);

/// <summary>Diamond tham chiếu riêng cho các phong cảnh main village đặc biệt.</summary>
/// <param name="Code">Mã phong cảnh (MS, PG, EG, SD, JU).</param>
/// <param name="Left">Mép trái.</param>
/// <param name="Right">Mép phải.</param>
/// <param name="Top">Mép trên.</param>
/// <param name="Bottom">Mép dưới.</param>
/// <param name="Size">Kích thước làng tham chiếu.</param>
/// <param name="UpperBuildings">True nếu công trình phía trên bị dịch lên.</param>
public readonly record struct CustomVillageReference(
    string Code,
    int Left,
    int Right,
    int Top,
    int Bottom,
    double Size,
    bool UpperBuildings);

/// <summary>
/// Bảng tham chiếu port nguyên từ <c>$g_afRefVillage</c>,
/// <c>$g_afRefCustomMainVillage</c> và <c>$g_asSceneryCodes</c> của
/// <c>MBR Global Variables.au3</c>.
/// </summary>
public sealed class VillageReferenceCatalog
{
    /// <summary>
    /// Mã phong cảnh theo đúng thứ tự enum <c>$eTree*</c>.
    /// Giống <c>$g_asSceneryCodes[$eTreeCount - 4]</c>, chỉ gồm phong cảnh thường
    /// (Builder Base/OTTO/Crystal/North có trong bảng kích thước nhưng không có mã scenery).
    /// </summary>
    public static readonly string[] SceneryCodes =
    {
        "DSS", "DAS", "CC", "PS", "EW", "HM", "JS", "EJ", "9C", "PG",
        "SD", "TM", "PR", "SH", "RS", "SM", "PX", "XC", "CF", "MS",
        "EM", "CS", "IT", "JO", "MT", "DA", "PA", "GC", "FS", "BK",
        "SP", "CH", "GH", "GB", "DP", "SC", "EG", "FO", "WW", "GS",
        "AN", "CD", "DO", "JU", "TW", "FI", "YS", "MI", "CM",
    };

    /// <summary>Tên đầy đủ của từng phong cảnh (dùng cho log).</summary>
    public static readonly string[] SceneryNames =
    {
        "Classic Spring", "Classic Autumn", "Clashy Construct", "Pirate Scenery", "Epic Winter",
        "Hog Mountain", "Jungle Scenery", "Epic Jungle", "9th Clashiversary",
        "Pumpkin Graveyard", "Snowy Day", "Tiger Mountain", "Primal Scenery", "Shadow Scenery",
        "Royale Scenery", "Summer Scenery", "Pixel Scenery", "10th Clashiversary",
        "Clash Fest", "Magic Scenery", "Epic Magic Scenery", "Classic Scenery", "Inferno Town",
        "Jolly Scenery", "Magic Theater Scenery",
        "Dark Ages Scenery", "Painter Scenery", "Goblin Caves Scenery", "Future Scenery",
        "Books of Clash", "Spooky Scenery", "Chess Scenery", "Ghost Scenery", "GingerBread Scenery",
        "Dragon Palace Scenery", "Space Scenery", "Egypt Scenery", "Football Scenery",
        "Wild West Scenery", "Clash Game Stadium Scenery", "Anime Scenery", "Clash of Dragons Scenery",
        "Doomed Scenery", "Justice Scenery", "Toy Workshop Scenery", "Fire and Ice Scenery",
        "Year of the Snake Scenery", "Military Scenery", "ClashMania Scenery",
        "Builder Base", "OTTO Outpost", "Crystal Caverns", "Of The North Scenery",
    };

    private static readonly VillageReference[] References =
    {
        new(499.612307505107, 34, 802, 58, 642, 488.170666746794, 50, 50, 42, 42), // DSS
        new(476.470652278333, 36, 801, 63, 633, 476.470652278333, 50, 50, 42, 42), // DAS
        new(463.064874687304, 56, 800, 68, 622, 473.183193210402, 50, 50, 42, 42), // CC
        new(487.190577721375, 35, 809, 57, 632, 487.190577721375, 50, 50, 42, 42), // PS
        new(485.292934467294, 35, 809, 57, 632, 485.292934467294, 50, 50, 42, 42), // EW
        new(471.591177471711, 40, 795, 62, 626, 471.591177471711, 50, 50, 42, 42), // HM
        new(469.503669847663, 46, 801, 65, 627, 469.503669847663, 50, 50, 42, 42), // JS
        new(481.531257240053, 35, 809, 57, 632, 481.531257240053, 50, 50, 42, 42), // EJ
        new(472.580445695883, 49, 803, 58, 625, 472.09836287867, 50, 50, 42, 42),  // 9C
        new(481.447425356988, 35, 809, 57, 632, 481.447425356988, 50, 50, 42, 42), // PG
        new(482.492164166387, 35, 809, 57, 632, 482.492164166387, 50, 50, 42, 42), // SD
        new(503.29315963308, 35, 809, 57, 632, 503.29315963308, 50, 50, 42, 42),   // TM
        new(481.049618717487, 35, 809, 57, 632, 481.049618717487, 50, 50, 42, 42), // PR
        new(486.827142073514, 35, 809, 57, 632, 486.827142073514, 50, 50, 42, 42), // SH
        new(474.160808435852, 46, 802, 61, 632, 474.160808435852, 50, 50, 42, 42), // RS
        new(462.772740076871, 55, 795, 65, 619, 462.772740076871, 50, 50, 42, 42), // SM
        new(472.211078091435, 48, 803, 66, 636, 472.211078091435, 50, 50, 42, 42), // PX
        new(473.526226121564, 55, 795, 65, 619, 473.526226121564, 50, 50, 42, 42), // XC
        new(477.718161770293, 38, 798, 60, 636, 477.718161770293, 50, 50, 42, 42), // CF
        new(497.088225054308, 42, 829, 57, 642, 497.088225054308, 50, 50, 42, 42), // MS
        new(527.91838832914, 35, 832, 58, 657, 527.91838832914, 50, 50, 42, 42),   // EM
        new(480, 35, 809, 57, 632, 480, 50, 50, 42, 42),                           // CS
        new(480, 35, 809, 57, 632, 480, 50, 50, 42, 42),                           // IT
        new(495.492313456579, 32, 808, 46, 628, 495.492313456579, 50, 50, 42, 42), // JO
        new(481.959033529279, 32, 820, 56, 646, 481.959033529279, 50, 50, 42, 42), // MT
        new(484.403614426064, 39, 825, 50, 639, 484.403614426064, 50, 50, 42, 42), // DA
        new(479.647517821756, 22, 820, 54, 650, 479.647517821756, 50, 50, 42, 42), // PA
        new(556.047580246031, 26, 838, 45, 652, 556.047580246031, 50, 50, 42, 42), // GC
        new(463.593357868925, 63, 802, 65, 622, 463.593357868925, 50, 50, 42, 42), // FS
        new(504.518620302313, 61, 824, 61, 639, 504.518620302313, 50, 50, 42, 42), // BK
        new(480.378842463205, 42, 822, 66, 654, 480.378842463205, 50, 50, 42, 42), // SP
        new(525.959020068643, 24, 812, 66, 660, 525.959020068643, 50, 50, 42, 42), // CH
        new(606.96375086645, 21, 838, 36, 650, 606.96375086645, 50, 50, 42, 42),   // GH
        new(524.998216861609, 16, 817, 61, 658, 524.998216861609, 50, 50, 42, 42), // GB
        new(525.707398061038, 30, 826, 53, 655, 525.707398061038, 50, 50, 42, 42), // DP
        new(481.05121550662, 28, 832, 50, 652, 481.05121550662, 50, 50, 42, 42),   // SC
        new(489.348532791742, 38, 834, 58, 656, 489.348532791742, 50, 50, 42, 42), // EG
        new(513.480039587398, 37, 822, 55, 645, 513.480039587398, 50, 50, 42, 42), // FO
        new(463.948923805267, 38, 800, 64, 640, 463.948923805267, 50, 50, 42, 42), // WW
        new(526.323586426313, 37, 823, 52, 642, 526.323586426313, 50, 50, 42, 42), // GS
        new(503.599873876659, 31, 825, 34, 631, 503.599873876659, 50, 50, 42, 42), // AN
        new(498.174826439333, 38, 829, 47, 638, 498.174826439333, 50, 50, 42, 42), // CD
        new(564.335102352584, 34, 834, 55, 655, 564.335102352584, 50, 50, 42, 42), // DO
        new(565.691230559734, 36, 822, 55, 645, 565.691230559734, 50, 50, 42, 42), // JU
        new(470.156228083852, 40, 826, 59, 648, 470.156228083852, 50, 50, 42, 42), // TW
        new(591.184859764333, 48, 813, 66, 642, 591.184859764333, 50, 50, 42, 42), // FI
        new(498.766631697753, 48, 834, 64, 658, 498.766631697753, 50, 50, 42, 42), // YS
        new(488.698377630365, 36, 821, 51, 640, 488.698377630365, 50, 50, 42, 42), // MI
        new(531.437084667657, 26, 811, 71, 662, 531.437084667657, 50, 50, 42, 42), // CM
        new(374.998531943809, 104, 722, 149, 611, 374.998531943809, 50, 46, 38, 42), // BB
        new(440.179472132523, 120, 732, 152, 608, 440.179472132523, 50, 46, 38, 42), // OO
        new(379.741811787463, 130, 728, 162, 608, 379.741811787463, 50, 46, 38, 42), // CR
        new(444.044042164249, 114, 739, 150, 615, 444.044042164249, 50, 46, 38, 42), // NS
    };

    /// <summary>Diamond riêng cho main village của các phong cảnh đặc biệt.</summary>
    public static readonly CustomVillageReference[] CustomMainVillages =
    {
        new("MS", 88, 778, 112, 624, 439.489958643413, true),
        new("PG", 38, 822, 48, 634, 496.873764541968, false),
        new("EG", 97, 760, 124, 614, 405.46550540893, true),
        new("SD", 40, 817, 48, 630, 490.949366617362, false),
        new("JU", 111, 765, 102, 596, 556.323458536752, true),
    };

    private readonly Dictionary<string, int> _indexByCode;

    /// <summary>Khởi tạo bảng tham chiếu.</summary>
    public VillageReferenceCatalog()
    {
        _indexByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < SceneryCodes.Length; i++)
        {
            _indexByCode[SceneryCodes[i]] = i;
        }

        // Bốn phong cảnh Builder Base nằm cuối enum $eTree*, sau 49 mã scenery.
        _indexByCode["BB"] = 49;
        _indexByCode["OO"] = 50;
        _indexByCode["CR"] = 51;
        _indexByCode["NS"] = 52;
    }

    /// <summary>Tìm chỉ số của mã phong cảnh.</summary>
    /// <param name="code">Mã, ví dụ <c>CC</c>.</param>
    /// <param name="index">Chỉ số trong bảng.</param>
    /// <returns>True nếu mã tồn tại.</returns>
    public bool TryGetIndex(string code, out int index)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            index = -1;
            return false;
        }

        return _indexByCode.TryGetValue(code, out index);
    }

    /// <summary>Lấy tham chiếu của một mã phong cảnh.</summary>
    /// <param name="code">Mã phong cảnh.</param>
    /// <returns>Tham chiếu.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Mã không có trong bảng.</exception>
    public VillageReference Get(string code)
    {
        if (!TryGetIndex(code, out int index))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Mã phong cảnh không có trong bảng.");
        }

        return References[index];
    }

    /// <summary>
    /// Kích thước làng tham chiếu, ưu tiên bảng custom main village như
    /// <c>IsCustomScenery</c> của AutoIt.
    /// </summary>
    /// <param name="code">Mã phong cảnh của cây.</param>
    /// <param name="secondTreeSet">True khi dùng bộ ảnh <c>2tree</c>.</param>
    /// <returns>Kích thước tham chiếu; 0 nếu không xác định.</returns>
    public double GetReferenceSize(string code, bool secondTreeSet)
    {
        if (TryGetCustom(code, out CustomVillageReference custom))
        {
            return custom.Size;
        }

        if (!TryGetIndex(code, out int index))
        {
            return 0;
        }

        VillageReference reference = References[index];
        return secondTreeSet ? reference.SizeAlt : reference.Size;
    }

    /// <summary>True nếu mã là một trong các main village đặc biệt.</summary>
    /// <param name="code">Mã phong cảnh.</param>
    /// <returns>True nếu dùng bảng custom.</returns>
    public bool IsCustomMainVillage(string code)
    {
        return TryGetCustom(code, out _);
    }

    private static bool TryGetCustom(string code, out CustomVillageReference custom)
    {
        foreach (CustomVillageReference candidate in CustomMainVillages)
        {
            if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                custom = candidate;
                return true;
            }
        }

        custom = default;
        return false;
    }
}
