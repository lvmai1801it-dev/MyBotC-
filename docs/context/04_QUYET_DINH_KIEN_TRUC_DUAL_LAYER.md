# QUYẾT ĐỊNH KIẾN TRÚC TRƯỚC KHI CODE (ARCHITECTURE DECISIONS - DUAL LAYER SPEC)

> **Phương pháp luận:** Phân tách rạch ròi 2 tầng kiến trúc:
> * **TẦNG 1: CONCEPT BẤT BIẾN (Core Architecture):** Các quyết định logic, trạng thái, ranh giới module độc lập với ngôn ngữ lập trình.
> * **TẦNG 2: TRIỂN KHAI THỰC TẾ (Implementation Layer):**
>   * **Nhánh A (AutoIt3):** Triển khai trực tiếp cho codebase hiện tại của dự án MyBot PHMOD v15.3 (Procedural + Arrays + Assign/Eval).
>   * **Nhánh B (C# WPF .NET 8):** Triển khai theo chuẩn công nghiệp phục vụ lộ trình porting (MVVM + Dependency Injection + XAML Binding + xUnit).

---

## BẢNG SO SÁNH TRADE-OFF AUTOIT3 VS C# WPF

| Khía cạnh Kỹ thuật | AutoIt3 (Codebase Hiện tại) | C# WPF (.NET 8 - Chuẩn Porting) |
| :--- | :--- | :--- |
| **Mô hình Lập trình** | Thủ tục tuần tự (Procedural, Func + Global) | Hướng đối tượng + MVVM + DI Container |
| **Giao diện (UI)** | Gọi hàm Win32 wrapper (`GUICtrlCreate*`) | XAML Data Binding + Virtualized Controls |
| **Quản lý Sự kiện** | Callback trực tiếp qua `GUICtrlSetOnEvent` | `ICommand` + `INotifyPropertyChanged` |
| **Kiểm thử (Testing)** | Không có framework chính thức, tự viết script | xUnit, Moq, FluentAssertions, Test Runner |
| **Đa luồng (Threading)** | Rất khó, chỉ giả lập qua timer / tiến trình con | `async/await`, `Task`, `CancellationToken` |
| **Quản lý Thư viện** | Copy file `.dll` vào thư mục `lib/` | NuGet Package Manager |
| **Cộng đồng & Contributor** | Rất hẹp (Ngôn ngữ ít cập nhật) | Cực kỳ rộng lớn trong thế giới nguồn mở |
| **Chi phí Thực thi** | **$0$ (Mã nguồn đang chạy thực tế)** | Cần 2 - 3 tháng tái cấu trúc hoàn chỉnh |

---

## 1. QUYẾT ĐỊNH 1: UI STATE MACHINE & CONTROL BINDING

### Tầng 1: Concept Bất Biến
* **Tách rời tuyệt đối Trạng thái Giao diện khỏi Render Control.**
* Toàn bộ trạng thái hiển thị (Visible / Enabled / Label) của các nút điều khiển được quản lý tập trung qua **Ma Trận Trạng Thái (UI State Matrix)** và bảo vệ bởi **Transition Guard** (chỉ cho phép các bước nhảy hợp lệ). Không rải `if/else` cập nhật control rải rác trong code.

```
+----------------------------------------------------------------------------------------------------+
|                                    UI STATE TRANSITION GUARD MATRIX                                |
|                                                                                                    |
|  UNINIT ──> IDLE ──> STARTING ──> SEARCHING / ATTACK ──> PAUSING ──> PAUSED ──> RESUMING           |
|               ▲                        │                                   │          │            |
|               │                        ▼                                   ▼          ▼            |
|               └──────────────────── STOPPING <─────────────────────────────┴──────────┘            |
|                                        │                                                           |
|                                        ▼                                                           |
|                                RECOVERING / ERROR ──> IDLE                                         |
+----------------------------------------------------------------------------------------------------+
```

### Tầng 2A: Triển khai trên AutoIt3 (Mã nguồn Hiện tại)
AutoIt không có `class` hay `dictionary`, do đó giải pháp tối ưu là sử dụng **Mảng 2 chiều hằng số (2D Array Matrix)** kết hợp chuỗi phân tách `"visibility|enabled|label"`:

```autoit
#include <GUIConstantsEx.au3>

; === 1. Định nghĩa Hằng số Trạng thái ===
Global Const $STATE_UNINITIALIZED = 0
Global Const $STATE_IDLE          = 1
Global Const $STATE_STARTING      = 2
Global Const $STATE_SEARCHING     = 3
Global Const $STATE_PAUSING       = 4
Global Const $STATE_PAUSED        = 5
Global Const $STATE_RESUMING      = 6
Global Const $STATE_STOPPING      = 7
Global Const $STATE_RECOVERING    = 8
Global Const $STATE_ERROR         = 9
Global Const $STATE_COUNT         = 10

Global Const $g_asStateNames[$STATE_COUNT] = [ _
    "UNINITIALIZED", "IDLE", "STARTING", "SEARCHING", "PAUSING", _
    "PAUSED", "RESUMING", "STOPPING", "RECOVERING", "ERROR"]

; === 2. Định nghĩa Chỉ số Control ===
Global Const $CTRL_START        = 0
Global Const $CTRL_STOP         = 1
Global Const $CTRL_PAUSE        = 2
Global Const $CTRL_RESUME       = 3
Global Const $CTRL_SEARCH_MODE  = 4
Global Const $CTRL_COUNT        = 5

Global $g_ahControls[$CTRL_COUNT] ; Lưu trữ Control IDs thực tế khi tạo GUI

; === 3. Ma Trận Trạng Thái Giao Diện (State Matrix: "vis|enabled|label") ===
Global Const $g_asUIStateMatrix[$STATE_COUNT][$CTRL_COUNT] = [ _
;    Start                 Stop                   Pause                  Resume                 SearchMode
    ["hide|0|",           "hide|0|",             "hide|0|",             "hide|0|",             "hide|0|"],           ; UNINITIALIZED
    ["show|1|Start",      "hide|0|",             "hide|0|",             "hide|0|",             "show|1|Search Mode"],; IDLE
    ["show|0|Starting...", "show|1|Cancel",       "hide|0|",             "hide|0|",             "hide|0|"],           ; STARTING
    ["hide|0|",           "show|1|Stop",         "show|1|Pause",        "hide|0|",             "hide|0|"],           ; SEARCHING
    ["hide|0|",           "show|1|Stop",         "show|0|Pausing...",   "hide|0|",             "hide|0|"],           ; PAUSING
    ["hide|0|",           "show|1|Stop",         "hide|0|",             "show|1|Resume",       "hide|0|"],           ; PAUSED
    ["hide|0|",           "show|1|Stop",         "hide|0|",             "show|0|Resuming...",  "hide|0|"],           ; RESUMING
    ["hide|0|",           "show|0|Stopping...",  "hide|0|",             "hide|0|",             "hide|0|"],           ; STOPPING
    ["hide|0|",           "show|1|Force Stop",   "hide|0|",             "hide|0|",             "hide|0|"],           ; RECOVERING
    ["hide|0|",           "show|1|Reset",        "hide|0|",             "hide|0|",             "hide|0|"]            ; ERROR
]

; === 4. Bảng Kiểm soát Chuyển Trạng thái Hợp lệ (Transition Guard) ===
Global Const $g_abValidTransitions[$STATE_COUNT][$STATE_COUNT] = [ _
;   UN  ID  ST  SE  PG  PD  RS  SP  RC  ER
    [0,  1,  0,  0,  0,  0,  0,  0,  0,  0], _ ; UNINITIALIZED -> IDLE
    [0,  0,  1,  0,  0,  0,  0,  0,  0,  0], _ ; IDLE -> STARTING
    [1,  0,  0,  1,  0,  0,  0,  0,  0,  1], _ ; STARTING -> IDLE | SEARCHING | ERROR
    [0,  0,  0,  0,  1,  0,  0,  1,  1,  0], _ ; SEARCHING -> PAUSING | STOPPING | RECOVERING
    [0,  0,  0,  0,  0,  1,  0,  1,  0,  0], _ ; PAUSING -> PAUSED | STOPPING
    [0,  0,  0,  0,  0,  0,  1,  1,  0,  0], _ ; PAUSED -> RESUMING | STOPPING
    [0,  0,  0,  1,  0,  0,  0,  0,  0,  1], _ ; RESUMING -> SEARCHING | ERROR
    [1,  0,  0,  0,  0,  0,  0,  0,  0,  1], _ ; STOPPING -> IDLE | ERROR
    [0,  0,  0,  1,  0,  0,  0,  0,  0,  1], _ ; RECOVERING -> SEARCHING | ERROR
    [0,  1,  0,  0,  0,  0,  0,  0,  0,  0]   ; ERROR -> IDLE
]

; === 5. Hàm Áp dụng Trạng thái Duy nhất ===
Func _ApplyUIState($iNewState)
    If $iNewState < 0 Or $iNewState >= $STATE_COUNT Then Return SetError(1, 0, 0)
    For $i = 0 To $CTRL_COUNT - 1
        Local $aTokens = StringSplit($g_asUIStateMatrix[$iNewState][$i], "|")
        If UBound($aTokens) < 4 Then ContinueLoop
        
        ; Cập nhật Ẩn/Hiện
        GUICtrlSetState($g_ahControls[$i], ($aTokens[1] == "show" ? $GUI_SHOW : $GUI_HIDE))
        ; Cập nhật Bật/Tắt
        GUICtrlSetState($g_ahControls[$i], ($aTokens[2] == "1" ? $GUI_ENABLE : $GUI_DISABLE))
        ; Cập nhật Nhãn hiển thị nếu có chỉ định
        If $aTokens[3] <> "" Then GUICtrlSetData($g_ahControls[$i], $aTokens[3])
    Next
    _GUICtrlStatusBar_SetText($g_hStatusBar, "Status : " & $g_asStateNames[$iNewState])
    Return 1
EndFunc

Func _TransitionToState($iNewState)
    If $g_abValidTransitions[$g_iCurrentState][$iNewState] == 0 Then
        SetLog("Invalid State Transition: " & $g_asStateNames[$g_iCurrentState] & " -> " & $g_asStateNames[$iNewState], $COLOR_ERROR)
        Return SetError(1, 0, 0)
    EndIf
    Local $iOldState = $g_iCurrentState
    $g_iCurrentState = $iNewState
    _ApplyUIState($iNewState)
    SetLog("STATE TRANSITION: " & $g_asStateNames[$iOldState] & " -> " & $g_asStateNames[$iNewState], $COLOR_INFO)
    Return 1
EndFunc
```

### Tầng 2B: Triển khai trên C# WPF (Tùy chọn Porting MVVM)
Trong WPF, không có khái niệm set state thủ công. Toàn bộ giao diện liên kết dữ liệu trực tiếp với `BotState` qua Computed Properties:

```csharp
public enum BotState { Uninitialized, Idle, Starting, Searching, Pausing, Paused, Resuming, Stopping, Recovering, Error }

public class MainViewModel : ObservableObject
{
    private BotState _state = BotState.Idle;
    public BotState State
    {
        get => _state;
        set {
            if (SetProperty(ref _state, value)) {
                OnPropertyChanged(nameof(IsStartVisible));
                OnPropertyChanged(nameof(IsStartEnabled));
                OnPropertyChanged(nameof(StartText));
                OnPropertyChanged(nameof(IsStopVisible));
                OnPropertyChanged(nameof(IsPauseVisible));
                OnPropertyChanged(nameof(IsResumeVisible));
                OnPropertyChanged(nameof(IsConfigEditable));
            }
        }
    }

    public Visibility IsStartVisible => (State is BotState.Idle or BotState.Starting) ? Visibility.Visible : Visibility.Collapsed;
    public bool IsStartEnabled => State == BotState.Idle;
    public string StartText => State == BotState.Starting ? "Starting..." : "Start Bot";
    public Visibility IsStopVisible => (State != BotState.Idle && State != BotState.Uninitialized) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsPauseVisible => (State is BotState.Searching or BotState.Pausing) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsResumeVisible => (State is BotState.Paused or BotState.Resuming) ? Visibility.Visible : Visibility.Collapsed;
    public bool IsConfigEditable => State is BotState.Idle or BotState.Paused or BotState.Error;

    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand PauseCommand { get; }
    public IAsyncRelayCommand ResumeCommand { get; }
}
```

---

## 2. QUYẾT ĐỊNH 2: PAUSE SEMANTICS ("SAFE-POINT PAUSE")

### Tầng 1: Concept Bất Biến
* **Cấm triệt để việc dừng đột ngột (Hard Thread Kill).**
* Khi nhận lệnh Pause, bot kích hoạt trạng thái trung gian `PAUSING`, tiếp tục vận hành cho đến khi hoàn thành xong action nguyên tử (đang thả xong đợt lính, hoặc đã bấm Return Home về an toàn tại Làng Chính), sau đó mới khóa trạng thái tại `PAUSED`.

```
[User Bấm Pause] ──> Cờ $g_bPauseRequested = True ──> UI chuyển "Status: Pausing..."
                               │
                               ▼
        ┌────────────────────────────────────────────────────────┐
        │ Kiểm tra Vị trí Thực tế của Bot:                       │
        │ - Đang thả lính? ────> Thả xong đợt lính hiện tại      │
        │ - Đang tìm kiếm? ────> Bấm Return Home về Làng         │
        │ - Đang nâng tường? ──> Xong thao tác nâng              │
        └──────────────────────────────┬─────────────────────────┘
                                       ▼
             Đã ở Làng Chính & Nhả chuột an toàn (ReleaseClicks)
                                       ▼
                   Chuyển Trạng thái: STATE_PAUSED (Hoàn tất)
```

### Tầng 2A: Triển khai trên AutoIt3
```autoit
Global $g_bPauseRequested = False

Func OnBtnPauseClicked()
    If $g_iCurrentState = $STATE_SEARCHING Then
        $g_bPauseRequested = True
        _TransitionToState($STATE_PAUSING)
    EndIf
EndFunc

Func CheckSafePointPause()
    If Not $g_bPauseRequested Then Return False
    
    ; Chỉ cho phép Pause khi bot đã ở màn hình làng chính an toàn
    If _IsAtMainVillage() Then
        ReleaseClicks() ; Đảm bảo chuột ảo đã nhả hoàn toàn
        $g_bPauseRequested = False
        _TransitionToState($STATE_PAUSED)
        SetLog("Safe-Point Reached: Bot paused safely in Village.", $COLOR_ACTION)
        
        ; Vòng lặp ngủ chờ lệnh Resume
        While $g_iCurrentState = $STATE_PAUSED
            Sleep(200)
            If $g_bRestart Or Not $g_bRunState Then ExitLoop
        WEnd
        Return True
    EndIf
    Return False
EndFunc
```

### Tầng 2B: Triển khai trên C# (Async CancellationToken)
```csharp
public async Task ExecuteCombatLoopAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await DeployTroopWaveAsync();
        
        // Kiểm tra điểm dừng an toàn
        if (_pauseRequested) {
            await ReturnHomeSafelyAsync();
            State = BotState.Paused;
            await _resumeSignal.Task; // Chờ người dùng bấm Resume
            State = BotState.Searching;
            _pauseRequested = false;
        }
    }
}
```

---

## 3. QUYẾT ĐỊNH 3: HỆ THỐNG TAB ĐỘNG (DYNAMIC TAB LOADER)

### Tầng 1: Concept Bất Biến
* **Cấm viết cứng 6 Tab trong 1 hàm đơn khối.**
* Mỗi Tab là một module tự quản lý logic nạp chậm (Lazy Loading). Khung chính nạp danh sách Tab từ một **Bảng Đăng Ký (Tab Registry)** theo thứ tự ưu tiên (Priority Index).

### Tầng 2A: Triển khai trên AutoIt3 (Kế thừa Pattern `Assign/Eval` từ MyBot Gốc)
Mã nguồn MyBot gốc đã sử dụng rất thành công pattern `Assign("ATTACKVECTOR_" & $id, ...)` để quản lý động các vector. Ta áp dụng chính xác pattern này cho hệ thống Tab:

```autoit
; Mảng lưu trữ danh sách Tab ID đã sắp xếp theo thứ tự hiển thị
Global $g_asRegisteredTabs[0]

Func RegisterTab($sTabID, $sTitleIniKey, $sDefaultTitle, $sCreateFunction, $iPriority, $bLazyLoad = True)
    Local $sPrefix = "TAB_" & $sTabID & "_"
    Assign($sPrefix & "TitleKey", $sTitleIniKey)
    Assign($sPrefix & "DefaultTitle", $sDefaultTitle)
    Assign($sPrefix & "CreateFunc", $sCreateFunction)
    Assign($sPrefix & "Priority", $iPriority)
    Assign($sPrefix & "LazyLoad", $bLazyLoad)
    Assign($sPrefix & "IsLoaded", False)

    ; Đưa vào danh sách mảng và sắp xếp chèn (Insertion Sort) theo Priority
    Local $iIndex = UBound($g_asRegisteredTabs)
    ReDim $g_asRegisteredTabs[$iIndex + 1]
    $g_asRegisteredTabs[$iIndex] = $sTabID
    _SortTabsByPriority()
EndFunc

Func _SortTabsByPriority()
    For $i = 1 To UBound($g_asRegisteredTabs) - 1
        Local $sCurrentTab = $g_asRegisteredTabs[$i]
        Local $iCurrentPrio = Eval("TAB_" & $sCurrentTab & "_Priority")
        Local $j = $i - 1
        While $j >= 0 And Eval("TAB_" & $g_asRegisteredTabs[$j] & "_Priority") > $iCurrentPrio
            $g_asRegisteredTabs[$j + 1] = $g_asRegisteredTabs[$j]
            $j -= 1
        WEnd
        $g_asRegisteredTabs[$j + 1] = $sCurrentTab
    Next
EndFunc

; Hàm dựng Header động từ Registry
Func BuildDynamicTabs($hTabContainer)
    For $i = 0 To UBound($g_asRegisteredTabs) - 1
        Local $sID = $g_asRegisteredTabs[$i]
        Local $sTitle = GetTranslatedFileIni("MBR Main GUI", Eval("TAB_" & $sID & "_TitleKey"), Eval("TAB_" & $sID & "_DefaultTitle"))
        GUICtrlCreateTabItem($sTitle)
        
        ; Nếu không Lazy Load -> Tạo Controls ngay lúc nạp
        If Not Eval("TAB_" & $sID & "_LazyLoad") Then
            Call(Eval("TAB_" & $sID & "_CreateFunc"))
            Assign("TAB_" & $sID & "_IsLoaded", True)
        EndIf
    Next
    GUICtrlCreateTabItem("") ; Đóng thẻ Tab
EndFunc

; Bắt sự kiện chuyển Tab để Lazy Load khi người dùng bấm vào
Func OnMainTabSwitched()
    Local $iCurrentTabIdx = GUICtrlRead($g_hTabMain)
    If $iCurrentTabIdx >= 0 And $iCurrentTabIdx < UBound($g_asRegisteredTabs) Then
        Local $sID = $g_asRegisteredTabs[$iCurrentTabIdx]
        If Not Eval("TAB_" & $sID & "_IsLoaded") Then
            Call(Eval("TAB_" & $sID & "_CreateFunc"))
            Assign("TAB_" & $sID & "_IsLoaded", True)
            SetDebugLog("Lazy Loaded Controls for Tab: " & $sID)
        EndIf
    EndIf
EndFunc

; Đăng ký 6 Tab chuẩn mực:
Func InitializeTabRegistry()
    RegisterTab("log",     "Tab_01", "Log",         "CreateLogTab",     10, False) ; Nạp ngay
    RegisterTab("village", "Tab_02", "Village",     "CreateVillageTab", 20, True)  ; Lazy load
    RegisterTab("attack",  "Tab_03", "Attack Plan", "CreateAttackTab",  30, True)
    RegisterTab("bot",     "Tab_04", "Bot",         "CreateBotTab",     40, True)
    RegisterTab("stats",   "Tab_04_STab_05", "Stats","CreateStatsTab",   50, True)
    RegisterTab("about",   "Tab_05", "About Us",    "CreateAboutTab",   90, False)
EndFunc
```

### Tầng 2B: Triển khai trên C# (Dependency Injection + ITab)
```csharp
public interface ITab {
    string TabId { get; }
    string TitleKey { get; }
    int Priority { get; }
    bool IsLazy { get; }
    UserControl CreateView();
}

public class TabRegistry {
    private readonly List<ITab> _tabs = new();
    public void Register(ITab tab) => _tabs.Add(tab);
    public IReadOnlyList<ITab> GetSortedTabs() => _tabs.OrderBy(t => t.Priority).ToList();
}
```

---

## 4. QUYẾT ĐỊNH 4: VỊ TRÍ BỐ TRÍ CHO TÍNH NĂNG MỚI (EQUIPMENT / PETS / SIEGE)

### Tầng 1: Concept Bất Biến
* **Giữ nguyên 6 Tab chính, tuyệt đối không tạo thêm Tab số 7 hay số 8 làm vỡ kích thước chuẩn $475 \times 650$.**
* Gom toàn bộ cấu hình Trang Bị Tướng (Equipment), Thú Cưng (Pets) và Máy Công Thành (Siege Machines) vào **Subtab `Train Army`** bên trong **Tab 3 (`Attack Plan Tab`)**.
* Hiển thị dạng **Ma trận Lưới Icon nhỏ $24 \times 24 \text{ px}$** kèm ô tích Checkbox, vừa vặn tuyệt đối trong không gian $466 \times 425 \text{ px}$.

### Tầng 2A: Triển khai trên AutoIt3 (Tích hợp trong `MBR GUI Design Child Attack - Troops.au3`)
```autoit
Func CreateArmyEquipmentAndPetsGroup($iStartX, $iStartY)
    ; Nhóm Hero Equipment (Kích thước 425x110)
    GUICtrlCreateGroup("Hero Equipment Selection", $iStartX, $iStartY, 425, 110)
    
    ; Khởi tạo danh sách ảnh Icon nhỏ 24x24 (Dùng chung ImageList chống rò rỉ GDI)
    Local $hEquipmentImageList = _GUIImageList_Create(24, 24, 5)
    _GUIImageList_AddBitmap($hEquipmentImageList, @ScriptDir & "\images\Equipment\Gauntlet.bmp")
    _GUIImageList_AddBitmap($hEquipmentImageList, @ScriptDir & "\images\Equipment\InvisVial.bmp")
    
    ; Bố trí 4 Tướng: Mỗi Tướng có 2 Slot chọn Equipment qua Combobox nhỏ
    Local $y = $iStartY + 20
    GUICtrlCreateLabel("King:", $iStartX + 10, $y + 3, 45, 15)
    Local $hCmbKingEq1 = GUICtrlCreateCombo("Giant Gauntlet", $iStartX + 60, $y, 160, 20, $CBS_DROPDOWNLIST)
    Local $hCmbKingEq2 = GUICtrlCreateCombo("Rage Vial", $iStartX + 230, $y, 160, 20, $CBS_DROPDOWNLIST)
    
    $y += 26
    GUICtrlCreateLabel("Queen:", $iStartX + 10, $y + 3, 45, 15)
    Local $hCmbQueenEq1 = GUICtrlCreateCombo("Invisibility Vial", $iStartX + 60, $y, 160, 20, $CBS_DROPDOWNLIST)
    Local $hCmbQueenEq2 = GUICtrlCreateCombo("Archer Puppet", $iStartX + 230, $y, 160, 20, $CBS_DROPDOWNLIST)
    
    GUICtrlCreateGroup("", -99, -99, 1, 1)
EndFunc
```

---

## 5. QUYẾT ĐỊNH 5: KIỂM THỬ 3-LAYER ASSERT & REPLAY DAV BYPASS

### Tầng 1: Concept Bất Biến
* **Giải quyết Mâu thuẫn DAV vs Replay:** Khi chạy video giả lập (Mock Replay) trên CI/CD, khung hình phát lại là cố định, lệnh click ảo không thể làm đổi pixel video. Do đó, Replay Driver tự động **Bypass `verify_fn`**.
* Đổi lại, hệ thống kiểm thử tự động áp dụng **Mô hình Đánh giá 3 Lớp (3-Layer Assertion)**:
  * **Lớp 1 (State Sequence Accuracy):** Tỷ lệ khung hình nhận diện đúng trạng thái phải $\ge 95\%$.
  * **Lớp 2 (Action Subsequence Match):** Kiểm tra toạ độ click thực tế của bot so với toạ độ mẫu, dung sai sai số $\le 15\text{ px}$.
  * **Lớp 3 (Causal Match):** Nghiêm cấm các thao tác click vào nút tấn công khi đang ở sai trạng thái (ví dụ: cấm click thả lính khi đang ở Làng Chính).

### Tầng 2A: Triển khai trên AutoIt3 (Kiểm thử Replay Không Cần Giả lập)
```autoit
Global $g_bReplayModeActive = False
Global $g_asRecordedClickLog[0] ; Mảng ghi nhận thao tác của bot khi chạy test

; Hàm Click kiểm chứng hỗ trợ Replay
Func ExecuteVerifiedClick($x, $y, $sExpectedChangeRegion = "")
    If $g_bReplayModeActive Then
        ; Ghi nhận toạ độ click để đối chiếu với kịch bản test
        Local $iPos = UBound($g_asRecordedClickLog)
        ReDim $g_asRecordedClickLog[$iPos + 1]
        $g_asRecordedClickLog[$iPos] = $x & "|" & $y & "|" & @MSEC
        Return True ; Bypass visual polling trong chế độ Replay
    EndIf
    
    ; Trong chế độ thật: Gửi click và kiểm tra biến đổi hình ảnh
    Click($x, $y)
    Return VerifyVisualChange($sExpectedChangeRegion)
EndFunc

; Hàm Đánh giá Lớp 2: Kiểm tra toạ độ thả lính trong CI
Func AssertActionSubsequence($aExpectedActions, $aRecordedActions, $iTolerancePx = 15)
    If UBound($aRecordedActions) < UBound($aExpectedActions) Then Return False
    
    For $i = 0 To UBound($aExpectedActions) - 1
        Local $aExp = StringSplit($aExpectedActions[$i], "|")
        Local $aAct = StringSplit($aRecordedActions[$i], "|")
        
        Local $iDist = Sqrt(($aExp[1] - $aAct[1])^2 + ($aExp[2] - $aAct[2])^2)
        If $iDist > $iTolerancePx Then
            SetLog("Action Tolerance Failed at Action #" & $i & " - Distance: " & $iDist & " px", $COLOR_ERROR)
            Return False
        EndIf
    Next
    Return True
EndFunc
```

### Tầng 2B: Triển khai trên C# xUnit
```csharp
[Theory]
[InlineData("golden_raid_th12.mkv", "golden_raid_th12.telemetry.jsonl")]
public async Task RunGoldenReplayTest(string videoPath, string telemetryPath)
{
    var mockInput = new MockInputBackend();
    var bot = new ReplayBotEngine(videoPath, mockInput);
    
    var report = await bot.ExecuteTestAsync();
    
    // Layer 1
    report.StateAccuracy.Should().BeGreaterOrEqualTo(0.95);
    // Layer 2
    mockInput.RecordedClicks.Should().MatchWithinTolerance(report.ExpectedClicks, tolerancePx: 15);
    // Layer 3
    report.IllegalStateActions.Should().BeEmpty();
}
```

---

## 6. CHIẾN LƯỢC CHUYỂN ĐỔI "STRANGLER FIG" (NẾU PORT SANG C#)

Nếu trong tương lai dự án quyết định port sang C# WPF để mở rộng cộng đồng nguồn mở, chiến lược chuyển đổi an toàn nhất được khuyến nghị:

```
┌────────────────────────────────────────────────────────────────────────┐
│               LỘ TRÌNH 5 THÁNG THEO MÔ HÌNH STRANGLER FIG              │
├────────────────────────────────────────────────────────────────────────┤
│ Tháng 1 │ Xây dựng Shell C# WPF (Khung 475x650, 6 Tab rỗng, Bottom HUD)│
│ Tháng 2 │ Port tầng Core (Screen Capture Win32/ADB, FSM, Virtual Input)│
│ Tháng 3 │ Port tầng Game Logic (Isometric Grid, Red Area, Dead Base)   │
│ Tháng 4 │ Port bộ thông dịch Attack CSV 2.0 & Hero Equipment           │
│ Tháng 5 │ Chạy song song Beta: AutoIt (Stable) ── C# WPF (Experimental)│
└────────────────────────────────────────────────────────────────────────┘
```
* **Nguyên tắc an toàn:** Trong suốt 4 tháng đầu, bản AutoIt3 hiện tại vẫn được giữ nguyên để phục vụ nhu cầu cắm bot cày tài nguyên thực tế. Không bao giờ khai tử phiên bản AutoIt khi bản C# chưa vượt qua bài test 72 giờ liên tục.

---
*Tài liệu này được xác lập làm chuẩn kiến trúc thống nhất, có giá trị định hướng trực tiếp cho cả codebase AutoIt3 hiện tại và dự án C# tương lai.*
