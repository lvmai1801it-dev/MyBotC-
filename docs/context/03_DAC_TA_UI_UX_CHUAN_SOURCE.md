# ĐẶC TẢ CHI TIẾT GIAO DIỆN & TRẢI NGHIỆM NGƯỜI DÙNG (UI/UX SPECIFICATION) MYBOT PHMOD V15.3

> **Nguyên tắc biên soạn:** Phân tích **100% dựa trên mã nguồn thực tế** trong thư mục `COCBot/GUI/` (gồm 60 file AutoIt GUI Design và GUI Control). Không thêm bớt hay suy đoán giao diện ngoài phạm vi mã nguồn của dự án.

---

## MỤC LỤC TỔNG QUAN

1. [PHẦN 1: BỐ CỤC KHUNG CỬA SỔ & CƠ CHẾ HIỂN THỊ (SHELL & MODES)](#phần-1-bố-cục-khung-cửa-sổ--cơ-chế-hiển-thị-shell--modes)
   - 1.1. Kích thước Cửa sổ & Tọa độ Khung (Normal Mode vs Mini Mode)
   - 1.2. Thanh Tiêu đề Tùy biến (Custom Title Bar) & Các Nút Điều khiển Shell
   - 1.3. Banner Nhận diện & Liên kết URL (Branding Banners)
2. [PHẦN 2: BẢNG ĐIỀU KHIỂN ĐÁY DÙNG CHUNG (BOTTOM CONTROL PANEL)](#phần-2-bảng-điều-khiển-đáy-dùng-chung-bottom-control-panel)
   - 2.1. Cụm Nút Điều khiển Vận hành Chính (Start / Stop / Pause / Resume / Search Mode)
   - 2.2. Các Phím Tắt Tác Vụ Nhanh (Direct, Rush, BBSpam, CCSpam)
   - 2.3. Bảng Giám sát Trạng thái Trực quan (Hero Shields, Laboratory, Pet House)
   - 2.4. Đồng hồ Đo Tài nguyên Tức thời & Lũy kế (Live Resource Ticker)
   - 2.5. Thanh Trạng thái Hệ thống (Status Bar)
3. [PHẦN 3: CẤU TRÚC CHI TIẾT 6 TAB CHÍNH (MAIN TABS ARCHITECTURE)](#phần-3-cấu-trúc-chi-tiết-6-tab-chính-main-tabs-architecture)
   - 3.1. Tab 1: Nhật Ký Vận Hành (Log Tab)
   - 3.2. Tab 2: Quản Lý Làng Chính (Village Tab - 4 Subtabs)
   - 3.3. Tab 3: Kế Hoạch Tấn Công (Attack Plan Tab - Cấu trúc Lồng 3 Cấp)
   - 3.4. Tab 4: Cấu Hình Bot (Bot Tab - 4 Subtabs)
   - 3.5. Tab 5: Thống Kê Chi Tiết (Stats Tab)
   - 3.6. Tab 6: Thông Tin Dự Án (About Us Tab)
4. [PHẦN 4: HỆ THỐNG CỬA SỔ PHỤ & HỘP THOẠI MODAL (POPUPS & SUB-WINDOWS)](#phần-4-hệ-thống-cửa-sổ-phụ--hộp-thoại-modal-popups--sub-windows)
   - 4.1. Màn hình Khởi động Tiến trình (Splash Screen)
   - 4.2. Cửa sổ Thiết lập Thứ tự Thả quân Tùy biến (Drop Order GUI)
   - 4.3. Khay Hệ thống Windows (System Tray Context Menu)
5. [PHẦN 5: CƠ CHẾ BẢN ĐỊA HÓA & PHẢN HỒI SỰ KIỆN (I18N & EVENT MODEL)](#phần-5-cơ-chế-bản-địa-hóa--phản-hồi-sự-kiện-i18n--event-model)
   - 5.1. Hệ thống Đa ngôn ngữ qua File `.ini` (`GetTranslatedFileIni`)
   - 5.2. Mô hình Xử lý Sự kiện (AutoIt `GUIOnEventMode`)
6. [PHẦN 6: DANH MỤC TASK NHIỆM VỤ THI CÔNG GIAO DIỆN (UI/UX TASK BREAKDOWN)](#phần-6-danh-mục-task-nhiệm-vụ-thi-công-giao-diện-uiux-task-breakdown)

---

## PHẦN 1: BỐ CỤC KHUNG CỬA SỔ & CƠ CHẾ HIỂN THỊ (SHELL & MODES)

*(Trích xuất trực tiếp từ `COCBot/MBR GUI Design.au3` và `COCBot/MBR GUI Design Mini.au3`)*

```
+-------------------------------------------------------------------+
| [Logo/Title]  MyBot.run v15.3          [◄/► Dock] [▄/█ Mini] [_] [X]|
+-------------------------------------------------------------------+
|  [ Banner Ảnh Header 475x67 ] - MBR Logo                          |
|  [ Banner URL 475x13 ] - Forum Link                               |
+-------------------------------------------------------------------+
| [Log] | [Village] | [Attack Plan] | [Bot] | [Stats] | [About Us]  |
|-------------------------------------------------------------------|
|                                                                   |
|                   VÙNG NỘI DUNG TỪNG TAB (CHILD GUI)              |
|                   (Kích thước: W=466, H=425)                      |
|                                                                   |
+-------------------------------------------------------------------+
| [Start/Stop] | [Heroes] K Q P W C D  | [Stats Ticker: G, E, DE]   |
| [Direct/Rush]| [Lab / Pet Timers]    | [Builders, Trophies, Gems] |
+-------------------------------------------------------------------+
| Status: Idle                                                      |
+-------------------------------------------------------------------+
```

### 1.1. Kích thước Cửa sổ & Tọa độ Khung
* **Chế độ Bình thường (Normal GUI Mode - `$g_iGuiMode = 1`):**
  * Chiều rộng toàn bộ cửa sổ: `$_GUI_MAIN_WIDTH = 475 px`
  * Chiều cao toàn bộ cửa sổ: `$_GUI_MAIN_HEIGHT = 650 px`
  * Chiều cao lề đỉnh (Title bar margin): `$_GUI_MAIN_TOP = 25 px` (nếu dùng Custom Title Bar) hoặc `5 px` (nếu dùng Standard Caption).
  * Vùng chứa Tab con (Child tab frame): `Width = 466 px`, `Height = 425 px` (`$g_iSizeWGrpTab1`, `$g_iSizeHGrpTab1`).
  * Chiều cao bảng điều khiển đáy (Bottom Panel): `$_GUI_BOTTOM_HEIGHT = 115 px`.
* **Chế độ Thu nhỏ (Mini GUI Mode - `$g_iGuiMode = 2`):**
  * `$_MINIGUI_MAIN_WIDTH = 475 px`
  * `$_MINIGUI_MAIN_HEIGHT = 205 px`
  * Ẩn toàn bộ 6 Tab cấu hình, chỉ hiển thị cửa sổ dòng Log rút gọn và Bảng điều khiển đáy để tối ưu diện tích màn hình.

---

### 1.2. Thanh Tiêu đề Tùy biến (Custom Title Bar) & Các Nút Điều khiển Shell
Được tạo trong hàm `CreateMainGUIControls` (`MBR GUI Design.au3`, dòng 184–260):
* Cửa sổ chứa nút tiêu đề: `$g_hFrmBotButtons` (`Width = $_GUI_MAIN_BUTTON_COUNT * 18, Height = 18`).
* Danh sách 5 nút chức năng trên thanh tiêu đề:
  1. **Nút Thu/Phóng Cửa sổ Dock (`$g_hLblBotShrink` / `$g_hLblBotExpand`):** Ký tự `◄` (U+25C4) và `►` (U+25BA). Tác dụng: Co ngắn hoặc bung rộng cửa sổ bot khi được ghép dính (dock) vào cửa sổ giả lập.
  2. **Nút Đổi Chế độ Mini/Normal (`$g_hLblBotMiniGUI` / `$g_hLblBotNormalGUI`):** Ký tự `▄` (U+2584) và `█` (U+2588). Tác dụng: Chuyển đổi qua lại giữa giao diện đầy đủ 650px và giao diện mini 205px.
  3. **Nút Thu nhỏ Windows (`$g_hLblBotMinimize`):** Ký tự `̶` (Gạch ngang). Tác dụng: Thu nhỏ bot xuống Taskbar hoặc System Tray.
  4. **Nút Đóng (`$g_hLblBotClose`):** Ký tự `×` (Dấu nhân đỏ, nền `0xFF4040`, chữ trắng). Tác dụng: Thoát chương trình an toàn.
  5. **Nhãn Tiêu đề Bot (`$g_hLblBotTitle`):** Hiển thị chuỗi `$g_sBotTitle`, Font `Segoe UI`, Size 11, nền trắng `0xFFFFFF`, hỗ trợ kéo rê cửa sổ (`BotMoveRequest`).

---

### 1.3. Banner Nhận diện & Liên kết URL (Branding Banners)
* **Ảnh Logo chính (`$g_hFrmBot_MAIN_PIC`):** Tọa độ `X=0, Y=$_GUI_MAIN_TOP, W=475, H=67`, nạp từ `@ScriptDir & "\images\logo.png"`. Click vào banner hỗ trợ kéo di chuyển cửa sổ.
* **Ảnh URL diễn đàn (`$g_hFrmBot_URL_PIC`):** Tọa độ `X=0, Y=$_GUI_MAIN_TOP + 67, W=475, H=13`, trỏ về website cộng đồng, con trỏ chuột tự đổi thành bàn tay (`Cursor = 0`).

---

## PHẦN 2: BẢNG ĐIỀU KHIỂN ĐÁY DÙNG CHUNG (BOTTOM CONTROL PANEL)

*(Trích xuất trực tiếp từ `COCBot/GUI/MBR GUI Design Bottom.au3`)*  
Bảng này nằm cố định ở đáy giao diện (`Y = $_GUI_MAIN_HEIGHT - $_GUI_BOTTOM_HEIGHT`), luôn hiển thị xuyên suốt trên tất cả các Tab.

### 2.1. Cụm Nút Điều khiển Vận hành Chính (Nhóm bên trái: `X=10, Y=10`)
* **Nút Start Bot (`$g_hBtnStart`):** Kích thước $75 \times 35 \text{ px}$, màu nền xanh lá `0x5CAD85`. Dùng để bắt đầu vòng lặp bot.
* **Nút Stop Bot (`$g_hBtnStop`):** Kích thước $75 \times 35 \text{ px}$, màu nền đỏ `0xDB4D4D`. Tự động thay thế vị trí nút Start khi bot đang chạy (Phím tắt: `ESC`).
* **Nút Pause / Resume (`$g_hBtnPause` / `$g_hBtnResume`):** Kích thước $75 \times 35 \text{ px}$, màu nền cam `0xFFA500` (Phím tắt: `Pause/Break`).
* **Nút Search Mode (`$g_hBtnSearchMode`):** Kích thước $75 \times 35 \text{ px}$, màu cam. Chế độ chỉ tìm kiếm base theo điều kiện mà không thực hiện tấn công.
* **Nút Chụp ảnh Làng (`$g_hBtnMakeScreenshot`):** Nhãn `"Photo"`, $35 \times 24 \text{ px}$, nền xanh lá `0x5CAD85`.
* **Nút Ẩn Giả lập (`$g_hBtnHide`):** Nhãn `"Hide"`, $40 \times 24 \text{ px}$, nền xanh dương `0x22C4F5`.
* **Nút Ghép Cửa sổ (`$g_hBtnEmbed`):** Nhãn `"Dock"`, $75 \times 24 \text{ px}$, nền xanh dương `0x22C4F5`. Tác dụng: Nhúng cửa sổ render của giả lập dính liền vào bên cạnh cửa sổ Bot.
* **Checkbox Chạy nền (`$g_hChkBackgroundMode`):** Tọa độ `X=11, Y=82`, nhãn `"Background Mode"`. Bật tắt chế độ chụp ADB/GDI nền.

---

### 2.2. Các Phím Tác Vụ Nhanh (Fast Action Toggles: Dòng `Y = 86`)
Hàng nút bấm kích thước nhỏ phía dưới bên trái:
1. **`Direct` (`$g_hBtnDirectAttack`):** Kích thước $48 \times 24 \text{ px}$. Thực thi ngay chuỗi chuẩn bị và tấn công cho Live Base hiện tại trên màn hình.
2. **`Rush` (`$g_hBtnRushAttack`):** Kích thước $44 \times 24 \text{ px}$. Chế độ tấn công dồn dập liên tục, bỏ qua toàn bộ các bước xin/cho quân hay nâng cấp.
3. **`BBSpam` (`$g_hbtnBBSpam`):** Kích thước $54 \times 24 \text{ px}$. Vòng lặp cày tài nguyên Làng Đêm (Builder Base) liên tục cho đến khi đầy kho.
4. **`CCSpam` (`$g_hbtnCCSpam`):** Kích thước $54 \times 24 \text{ px}$. Vòng lặp tự động tặng quân Clan liên tục mỗi 30 giây.

---

### 2.3. Bảng Giám sát Trạng thái Trực quan (Hero Shields, Laboratory, Pet House)
Nằm ở khu vực giữa đáy (`X = 174, Y = 5` đến `Y = 90`):

#### A. Trạng thái 6 Tướng CoC (Dòng 1: `Y = 5`):
Hiển thị bằng Icon Khiên 16x16 kèm ký tự đại diện:
* **`K`** = Barbarian King (`$g_hlblKing`)
* **`Q`** = Archer Queen (`$g_hlblQueen`)
* **`P`** = Minion Prince / Royal Champion (`$g_hlblPrince`)
* **`W`** = Grand Warden (`$g_hlblWarden`)
* **`C`** = Champion (`$g_hlblChampion`)
* **`D`** = Duke / Hero mới (`$g_hlblDuke`)

**Quy chuẩn 4 màu sắc trạng thái khiên (`$sTxtTip`):**
* **Màu Xám (Gray Shield):** Không sở hữu / Không khả dụng (`$eIcnGrayShield`).
* **Màu Xanh lá (Green Shield):** Đã hồi phục đầy máu, sẵn sàng xuất trận (`$eIcnGreenShield`).
* **Màu Xanh dương (Blue Shield):** Đang trong thời gian hồi máu (Healing timer) (`$eIcnBlueShield`).
* **Màu Đỏ (Red Shield):** Đang trong quá trình nâng cấp cấp độ (`$eIcnRedShield`).

#### B. Trạng thái Nhà Thí nghiệm (Laboratory - Dòng 2: `Y = 43`):
* Icon kính hiển vi 18x18 (`$eIcnLaboratory`).
* Nhãn ký tự `"L"` (`$g_hPicLabMarker`).
* Nhãn đồng hồ đếm ngược thời gian nâng cấp còn lại (`$g_hLbLLabTime`, ví dụ: `"1d 4h"`, `"Finished"`).

#### C. Trạng thái Nhà Thú cưng (Pet House - Dòng 3: `Y = 76`):
* Icon nhà Pet 18x18 (`$eIcnPetHouse`).
* Nhãn đồng hồ đếm ngược thời gian nâng cấp Pet (`$g_hLbLPetTime`).

---

### 2.4. Đồng hồ Đo Tài nguyên Tức thời & Lũy kế (Live Resource Ticker)
Nằm ở khu vực bên phải đáy (`X = 270` đến `X = 470`), có 2 nút mũi tên trái/phải (`$g_hPicArrowLeft`, `$g_hPicArrowRight`) để chuyển đổi trang hiển thị:
* **Trang 1: Làng Chính (Main Village Stats):**
  * Icon Vàng + Số vàng hiện có (`$g_hLblResultGoldNow`) + Tốc độ cướp vàng/giờ (`$g_hLblResultGoldHourNow`).
  * Icon Dầu + Số dầu hiện có (`$g_hLblResultElixirNow`) + Tốc độ cướp dầu/giờ (`$g_hLblResultElixirHourNow`).
  * Icon Dầu đen + Số DE hiện có (`$g_hLblResultDENow`) + Tốc độ cướp DE/giờ (`$g_hLblResultDEHourNow`).
  * Icon Cúp (`$g_hLblResultTrophyNow`), Số thợ rảnh/tổng thợ (`$g_hLblResultBuilderNow`), Số Gem (`$g_hLblResultGemNow`), Thời gian bot đã chạy (`$g_hLblResultRuntimeNow`).
* **Trang 2: Làng Đêm & Clan Capital (Builder Base & Capital Stats):**
  * Vàng Làng đêm (`$g_hLblResultGoldBBNow`), Dầu Làng đêm (`$g_hLblResultElixirBBNow`), Cúp Làng đêm (`$g_hLblResultTrophyBBNow`), Thợ Làng đêm (`$g_hLblResultBuilderBBNow`).
  * Vàng Thủ phủ Clan (`$g_hLblResultCCGoldNow`), Huy chương Đột kích (`$g_hLblResultCCMedalNow`).

---

### 2.5. Thanh Trạng thái Hệ thống (Status Bar)
Nằm sát đáy cùng (`$g_hStatusBar = _GUICtrlStatusBar_Create`):
* Hiển thị trạng thái máy FSM theo thời gian thực: `"Status : Idle"`, `"Status : Searching (Raid #14)"`, `"Status : Dropping Troops"`, `"Status : Train Army (Wait 3m 20s)"`.

---

## PHẦN 3: CẤU TRÚC CHI TIẾT 6 TAB CHÍNH (MAIN TABS ARCHITECTURE)

*(Trích xuất từ `MBR GUI Design.au3` dòng 355–362)*

```
$g_hTabMain = GUICtrlCreateTab(5, 85 + $_GUI_MAIN_TOP, 466, 425)
├── Tab 1: Log           ($g_hTabLog)
├── Tab 2: Village       ($g_hTabVillage)
├── Tab 3: Attack Plan   ($g_hTabAttack)
├── Tab 4: Bot           ($g_hTabBot)
├── Tab 5: Stats         ($g_hTabStats)
└── Tab 6: About Us      ($g_hTabAbout)
```

---

### 3.1. Tab 1: Nhật Ký Vận Hành (Log Tab)
*(File: `COCBot/GUI/MBR GUI Design Log.au3`)*
* **Thành phần chính:**
  * Khung RichEdit hiển thị text đa màu (`$g_hTxtLog` tạo bởi `_GUICtrlRichEdit_Create`):
    * Màu Xanh lá (`$COLOR_SUCCESS = 0x006600`): Cướp thành công, hoàn thành trận đánh.
    * Màu Đỏ (`$COLOR_ERROR = $COLOR_RED`): Lỗi kết nối, OCR thất bại, không tìm thấy cửa sổ.
    * Màu Xanh dương (`$COLOR_INFO = $COLOR_BLUE`): Trạng thái tìm trận, thông tin làng.
    * Màu Cam (`$COLOR_WARNING = $COLOR_MAROON`): Cảnh báo mây lâu, bình chứa đầy.
    * Màu Tím (`$COLOR_DEBUG = $COLOR_PURPLE`): Log debug tọa độ pixel và template.
* **Các nút chức năng phụ dưới khung log:**
  * Checkbox Tự cuộn trang (Auto Scroll: `$g_hChkAutoScrollLog`).
  * Nút Xóa màn hình Log (Clear Log: `$g_hBtnClearLog`).
  * Nút Copy toàn bộ nội dung Log vào Clipboard (Copy Log: `$g_hBtnCopyLog`).

---

### 3.2. Tab 2: Quản Lý Làng Chính (Village Tab)
*(File: `COCBot/GUI/MBR GUI Design Village.au3`)*  
Bao gồm 4 Subtab con:

```
Village Tab ($g_hGUI_VILLAGE_TAB)
├── Subtab 1: Misc          (Dọn chướng ngại vật, xe loot, nạp lại bẫy)
├── Subtab 2: Clan Castle   (Quyên góp lính, xin lính, chia sẻ replay)
├── Subtab 3: Upgrade       (Nâng cấp công trình & xả tường Greedy)
└── Subtab 4: Notify        (Cấu hình gửi tin nhắn Telegram/Discord)
```

#### A. Subtab `Misc` (`MBR GUI Design Child Village - Misc.au3`):
* Nhóm Dọn rác: Checkbox Tự dọn cây cối/hòm Gem (`$g_hChkClearObstacles`), Số thợ tối thiểu cần giữ lại (`$g_hTxtKeepBuilders`).
* Nhóm Thu hoạch: Tự thu mỏ vàng/dầu (`$g_hChkCollectResources`), Thu hoạch xe Loot Cart (`$g_hChkCollectLootCart`).
* Nhóm Nạp đạn: Tự động nạp nỏ X-Bow, Tháp Inferno, Bom bẫy (`$g_hChkRearmTraps`).
* Nhóm Tăng tốc: Tự dùng Bình Tăng Tốc (Clock Tower Potion, Training Potion).

#### B. Subtab `Clan Castle` (`MBR GUI Design Child Village - Donate.au3`):
* Cấu hình Xin quân: Tự động gửi request xin lính khi hết cooldown (`$g_hChkRequestCC`), Ô nhập text nội dung xin lính (`$g_hTxtRequestMsg`).
* Cấu hình Cho quân (Donation Engine):
  * Danh sách icon các loại lính/phép/máy kèm Checkbox cho phép donate.
  * Danh sách từ khóa bộ lọc: Cho phép (Whitelist), Cấm cho (Blacklist từ khóa như `"no goblin"`).
  * Tự động chia sẻ Replay trận đánh vào Clan chat (`$g_hChkShareReplay`).

#### C. Subtab `Upgrade` (`MBR GUI Design Child Village - Upgrade.au3`):
* **Nâng cấp Tường (Auto Wall Upgrade):**
  * Bật/tắt xả tường (`$g_hChkUpgradeWalls`).
  * Chọn nguồn tài nguyên: Chỉ dùng Vàng, Chỉ dùng Dầu, hoặc Dùng cả hai (`$g_hCmbWallResource`).
  * Ngưỡng dự phòng an toàn (`$g_hTxtMinGoldReserve`, `$g_hTxtMinElixirReserve`): Không bao giờ xả tường nếu tài nguyên thấp hơn mức này.
  * Cấp độ tường giới hạn: Nâng từ tường Level $Min$ đến Level $Max$.
* **Nâng cấp Công trình (Building Upgrade):**
  * Tự động nâng cấp nhà theo danh sách vị trí lưu sẵn hoặc ưu tiên công trình tài nguyên/phòng thủ.

#### D. Subtab `Notify` (`MBR GUI Design Child Village - Notify.au3`):
* Cấu hình thông báo từ xa qua **Telegram Bot** hoặc **Discord Webhook**:
  * Nhập Token Telegram, Chat ID.
  * Tùy chọn sự kiện gửi: Khi bị đối thủ tấn công, Khi bot gặp lỗi ngắt kết nối, Báo cáo kết quả sau mỗi trận cướp (kèm ảnh chụp màn hình loot).

---

### 3.3. Tab 3: Kế Hoạch Tấn Công (Attack Plan Tab)
*(File: `COCBot/GUI/MBR GUI Design Attack.au3`)*  
Đây là khu vực giao diện phức tạp nhất với cấu trúc lồng nhau 3 cấp (Nested Tabs):

```
Attack Plan Tab ($g_hGUI_ATTACK_TAB)
├── Subtab 1: Train Army       (Huấn luyện quân đội, Quick Train)
├── Subtab 2: Search & Attack  (Cấu hình Tìm kiếm & Tấn công)
│     ├── Child 1: Deadbase    (Nhà Hoang: Search, Attack, Collectors)
│     ├── Child 2: Activebase  (Nhà Sống: Search, Attack, Weak Defenses)
│     └── Child 3: Options     (Biên đỏ, Dropline, Early Surrender, Smart Zap)
└── Subtab 3: Strategies       (Kịch bản tấn công & Thứ tự thả quân)
```

#### A. Subtab `Train Army` (`MBR GUI Design Child Attack - Troops.au3`):
* Lựa chọn phương thức luyện quân: Sử dụng Quick Train Preset 1, 2, 3 (`$g_hRadQuickTrain1..3`) hoặc Custom Army.
* Điều kiện sẵn sàng xuất kích:
  * Ngưỡng % quân tối thiểu (Ví dụ: `100%`).
  * Yêu cầu Tướng: Bắt buộc đủ King (`$g_hChkRequireKing`), Queen (`$g_hChkRequireQueen`), Warden (`$g_hChkRequireWarden`).
  * Yêu cầu Máy công thành (Siege Machine) và Lính Clan.

#### B. Subtab `Search & Attack` $\to$ Cụm `Deadbase` (`MBR GUI Design Child Attack - Deadbase.au3`):
* **Tab con `Search`:**
  * Ngưỡng tối thiểu: Min Gold (`$g_hTxtMinGoldDB`), Min Elixir (`$g_hTxtMinElixirDB`), Min Dark Elixir (`$g_hTxtMinDEDB`), Min Trophies.
  * Giới hạn cấp Town Hall đối thủ: Chỉ đánh TH nhỏ hơn hoặc bằng TH người chơi.
  * Giới hạn số lần Next tối đa trước khi dừng (`$g_hTxtMaxSearchDB`).
* **Tab con `Attack`:**
  * Chọn thuật toán: Standard (`algorithm_AllTroops`), Scripted (`AttackFromCSV`), Smart Farm (`SmartFarm`).
* **Tab con `Collectors`:**
  * Số lượng mỏ đầy tối thiểu cần phát hiện (`$g_iCollectorMatchesMin`, thường là 4 đến 6).
  * Checkbox kiểm tra độ đầy của mỏ Vàng, Dầu, Hắc Dầu.

#### C. Subtab `Search & Attack` $\to$ Cụm `Activebase` (`MBR GUI Design Child Attack - Activebase.au3`):
* Bộ lọc Căn cứ Phòng thủ Yếu (Weak Base Defense Filters):
  * Checkbox và Combobox giới hạn cấp độ tối đa của: Đại pháo Eagle, Tháp Inferno, Cung X-Bow, Pháo Scattershot, Trụ Monolith, Pháo không Air Defense.
  * Nếu các công trình phòng thủ vượt quá cấp độ chọn $\implies$ Tự động bấm Next bỏ qua.

#### D. Subtab `Search & Attack` $\to$ Cụm `Options` (`MBR GUI Design Child Attack - Options.au3`):
* **Biên đỏ & Dropline:** Chọn Routine quét viền đỏ (`$g_aiAttackScrRedlineRoutine`: None, Original, ImgLoc Raw, ImgLoc Deployable).
* **Smart Zap:** Bật tính năng đánh sét tự động diệt Air Defense/Inferno trước trận.
* **Tiêu chí Dừng trận sớm (End Battle):**
  * Dừng khi không còn tài nguyên cướp được (`$g_hChkEndNoLoot`).
  * Dừng khi đạt 1 Sao / 2 Sao / 3 Sao hoặc đạt 50% phá hủy.
  * Dừng khi toàn bộ Tướng và Lính đã chết sạch.

---

### 3.4. Tab 4: Cấu Hình Bot (Bot Tab)
*(File: `COCBot/GUI/MBR GUI Design Bot.au3`)*  
Bao gồm 4 Subtab:

#### A. Subtab `Options` (`MBR GUI Design Child Bot - Options.au3`):
* Combobox Ngôn ngữ giao diện (`$g_hCmbGUILanguage`): Nạp tự động từ thư mục `languages/*.ini` (English, Vietnamese, Chinese, Spanish...).
* Tự động xóa file rác: Xóa file Log cũ quá $N$ ngày (`$g_hTxtDeleteLogsDays`), Xóa ảnh Loot cũ (`$g_hTxtDeleteLootsDays`), Xóa ảnh Temp.
* Tự khởi động cùng Windows, Tự căn lề cửa sổ giả lập (`Auto Align`), Tự động ẩn cửa sổ.

#### B. Subtab `Android` (`MBR GUI Design Child Bot - Android.au3`):
* Danh sách lựa chọn Giả lập: BlueStacks 5, MEmu, LDPlayer, Nox.
* Cấu hình đường dẫn ADB (`adb.exe`), IP và Cổng ADB (`127.0.0.1:5555`).
* Chế độ Screen Capture: GDI BitBlt, PrintWindow, hoặc ADB Screencap.

#### C. Subtab `Debug` (`MBR GUI Design Child Bot - Debug.au3`):
* Tùy chọn Bật Log chi tiết (Debug Log level).
* Lưu ảnh chụp Zombie Base (các base bị phân loại nhầm Deadbase/Livebase) để huấn luyện lại bộ mẫu nhận diện.
* Các nút test nhanh: Chụp thử 1 frame, Test OCR đọc số vàng, Test tìm kiếm biên đỏ.

#### D. Subtab `Profiles` (`MBR GUI Design Child Bot - Profiles.au3`):
* Quản lý Hồ sơ: Combobox chọn Profile, các nút bấm icon: Thêm mới, Đổi tên, Xóa, Lưu hồ sơ.
* **Cơ chế Đổi Tài khoản (Switch Accounts):**
  * Checkbox bật tính năng chuyển acc (`$g_hChkSwitchAcc`).
  * Chọn phương thức chuyển: **Supercell ID** (`$g_hRadSwitchSuperCellID`) hoặc **SharedPrefs XML** (`$g_hRadSwitchSharedPrefs`).
  * Bảng danh sách tài khoản: Cột Checkbox kích hoạt, Cột Tên tài khoản, Cột Profile cấu hình tương ứng gán cho tài khoản đó.

---

### 3.5. Tab 5: Thống Kê Chi Tiết (Stats Tab)
*(File: `COCBot/GUI/MBR GUI Design Child Bot - Stats.au3`)*
* Lưới hiển thị chi tiết lịch sử từng trận đánh (ListView Grid):
  * Thời gian tấn công, Loại base (DB/LB), Số Vàng/Dầu/DE cướp được, Số Cúp biến động, Tỷ lệ % phá hủy, Thời gian tìm kiếm.
* Biểu đồ/Thống kê tổng hợp: Tổng số trận đánh, Tỷ lệ thắng/thua, Lượng tài nguyên trung bình mỗi trận, Tổng chi phí vàng đã trả cho nút Next.

---

### 3.6. Tab 6: Thông Tin Dự Án (About Us Tab)
*(File: `COCBot/GUI/MBR GUI Design About.au3`)*
* Hiển thị danh sách tác giả, nhóm phát triển cộng đồng (MyBot Team & PHMOD contributors).
* Phiên bản hiện tại, Giấy phép bản quyền GNU GPL v3.
* Nút bấm mở trang chủ diễn đàn (`https://mybot.run`) và link quyên góp hỗ trợ duy trì dự án.

---

## PHẦN 4: HỆ THỐNG CỬA SỔ PHỤ & HỘP THOẠI MODAL (POPUPS & SUB-WINDOWS)

### 4.1. Màn hình Khởi động Tiến trình (Splash Screen)
*(File: `COCBot/GUI/MBR GUI Design Splash.au3`)*
* Cửa sổ hiển thị không viền khi mở bot (`Width = 350, Height = 120`).
* Hiển thị thanh tiến trình (Progress Bar: `$g_hPrgSplash`) và nhãn trạng thái nạp từng thành phần:
  * *"Loading Main GUI..."* $\to$ *"Loading GUI Bottom..."* $\to$ *"Loading Village tab..."* $\to$ *"Loading Attack tab..."* $\to$ *"Initializing GUI..."*.

---

### 4.2. Cửa sổ Thiết lập Thứ tự Thả quân Tùy biến (Drop Order GUI)
*(File: `COCBot/GUI/MBR GUI Design Attack.au3`, dòng 62–144)*
* Cửa sổ Modal độc lập: `$g_hGUI_DropOrder`, Kích thước $440 \times 375 \text{ px}$.
* Cho phép người dùng kéo thả hoặc chọn Combobox sắp xếp trình tự thả quân theo ý muốn (Ví dụ: 1. Golem $\to$ 2. Phù thủy $\to$ 3. Wall Breaker $\to$ 4. Hero $\to$ 5. Lính dọn dẹp).
* Hỗ trợ danh sách hơn 80 loại lính và lính sự kiện (`LoadTranslatedDropOrderList`).

---

### 4.3. Khay Hệ thống Windows (System Tray Context Menu)
*(File: `COCBot/MBR GUI Design.au3`, dòng 124–159)*
* Khi click chuột phải vào icon Bot ở góc đồng hồ Windows (System Tray):
  * **Show bot:** Hiện lại cửa sổ Bot.
  * **Start bot / Stop bot:** Bật/tắt trạng thái chạy.
  * **Pause bot / Resume bot:** Tạm dừng hoặc tiếp tục.
  * **Hide when minimized:** Tự ẩn vào khay hệ thống khi bấm nút thu nhỏ.
  * **About / Exit:** Mở tab giới thiệu hoặc Thoát hoàn toàn ứng dụng.

---

## PHẦN 5: CƠ CHẾ BẢN ĐỊA HÓA & PHẢN HỒI SỰ KIỆN (I18N & EVENT MODEL)

### 5.1. Hệ thống Đa ngôn ngữ qua File `.ini` (`GetTranslatedFileIni`)
Toàn bộ chuỗi văn bản trên giao diện (Nhãn label, Tên nút bấm, Tooltip gợi ý) tuyệt đối không viết cứng (hardcode) trong code AutoIt mà đều đi qua hàm tra cứu từ điển:

```autoit
; Cú pháp chuẩn trong mã nguồn:
GetTranslatedFileIni($sSection, $sKey, $sDefaultText)
```

* `$sSection`: Tên file giao diện (Ví dụ: `"MBR GUI Design Bottom"`).
* `$sKey`: Mã định danh chuỗi (Ví dụ: `"BtnStart"`, `"BtnStart_Info_01"`).
* `$sDefaultText`: Chuỗi tiếng Anh mặc định dùng khi file ngôn ngữ thiếu key.
* **Cơ chế nạp:** Khi người dùng đổi ngôn ngữ trong Combobox Language, hệ thống đọc file `languages/<SelectedLanguage>.ini` và tự động cập nhật lại toàn bộ text của các Control trên màn hình mà không cần khởi động lại bot.

---

### 5.2. Mô hình Xử lý Sự kiện (AutoIt `GUIOnEventMode`)
Dự án kích hoạt chế độ bắt sự kiện hướng đối tượng của AutoIt:
```autoit
Opt("GUIOnEventMode", 1)
```
* Thay vì dùng vòng lặp `GUIGetMsg()`, mỗi nút bấm hoặc checkbox đều được gắn trực tiếp với tên hàm xử lý thông qua `GUICtrlSetOnEvent`:
  * Nút Start: `GUICtrlSetOnEvent($g_hBtnStart, "btnStart")`
  * Nút Stop: `GUICtrlSetOnEvent($g_hBtnStop, "btnStop")`
  * Checkbox Deadbase: `GUICtrlSetOnEvent($g_hChkDeadbase, "DBcheck")`
  * Đổi Profile: `GUICtrlSetOnEvent($g_hCmbProfile, "cmbProfile")`

---

## PHẦN 6: DANH MỤC TASK NHIỆM VỤ THI CÔNG GIAO DIỆN (UI/UX TASK BREAKDOWN)

Kế hoạch phân rã chi tiết để tái lập trình toàn bộ hệ thống giao diện MyBot PHMOD (chia thành **5 Phase chuyên biệt về UI/UX**):

### PHASE UI-1: BỐ CỤC KHUNG CHÍNH & THANH TIÊU ĐỀ (SHELL & TITLE BAR)
* [ ] **Task UI-1.1:** Dựng cửa sổ chính chuẩn kích thước $475 \times 650 \text{ px}$ (hỗ trợ chuyển đổi chế độ Mini $475 \times 205 \text{ px}$).
* [ ] **Task UI-1.2:** Dựng Custom Title Bar với 5 nút điều khiển: Dock (Thu/Phóng `◄`/`►`), Mini/Normal (`▄`/`█`), Minimize, Close, và nhãn tiêu đề hỗ trợ kéo rê cửa sổ.
* [ ] **Task UI-1.3:** Nhúng Banner hình ảnh Logo 475x67 và Banner liên kết URL 475x13.
* [ ] **Task UI-1.4:** Thiết lập System Tray Icon và Context Menu (Show, Start/Stop, Pause, Hide, Exit).

### PHASE UI-2: BẢNG ĐIỀU KHIỂN ĐÁY DÙNG CHUNG (BOTTOM HUD PANEL)
* [ ] **Task UI-2.1:** Dựng cụm nút vận hành lớn bên trái: Start (Xanh), Stop (Đỏ), Pause/Resume (Cam), Search Mode, Photo, Hide, Dock, Checkbox Background Mode.
* [ ] **Task UI-2.2:** Dựng 4 nút tác vụ nhanh: `Direct`, `Rush`, `BBSpam`, `CCSpam`.
* [ ] **Task UI-2.3:** Dựng bảng hiển thị trạng thái 6 Tướng CoC (K, Q, P, W, C, D) với 4 màu khiên trực quan (Xám, Xanh lá, Xanh dương, Đỏ).
* [ ] **Task UI-2.4:** Dựng widget hiển thị thời gian còn lại của Nhà Thí nghiệm (Lab) và Nhà Thú cưng (Pet House).
* [ ] **Task UI-2.5:** Dựng đồng hồ đo tài nguyên trực tiếp (Live Ticker) 2 trang có nút chuyển mũi tên (Trang 1: Vàng, Dầu, DE, Cúp; Trang 2: Làng Đêm & Clan Capital).
* [ ] **Task UI-2.6:** Tích hợp Status Bar hiển thị trạng thái máy FSM ở chân giao diện.

### PHASE UI-3: DỰNG CÁC TAB CẤU HÌNH CỐT LÕI (MAIN TABS PART 1)
* [ ] **Task UI-3.1: Tab Log:** Dựng khung RichEdit cuộn đa dòng, phân loại 5 màu sắc log (Success, Error, Info, Warning, Debug) kèm nút Clear, Copy, Checkbox AutoScroll.
* [ ] **Task UI-3.2: Tab Village - Subtab Misc:** Checkbox dọn chướng ngại vật, giữ thợ, thu hoạch mỏ, xe loot, nạp lại bẫy.
* [ ] **Task UI-3.3: Tab Village - Subtab Clan Castle:** Giao diện xin quân, danh sách lính/phép/máy cho quân, bộ lọc từ khóa Whitelist/Blacklist.
* [ ] **Task UI-3.4: Tab Village - Subtab Upgrade:** Cấu hình xả tường Greedy (chọn tài nguyên, ngưỡng dự phòng an toàn, cấp tường min-max) và nâng cấp nhà.
* [ ] **Task UI-3.5: Tab Village - Subtab Notify:** Cấu hình Token Telegram Bot và Discord Webhook.

### PHASE UI-4: DỰNG TAB TẤN CÔNG & KỊCH BẢN (MAIN TABS PART 2)
* [ ] **Task UI-4.1: Tab Attack - Subtab Train Army:** Chọn Quick Train Preset 1/2/3, điều kiện đủ lính, checkbox bắt buộc đủ Tướng trước khi đánh.
* [ ] **Task UI-4.2: Tab Attack - Subtab Search & Attack (Deadbase):** Dựng 3 tab con lồng nhau:
  * `Search`: Nhập Min Gold, Elixir, DE, Cup, TH level, Max searches.
  * `Attack`: Chọn thuật toán Standard, CSV Scripted, SmartFarm.
  * `Collectors`: Ngưỡng số lượng mỏ đầy tối thiểu.
* [ ] **Task UI-4.3: Tab Attack - Subtab Search & Attack (Activebase):** Bộ lọc công trình phòng thủ yếu (Mortar, WizTower, AD, XBow, Inferno, Eagle, Scatter, Monolith).
* [ ] **Task UI-4.4: Tab Attack - Subtab Options:** Cấu hình Routine quét viền đỏ, Smart Zap, Tiêu chí dừng trận sớm (Hết loot, đạt sao, hết quân).
* [ ] **Task UI-4.5: Modal Window Drop Order:** Cửa sổ thiết lập thứ tự thả quân tùy biến 80+ loại lính.

### PHASE UI-5: DỰNG TAB HỆ THỐNG, ĐA TÀI KHOẢN & BẢN ĐỊA HÓA (SYSTEM & I18N)
* [ ] **Task UI-5.1: Tab Bot - Options & Android:** Chọn ngôn ngữ, tự xóa file log/loot theo ngày, cấu hình giả lập, cổng ADB, capture backend.
* [ ] **Task UI-5.2: Tab Bot - Profiles & Multi-Account:** Combobox chọn Profile, nút Thêm/Đổi tên/Xóa, Bảng danh sách chuyển đổi tài khoản Supercell ID.
* [ ] **Task UI-5.3: Tab Stats:** Dựng bảng lưới ListView thống kê lịch sử trận đánh và biểu đồ tổng hợp tài nguyên.
* [ ] **Task UI-5.4: Tab About Us:** Giao diện thông tin nhóm phát triển, phiên bản, giấy phép GPL-3.0 và liên kết diễn đàn.
* [ ] **Task UI-5.5: Hệ thống Bản địa hóa (I18N Engine):** Xây dựng module đọc file ngôn ngữ `languages/*.ini` và cập nhật text động cho toàn bộ Control trên giao diện.

---
*Tài liệu được phân tích và trích xuất trực tiếp 100% từ mã nguồn giao diện AutoIt3 của MyBot PHMOD v15.3.*
