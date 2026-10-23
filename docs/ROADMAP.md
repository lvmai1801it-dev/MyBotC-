# ROADMAP — Port MyBot PHMOD v15.3 sang C#

> **Cập nhật:** 2026-10-23
> **Điểm xuất phát:** build `0 warning / 0 error`, 371 test PASS, GUI 40 màn hình / 2.854 control đã xong,
> vòng bot đã chạy `runBot` (init + main loop, maintenance + idle) nhưng các nhánh M3/M4 còn `StubStep` và Collect chưa nạp template `imgxml`.
> **Nguồn số liệu:** `C:\Users\PC\Downloads\MyBot PHMOD v15.3\MyBot\MyBot_PHMOD_v15.3_Analysis_Report.md` §12
> (include graph 332 file / 120.705 dòng,
> 181 `DllCall`, 29 export `MyBot.run.dll`, 24 maintenance action, ADB shell thường trú, ImgLoc `.xml` đóng gói).

---

## 0. Cách dùng tài liệu này

- Mỗi dòng trong bảng task là **một đơn vị công việc giao được cho một phiên làm việc**, có tham chiếu file AutoIt gốc để đối chiếu.
- Cột **Trạng thái**: `⏳` chưa làm · `🚧` đang làm · `✅` xong · `⛔` bị chặn.
- Cột **Effort** là *tương đối*, không phải cam kết thời gian:
  `S` < 1 ngày · `M` 1–3 ngày · `L` ~1 tuần · `XL` > 1 tuần.
- Khi xong một task: đổi trạng thái ở đây **và** cập nhật `PROGRESS.md` (quy tắc §6 của file đó).

---

## 1. Điểm xuất phát — cái gì đã có thật

| Tầng | Trạng thái | Bằng chứng trong `src/` |
|---|---|---|
| Hạ tầng: DI, config JSON + schema, Serilog, FSM, watchdog, profile lock | ✅ | `MyBot.Core*`, `MyBot.App/Startup.cs` |
| Giả lập: dò / khởi động / dùng lại MEmu 9 & BlueStacks5 | ✅ | `Core.Capture/Emulator*` |
| Chụp + input: ADB screencap, GDI, PostMessage, `input roll` | ✅ | `Core.Capture`, `Core.Input` |
| Vision: OpenCV template matching, ImgLoc native, OCR native | ✅ nền | `Core.CV`, `native/lib` |
| Hình học: red area, dropline, cluster, diamond, base classifier | ✅ | `Game.CoC/Math` |
| Vào làng: MainScreenGuard, recovery, zoom-out, căn giữa, Town Hall lần đầu | ✅ | `Game.CoC/State`, `Game.CoC/Village` |
| GUI: 40 màn / 2.854 control, i18n | ✅ | `MyBot.UI/Gui` |
| **Vòng bot** | ⚠️ chỉ quan sát | `MainViewModel.BotLoopAsync` |
| Train · Donate · Upgrade · Search · Attack · Clan Games | ❌ **0 dòng logic** | chỉ có nhãn GUI + i18n |

**Kết luận:** hạ tầng và "giác quan" của bot đã xong. Toàn bộ phần *hành động* còn trắng.

---

## 2. Nguyên tắc thứ tự

1. **Port theo vòng `runBot`, không port theo chiều rộng module.** Mỗi task phải cắm được vào vòng lặp đang chạy, không phải thư viện mồ côi.
2. **Runtime parity trước gameplay parity.** AutoIt dựa vào một console ADB thường trú + framing theo prompt. Nếu vẫn gọi `adb` từng lệnh thì mỗi action chậm 200–800 ms và timeout sẽ lệch — sửa trước khi viết 40 action.
3. **Thứ tự giá trị tăng dần:** thu hoạch → train → đánh → bảo trì. Attack là phần lớn nhất (15.331 dòng, 62 file) nên làm **sau cùng** nhưng không được bỏ.
4. **Không đánh dấu ✅ nếu chưa chạy trên MEmu thật.** Test xanh chỉ là điều kiện cần.
5. **Một task = một hành vi có thể quan sát trong log.**

---

## 3. Milestone

| M | Tên | Mục tiêu quan sát được | Task |
|---|---|---|---|
| **M0** | Nền & runtime parity | Click/kéo/chụp ổn định, ADB thường trú, CI sạch | TD-1…TD-7, RT-1…RT-5 |
| **M1** | Bot có hành động đầu tiên | Bấm Start → bot tự thu hoạch, nhặt quà, dọn mộ, chạy maintenance timer | BOT-1…BOT-3, V-01…V-04 |
| **M2** | Tự train quân | Bot train đủ quân theo cấu hình rồi chờ | AR-1…AR-6 |
| **M3** | Tìm trận & đánh | Bot search theo điều kiện, deploy, kết thúc trận, về làng | SE-1…SE-4, AT-1…AT-7 |
| **M4** | Bảo trì đầy đủ | Nâng cấp, donate, lab, clan games chạy theo lịch | UP-1…UP-4, DN-1, CG-1…CG-2, V-05…V-06 |
| **M5** | Vận hành nhiều tài khoản | Thông báo ra ngoài, đổi acc, nhiều instance | NT-1…NT-2, AC-1, MB-1, DF-1 |

---

## 4. M0 — Nền & runtime parity

### 4.1 Nợ kỹ thuật

| ID | Task | Ghi chú / tham chiếu | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| TD-1 | Xoá `MyBot.ZoomKit` (fork độc lập, không merge được) | Trùng 5 class với `Game.CoC/State/Zoom` nhưng dùng bộ abstraction riêng (`ITileSearcher`, `IZoomLogger`…); không project nào tham chiếu. Đã sao lưu ra `F:\_backup_ZoomKit_2026-10-23` rồi xoá khỏi `src/`, `tests/`, `MyBot.sln` | S | — | ✅ |
| TD-2 | Dọn 10 file `Class1.cs` | Đã xoá cả 10. Giữ 6 project `UI.Tabs.*` làm chỗ đặt view cho M1–M4; không sửa `.csproj`/`.sln` | S | — | ✅ |
| TD-3 | `StateClassifier` dùng template thật | Hiện là `Mat` dummy 10×10 → không phân loại được màn hình thật. Lấy từ `imgxml/` | M | — | ⏳ |
| TD-4 | Đóng 3 lệch GUI đã đo | Đã xong: icon tab 16×16 cho 14 tab control (`Bind_ImageList`, `MBR GUI Control.au3:2053`), status bar 472×23 neo đáy panel Bottom bind `StatusText` (`_GUICtrlStatusBar_Create`, `MBR GUI Design.au3:304`), biến thể title bar theo `botDesignFlags` bit 0 (`$GUI_MAIN_TOP` 5 vs 23, `MBR GUI Design Mini.au3:126`). 6 test UI mới | S | — | ✅ |
| TD-4b | Nối `general.botDesignFlags` từ config vào GUI | `AppConfig`/schema chưa có trường này nên `MainWindow.BotDesignFlags` đang **hardcode 3** (delta của TD-4). Cần: thêm field vào `AppConfig` + `config-schema.json`, đọc trong `ConfigManager`, gán `MainWindow.BotDesignFlags` trước `Show()`. Ref: `readConfig.au3:331`, `MBR GUI Design Mini.au3:126` | S | TD-4 | ⏳ |
| TD-5 | 299 control tại `(-99,-99)` | Chủ yếu là lưới donate sinh lúc chạy → dựng khi làm DN-1 | S | DN-1 | ⏳ |
| TD-6 | Sắp xếp lại test project | `Game.CoC.Tests` và `Integration.Tests` chỉ có `UnitTest1`; 34 file logic CoC đang test gián tiếp trong `Core.Tests` | M | — | ⏳ |
| TD-7 | Harness golden-frame | Lưu frame MEmu 860×732 + kết quả AutoIt (score ImgLoc, text OCR) làm fixture, so với bản C# | M | — | ⏳ |

### 4.2 Runtime

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| RT-1 | **`PersistentAdbShell`**: console thường trú, đọc theo prompt, timeout mặc định 3.000 ms, đếm lỗi → restart daemon | ✅ `src/MyBot.Core.Capture/Adb/PersistentAdbShell.cs` + 5 test trên fake adb. **Integration gap:** chưa wire vào `AdbScreencapCaptureBackend`/`MainViewModel` (RT-1b hoặc gộp BOT-2) | M | — | ✅ |
| RT-2 | Kênh **minitouch** riêng | `Android.au3:2445`; 21 file `.minitouch`. Cần cho kéo chính xác (dropline, zoom, center) | M | — | ⏳ |
| RT-3 | Lớp `Pixels`: `_PixelSearch`, `_ColorCheck`, `_MultiPixelSearch`, `_CaptureRegion`, `isInsideDiamond` | `functions/Pixels/*` (8 file) | M | — | ⏳ |
| RT-4 | Kiểm tra sức khoẻ Android: page error, time lag, điều kiện reboot, distributors | `Android/checkAndroidPageError.au3`, `checkAndroidTimeLag.au3`, `CheckAndroidRebootCondition.au3`, `Distributors.au3` | M | RT-1 | ⏳ |
| RT-5 | Primitive click/drag parity: `Click`, `ClickDrag`, `FindAButton`, `IsPage` (offset ngẫu nhiên + delay) | `Other/Click.au3`, `ClickDrag.au3`, `FindAButton.au3`, `IsPage.au3` | M | RT-1 | ⏳ |

---

## 5. M1 — Bot có hành động đầu tiên

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| BOT-1 | **`MaintenanceScheduler`**: 24 action ID, 8 dải thời gian, 9 nhóm, `_ArrayShuffle` có seed + log, timer riêng từng action | ✅ `src/MyBot.Core.FSM/Maintenance/` (enum + data + scheduler) + 10 test. **Chưa wire** vào `MainViewModel` (BOT-2) | L | — | ✅ |
| BOT-2 | **`runBot` vòng chính** — thay vòng quan sát hiện tại bằng: Main Screen → maintenance action → idle action, giữ nguyên thứ tự AutoIt | ✅ `src/MyBot.UI/BotLoop/` (`BotLoop` + `BotLoopContext` + 4 step) + 33 test; `MainViewModel` chỉ còn setup + callback. **Gap:** M3/M4 còn stub, Collect chưa nạp template, chưa chạy MEmu | L | BOT-1, RT-1 | ✅ |
| BOT-3 | Ngữ nghĩa pause/resume/stop xuyên action | `Other/TogglePause.au3`, `_SleepStatus.au3`, `BotCommand.au3` | M | BOT-2 | ⏳ |
| V-01 | **Collect đầy đủ** (thay `CollectorHarvester` tối giản): tap mỏ theo `imgxml`, chờ animation, chống tap trùng, xử lý kho đầy | ✅ `Game.CoC/Village/CollectorHarvester.cs` + `CollectorResult.cs` + 7 test; port `Collect.au3:17-84`. **Gap:** chưa port `CollectLootCart`/`TreasuryCollect`, chưa wire `runBot` | M | RT-5 | ✅ |
| V-02 | Quà & mốc: `CollectAchievements`, `FreeMagicItems`, `TreasuryCollect`, `StarBonus` | `Village/CollectAchievements.au3`, `FreeMagicItems.au3`, `TreasuryCollect.au3`, `StarBonus.au3` | M | V-01 | ⏳ |
| V-03 | Dọn chướng ngại: mộ, cây, đá | `Image Search/CheckTombs.au3` + `imgxml` obstacle | S | RT-5 | ⏳ |
| V-04 | Khiên: `chkShieldStatus`, `BreakPersonalShield` | `Village/chkShieldStatus.au3`, `BreakPersonalShield.au3` | S | BOT-2 | ⏳ |

> **Mốc kiểm chứng M1:** bấm Start trên MEmu, log hiện chuỗi `[Maintenance] Chọn action …` → bot tự thu hoạch → tự chạy lại sau 2–4 phút theo dải `Fast`.

---

## 6. M2 — Tự train quân

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| AR-1 | Mở tổng quan quân + đọc sức chứa (OCR) | `CreateArmy/openArmyOverview.au3`, `checkArmyCamp.au3` | M | RT-5 | ⏳ |
| AR-2 | `TrainIt` / `TrainClick` + cử chỉ **nhấn giữ** (long press) | `CreateArmy/TrainIt.au3`, `TrainClick.au3` | M | AR-1, RT-2 | ⏳ |
| AR-3 | `TrainSystem` + `QuickTrain` + `DoubleTrain` | `CreateArmy/TrainSystem.au3`, `QuickTrain.au3`, `DoubleTrain.au3` | L | AR-2 | ⏳ |
| AR-4 | Đọc quân hiện có: troops / spells / heroes / siege / CC | `CreateArmy/getArmy*.au3` (7 file) | L | AR-1 | ⏳ |
| AR-5 | Chờ train xong: `CheckFullArmy`, `SmartWait4Train` | `CreateArmy/CheckFullArmy.au3`, `SmartWait4Train.au3` | M | AR-3 | ⏳ |
| AR-6 | Quân siege + phép | `CreateArmy/TrainSiege.au3`, `getArmyCCSpells.au3` | M | AR-3 | ⏳ |

---

## 7. M3 — Tìm trận & đánh ⚠️ phần lớn nhất

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| SE-1 | **VillageSearch + PrepareSearch**: bấm Attack, chọn chế độ, vào hàng đợi | `Search/VillageSearch.au3`, `PrepareSearch.au3` | L | RT-4 | ⏳ |
| SE-2 | Đọc tài nguyên đối thủ + điều kiện lặp: `GetResources`, `CompareResources`, `multiSearch`, `WaitForClouds` | `Search/GetResources.au3`, `CompareResources.au3`, `multiSearch.au3`, `WaitForClouds.au3` | L | SE-1 | ⏳ |
| SE-3 | Phân loại base đối thủ: `WeakBase`, `checkDeadBase`, `imglocCheckWall` | `Search/WeakBase.au3`, `Image Search/checkDeadBase.au3`, `imglocCheckWall.au3` | M | SE-1 | ⏳ |
| SE-4 | Trạng thái search: `IsSearchModeActive`, `IsSearchAttackEnabled`, `CheckZoomOut` | `Search/*` | S | SE-1 | ⏳ |
| AT-1 | `PrepareAttack` + `GetAttackBar` (đọc thanh quân) | `Attack/PrepareAttack.au3`, `GetAttackBar.au3` | L | AR-4 | ⏳ |
| AT-2 | **Deploy engine**: chọn điểm theo dropline, kiểm tra red area, nhịp deploy, kéo thả | `Attack/Attack Algorithms/`, `RedArea/` | XL | AT-1, RT-2 | ⏳ |
| AT-3 | `AttackCycle` + điều kiện kết thúc + `ReturnHome` | `Attack/AttackCycle.au3`, `ReturnHome.au3` | L | AT-2 | ⏳ |
| AT-4 | Script CSV: nạp `AttackCSV/*.csv` + `ScriptRotation` | `Attack/AttackCSV/`, `ScriptRotation.au3` | L | AT-2 | ⏳ |
| AT-5 | Thuật toán cao cấp: SmartFarm, AllTroops… | `Attack/Attack Algorithms/` | XL | AT-3 | ⏳ |
| AT-6 | Báo cáo trận: `AtkReport`, `AttackStats` | `Attack/AtkReport.au3`, `AttackStats.au3` | M | AT-3 | ⏳ |
| AT-7 | Builder Base + Clan Capital attack (spam) | `Attack/BuilderBase/`, `Attack/ClanCapital/`, `Village/CCSpam.au3` | M | AT-3 | ⏳ |

---

## 8. M4 — Bảo trì đầy đủ

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| UP-1 | `LocateUpgrade` + `UpgradeBuilding`: chọn công trình, đọc giá, xác nhận, xử lý thiếu tài nguyên | `Village/LocateUpgrade.au3`, `UpgradeBuilding.au3` | L | RT-5 | ⏳ |
| UP-2 | `UpgradeHeroes`, `UpgradeWall`, `Laboratory` | `Village/UpgradeHeroes.au3`, `UpgradeWall.au3`, `Laboratory.au3` | L | UP-1 | ⏳ |
| UP-3 | `Auto Upgrade` — lịch nâng cấp theo rule | `Village/Auto Upgrade.au3` | L | UP-2 | ⏳ |
| UP-4 | `PetHouse`, `Blacksmith`, `HelperHut` | `Village/PetHouse.au3`, `Blacksmith.au3`, `HelperHut.au3` | M | UP-1 | ⏳ |
| DN-1 | **`DonateCC`** + `DonateCCWBL` + dựng 299 control lưới donate | `Village/DonateCC.au3`, `DonateCCWBL.au3`; TD-5 | L | AR-4 | ⏳ |
| CG-1 | Clan Games + Personal Challenges | `Village/Clan Games/`, `Village/Personal Challenges/` | M | BOT-2 | ⏳ |
| CG-2 | Clan Capital | `Village/ClanCapital.au3` | M | AT-7 | ⏳ |
| V-05 | `RequestCC` (xin quân vào thành clan) | `Village/RequestCC.au3` | S | DN-1 | ⏳ |
| V-06 | Báo cáo làng + idle: `ProfileReport`, `VillageReport`, `AddIdleTime` | `Village/*.au3` | S | BOT-2 | ⏳ |

---

## 9. M5 — Vận hành nhiều tài khoản

| ID | Task | Tham chiếu AutoIt | Effort | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| NT-1 | Thông báo: `Notify` + API client (Telegram/Discord) | `Village/Notify.au3`, `Other/Api.au3`, `ApiClient.au3`, `ApiHost.au3` | M | BOT-2 | ⏳ |
| NT-2 | Thống kê & xác thực forum (native) | `Other/UpdateStats.au3`, `ForumAuthentication.au3`; export `CheckForumAuthentication`/`ForumLogin` | M | RT-1 | ⏳ |
| AC-1 | Đổi tài khoản trong game | `Village/SwitchAccount.au3`, `SwitchAccountVariablesReload.au3` | M | BOT-2 | ⏳ |
| MB-1 | MultiBot / Watchdog / WMI: IPC, broadcast, restart khi treo | `MultiBot.au3`, `MyBot.run.Watchdog.au3`, `MyBot.run.Wmi.au3`, `Other/Synchronization.au3` | L | BOT-2 | ⏳ |
| DF-1 | SQLite lưu loot + thống kê trận (`MyBot.Core.Data` đang rỗng) | — | M | AT-6 | ⏳ |

---

## 10. Định nghĩa "Done"

Một task chỉ được đánh ✅ khi thoả **tất cả**:

1. `dotnet build MyBot.sln` → 0 warning / 0 error (repo bật `TreatWarningsAsErrors`).
2. Có test mới cho phần logic thuần (không cần thiết bị); integration test gắn tag nếu cần MEmu.
3. Đã chạy trên MEmu thật (hoặc BlueStacks5) và log chứng minh hành vi — dán log vào `PROGRESS.md`.
4. Không dùng `Thread.Sleep`; mọi delay qua `ITimeProvider` / `HumanDelay`.
5. Task được ghi vào `PROGRESS.md` §3 và dòng trạng thái ở đây được cập nhật.

---

## 11. Rủi ro đã biết

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|---|---|---|
| ImgLoc `.xml` là **định dạng đóng** (1 blob base64, không phải XML) | Không thể đọc template bằng C# thuần; mọi thứ phụ thuộc `MyBot.run.dll` x86 | Giữ P/Invoke x86 (đang chạy được); quyết định P/Invoke vs port lại nằm ở báo cáo §12.4 |
| `MyBot.run.dll` 32-bit | Toàn solution kẹt x86 | Đã chấp nhận; không thêm package chỉ có x64 |
| 100 tên hàm trùng giữa các executable (`SetLog` 4 bản…) | Port nhầm bản → sai hành vi | Luôn lấy theo **closure của entry point**, không theo tên file |
| Template gắn với phiên bản game (v15.3) | Game update → vỡ vision | TD-7 golden-frame + tách `imgxml` ra khỏi logic |
| Attack CSV + `Attack Algorithms` chưa phân tích sâu | AT-4/AT-5 phải đọc lại source trước khi port | Dành 1 phiên khảo sát trước khi bắt đầu M3 |
| AutoIt không có step debugger | So sánh hành vi khó | Golden-frame + log song song (bản AutoIt chạy được, xem `docs/context/`) |

---

## 12. Việc nên làm ngay

1. **TD-1 + TD-2 + TD-4** ✅ — nợ kỹ thuật và GUI đã đóng; không còn việc dọn dẹp chặn đường.
2. **RT-1** ✅ — `PersistentAdbShell` đã có; việc nối vào capture/Vòng bot để lại cho RT-1b hoặc BOT-2.
3. **BOT-1** ✅ — `MaintenanceScheduler` (24 action, 8 dải, 9 nhóm, seed + log); **V-01** ✅ — `CollectorHarvester` đầy đủ (tap mỏ, dedupe, kho đầy, delay qua `ITimeProvider`); **BOT-2** ✅ — `runBot` đã nối scheduler + harvester vào vòng chạy với callback từ `MainViewModel`. Tiếp theo: **RT-5/V-02** (nạp template cho Collect) và **BOT-3** (pause/resume/stop).

> Task `N5` trong `PROGRESS.md` **= BOT-2** của roadmap này.
