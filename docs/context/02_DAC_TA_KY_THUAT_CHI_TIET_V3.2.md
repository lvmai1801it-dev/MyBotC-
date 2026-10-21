# ĐẶC TẢ KỸ THUẬT & PHÂN TÍCH TOÀN DIỆN MÃ NGUỒN MYBOT PHMOD (BẢN CHUẨN HOÁ PUBLIC OSS V3.2)

> **Mục tiêu tài liệu:** Phân tích trực tiếp từ mã nguồn thực tế của dự án **MyBot PHMOD v15.3 (AutoIt3 & MyBot.dll)**. Trích xuất chính xác cấu trúc logic, bảng tham số CSV, hệ toạ độ hình thoi Isometric, thuật toán nhận diện biên đỏ thả quân (Red Area), cơ chế lọc nhà hoang (Dead Base), hệ thống giám sát Watchdog, Mock Input Driver, quy chuẩn Golden Test cho CI/CD và quy trình Graceful Shutdown để cộng đồng mã nguồn mở có thể dễ dàng hiểu, sửa lỗi và kế thừa mà không bị sai lệch cấu trúc dữ liệu.

---

## MỤC LỤC TỔNG QUAN

1. [PHẦN 1: TỔNG QUAN HỆ THỐNG & ĐẶC TẢ DRIVER GIẢ LẬP](#phần-1-tổng-quan-hệ-thống--đặc-tả-driver-giả-lập)
   - 1.1. Bản chất Dự án & Kích thước Màn hình Chuẩn
   - 1.2. Phân tích Các Phương thức Điều khiển & Bảng So sánh Input/Capture
   - 1.3. Bảng Tham chiếu & Quản trị Lỗi Đặc thù Từng Giả lập (Emulator Quirks)
2. [PHẦN 2: BẢNG ĐẶC TẢ TOÀN DIỆN THAM SỐ KỊCH BẢN TẤN CÔNG (MYBOT CSV SPECIFICATION)](#phần-2-bảng-đặc-tả-toàn-diện-tham-số-kịch-bản-tấn-công-mybot-csv-specification)
   - 2.1. Cú pháp Dòng lệnh & Ký tự Phân cách Pipe (`|`)
   - 2.2. Chi tiết Lệnh `DROP` & Giải mã Tham số Cột 1 đến Cột 9
   - 2.3. Chi tiết Lệnh `MAKE` (Tạo Vector Thả quân)
   - 2.4. Chi tiết Lệnh `SIDEB` (Tính điểm Hướng Đánh theo Công trình Phòng thủ)
   - 2.5. Chi tiết Các Lệnh: `WAIT`, `HERO`, `SPELL`, `RETRY`, `SIDE`
3. [PHẦN 3: GIẢI MÃ THUẬT TOÁN HÌNH HỌC & COMPUTER VISION TRONG CODEBASE AUTOIT](#phần-3-giải-mã-thuật-toán-hình-học--computer-vision-trong-codebase-autoit)
   - 3.1. Thuật toán Biến đổi Toạ độ Hình thoi Isometric Diamond (`ConvertInternalExternArea`)
   - 3.2. Thuật toán Quét Viền Đỏ Thả quân (`_GetRedArea.au3` & C++ DLL Call)
   - 3.3. Thuật toán Phân cụm Mỏ & Nhận diện Nhà Hoang (`checkDeadBaseFast` & Giải nghĩa `hasElixirStorage`)
4. [PHẦN 4: HỆ THỐNG STATE MACHINE, WATCHDOG TIMERS & TỰ PHỤC HỒI](#phần-4-hệ-thống-state-machine-watchdog-timers--tự-phục-hồi)
   - 4.1. Bảng Thời Gian Giới Hạn Từng Trạng Thái (Per-State Watchdog Table)
   - 4.2. Cơ chế Giám sát Độc lập (`MyBot.run.Watchdog.au3`)
   - 4.3. Thuật toán Nhận diện Mây & Giới hạn Tìm kiếm (`WaitForClouds.au3` & `$maxSearchCount`)
5. [PHẦN 5: DANH MỤC XỬ LÝ POPUP, PHÁT HIỆN GAME TREO & GRACEFUL SHUTDOWN](#phần-5-danh-mục-xử-lý-popup-phát-hiện-game-treo--graceful-shutdown)
   - 5.1. Bảng Ma trận Nhận diện & Toạ độ Xử lý Popup
   - 5.2. Nhận diện Đơ Hình Game (Freeze Detection) & Quy trình Khởi động lại
   - 5.3. Giao thức Tắt Máy An Toàn (Graceful Shutdown Protocol)
6. [PHẦN 6: NGUYÊN TẮC HÀNH VI CON NGƯỜI & BẢO VỆ TÀI KHOẢN (ANTI-BAN)](#phần-6-nguyên-tắc-hành-vi-con-người--bảo-vệ-tài-khoản-anti-ban)
   - 6.1. Phân phối Thời gian Trễ (Gaussian / Human Jitter)
   - 6.2. Quỹ đạo Di chuột Cong & Bounding Box Offset
   - 6.3. Giới hạn Phiên Chơi & Cảnh báo Phát hiện Phía Máy chủ (Server-Side Awareness)
7. [PHẦN 7: KIẾN TRÚC MỞ RỘNG (PLUGINS) & KIỂM THỬ RECORD/REPLAY TOÀN DIỆN](#phần-7-kiến-trúc-mở-rộng-plugins--kiểm-thử-recordreplay-toàn-diện)
   - 7.1. Giao diện Plugin Driver (ICaptureBackend, IInputBackend, IEmulatorDriver)
   - 7.2. Đặc tả Triển khai `MockInputBackend` cho Replay Testing
   - 7.3. Định dạng Dữ liệu Golden Test (`telemetry.jsonl`) & Tiêu chí Assert CI
   - 7.4. Chiến lược Nén & Lưu trữ Dữ liệu Video Recording (Git LFS / H.265)
   - 7.5. Chiến lược Chống Mục nát Mẫu Nhận diện (Template Rot Defense)
8. [PHẦN 8: LƯỢC ĐỒ CƠ SỞ DỮ LIỆU, CẤU TRÚC THƯ MỤC & LỘ TRÌNH 48+ TASKS](#phần-8-lược-đồ-cơ-sở-dữ-liệu-cấu-trúc-thư-mục--lộ-trình-48-tasks)

---

## PHẦN 1: TỔNG QUAN HỆ THỐNG & ĐẶC TẢ DRIVER GIẢ LẬP

### 1.1. Bản chất Dự án & Kích thước Màn hình Chuẩn
* MyBot PHMOD v15.3 là phần mềm tự động hóa trò chơi Clash of Clans, được phát triển trên nền tảng **AutoIt3** kết hợp thư viện C++ biên dịch sẵn **`MyBot.dll`** (nằm trong thư mục `lib/` hoặc được nạp động qua Win32 API `DllCall`).
* **Chuẩn độ phân giải bắt buộc:** Toàn bộ toạ độ pixel, thuật toán quét hình thoi và thư viện ảnh mẫu (`.png`, Haar cascade `.xml`) đều được xây dựng cố định trên kích thước cửa sổ nội dung:
  $$\text{Window Width} = 860 \text{ px}, \quad \text{Window Height} = 732 \text{ px}$$
* Khi khởi động, hàm `_SetWindowPos` trong AutoIt luôn cưỡng chế cửa sổ giả lập về đúng $860 \times 732$ để tránh làm sai lệch toạ độ.

### 1.2. Phân tích Các Phương thức Điều khiển & Bảng So sánh Input / Capture

Trong mã nguồn MyBot, hai cơ chế giao tiếp chính được sử dụng song song:

```autoit
; [Trích từ Click.au3 & Android.au3]
; 1. Phương thức Win32 GDI & PostMessage
_WinAPI_PostMessage($g_hAndroidWindow, $WM_LBUTTONDOWN, 0, _WinAPI_MakeLong($x, $y))
_WinAPI_PostMessage($g_hAndroidWindow, $WM_LBUTTONUP, 0, _WinAPI_MakeLong($x, $y))

; 2. Phương thức ADB Shell
AdbShell("input tap " & $x & " " & $y)
AdbShell("input swipe " & $x1 & " " & $y1 & " " & $x2 & " " & $y2 & " " & $duration)
```

#### Bảng so sánh các phương thức thao tác trong thực tế:

| Phương thức Thao tác | Độ trễ (Latency) | Độ tin cậy (Reliability) | Cần Focus Cửa sổ? | Khi nào nên dùng trong dự án? |
| :--- | :--- | :--- | :--- | :--- |
| **Win32 `PostMessage`** | $1 - 5 \text{ ms}$ | **Trung bình** (Nhiều giả lập bỏ qua nếu render DirectX) | Không | Dùng khi cần tốc độ thả quân Barch cực nhanh trên cửa sổ nền. |
| **Win32 `SendInput`** | $5 - 15 \text{ ms}$ | **Rất cao** | **Có** (Chiếm chuột thật của người dùng) | Chỉ dùng khi test đơn lẻ, người dùng không chạm vào máy tính. |
| **ADB `input tap`** | $80 - 180 \text{ ms}$ | **Rất cao** (Android nhận như cảm ứng thật) | Không | **Khuyến nghị mặc định** cho toàn bộ thao tác click menu, tìm trận. |
| **ADB `sendevent`** | $30 - 80 \text{ ms}$ | Rất cao (Gửi trực tiếp Linux Event Node) | Không | Dùng khi cần vuốt màn hình (Swipe/Drag) chính xác cao (yêu cầu root). |
| **Scrcpy Control Socket** | $15 - 30 \text{ ms}$ | Cực cao (Stream socket nhị phân trực tiếp) | Không | Tối ưu nhất cho hệ thống chạy đa tài khoản/đa giả lập song song. |

### 1.3. Bảng Tham chiếu Giả lập (Emulator Quirks)

```autoit
; [Trích từ MBR Global Variables.au3 & DetectAndroid.au3]
Global Const $sBlueStacksClass = "BlueStacksApp"
Global Const $sMEmuClass = "MEmuConsole"
Global Const $sNoxClass = "Nox"
Global Const $sLDPlayerClass = "LDPlayerMainFrame"
```

* **BlueStacks 5:** Cửa sổ chính `BlueStacksApp` chỉ là khung bọc; handle nhận sự kiện render và click thực tế là handle con có class `subWin`. Cổng ADB được gán ngẫu nhiên trong file `bluestacks.conf`.
* **LDPlayer 9:** Tương thích ADB hoàn hảo nhất qua cổng mặc định `5555 + (InstanceId * 2)`. RenderWindow hỗ trợ chụp `BitBlt` ổn định.
* **MEmu Play:** Cổng ADB cố định `21503`. Khi Windows bật High-DPI Scaling (> 100%), Win32 `PostMessage` sẽ bị lệch tọa độ nếu không gọi `SetProcessDpiAwareness`.
* **NoxPlayer:** Sử dụng OpenGL độc quyền khiến hàm Win32 `BitBlt` trả về màn hình đen $\implies$ Bắt buộc dùng `Desktop Duplication API` hoặc `ADB screencap`.

---

## PHẦN 2: BẢNG ĐẶC TẢ TOÀN DIỆN THAM SỐ KỊCH BẢN TẤN CÔNG (MYBOT CSV SPECIFICATION)

Rất nhiều người đọc nhầm lẫn định dạng CSV của MyBot dùng dấu phẩy `,`. **Thực tế trong mã nguồn `ParseAttackCSV.au3`, ký tự phân cách bắt buộc là dấu gạch đứng `|` (Pipe character)**.

```autoit
; [Trích từ ParseAttackCSV.au3, dòng 57-63]
$acommand = StringSplit($line, "|")
If $acommand[0] >= 8 Then
    $command = StringStripWS(StringUpper($acommand[1]), $STR_STRIPTRAILING)
    ; Đọc tối đa 9 giá trị tham số chung cho toàn bộ tập lệnh CSV
    __ParseAttackCSV_ReadValues($acommand, $value1, $value2, $value3, $value4, $value5, $value6, $value7, $value8, $value9)
```

---

### 2.1. Cú pháp Dòng lệnh & Cơ chế Đọc Giá trị Tổng quát
Hàm `__ParseAttackCSV_ReadValues` được thiết kế dùng chung để bóc tách tối đa 9 tham số (`$value1` đến `$value9`, tương ứng từ cột 2 đến cột 10 trong dòng text):

```autoit
; [Trích từ ParseAttackCSV.au3, dòng 700-724]
Local $iLastCol = UBound($acommand) - 1
If $iLastCol > 10 Then $iLastCol = 10
For $i = 2 To $iLastCol
    Local $sValue = StringStripWS(StringUpper($acommand[$i]), $STR_STRIPTRAILING)
    Switch $i
        Case 2:  $value1 = $sValue
        Case 3:  $value2 = $sValue
        Case 4:  $value3 = $sValue
        Case 5:  $value4 = $sValue
        Case 6:  $value5 = $sValue
        Case 7:  $value6 = $sValue
        Case 8:  $value7 = $sValue
        Case 9:  $value8 = $sValue
        Case 10: $value9 = $sValue
    EndSwitch
Next
```

Mỗi lệnh sẽ sử dụng số lượng cột khác nhau tùy theo nghiệp vụ. Lệnh `DROP` chủ yếu khai thác từ Cột 1 đến Cột 8, trong khi các lệnh như `SIDEB` hoặc `MAKE` khai thác trọn vẹn cả Cột 8 và Cột 9.

---

### 2.2. Chi tiết Lệnh `DROP` & Giải mã Tham số Cột 1 đến Cột 9

Cú pháp chuẩn trên một dòng file `.csv`:
```
DROP | <Vector> | <Index> | <Quantity> | <Troop> | <DelayPoints> | <DelayDrop> | <SleepAfter> [| <OptionalComment/Extra>]
```

```autoit
; [Trích từ DropTroopFromINI.au3, dòng 33]
Func DropTroopFromINI($sDropVectors, $iStartIndex, $iEndIndex, $aiIndexArray, $iMinQuantity, $iMaxQuantity, $sTroopName, $delayPointmin, $delayPointmax, $delayDropMin, $delayDropMax, $sleepafterMin, $sleepAfterMax, $bDebug = False)
```

#### Bảng định nghĩa chi tiết từng cột tham số của lệnh `DROP`:

| Vị trí Cột | Tên Biến trong Code | Định dạng Cho phép | Ý nghĩa Nghiệp vụ & Logic Thực thi trong AutoIt |
| :---: | :--- | :--- | :--- |
| **Cột 1** | `$command` | Chuỗi `DROP` | Tên lệnh nhả quân. |
| **Cột 2** | `$value1`<br>(`$sDropVectors`) | Tên Vector đơn (Ví dụ: `A`, `B`) hoặc Vector ghép (Ví dụ: `A-B`, `A-B-C-D`) | Chỉ định các đường vector toạ độ đã được tạo trước đó bằng lệnh `MAKE`. Nếu ghép `A-B`, bot sẽ luân phiên thả giữa hai vector. |
| **Cột 3** | `$value2`<br>(`$Index`) | Khoảng số `1-10`, danh sách `1,3,5` hoặc số đơn `5` | Chỉ số điểm thả trên vector: <br>• Dạng khoảng tăng dần (`1-10`): Thả tuần tự từ điểm 1 đến 10.<br>• Dạng khoảng giảm dần (`10-1`): Đảo ngược chiều thả từ điểm 10 về 1.<br>• Dạng danh sách (`1,3,5`): Chỉ thả tại các điểm chỉ định. |
| **Cột 4** | `$value3`<br>(`$Quantity`) | Số nguyên cố định `10` hoặc khoảng ngẫu nhiên `10-20` | Tổng số lượng quân sẽ thả trong dòng lệnh này. Nếu ghi `10-20`, bot gọi `Random(10, 20, 1)` để xác định số lượng thực tế. |
| **Cột 5** | `$value4`<br>(`$sTroopName`) | Chuỗi tên lính chuẩn CoC | Mã định danh quân lính trong game:<br>• Lính thường: `Barb`, `Arch`, `Giant`, `Gobl`, `Wall`, `Wiza`...<br>• Lính Super: `SBarb`, `SArch`, `SGobl`...<br>• Tướng & Máy: `King`, `Queen`, `Warden`, `Champ`, `AnyS` (Bất kỳ Siege Machine nào có sẵn), `Castle` (Quân Clan). |
| **Cột 6** | `$value5`<br>(`$delayPoints`) | Số đơn `50` hoặc khoảng `50-80` (ms) | Thời gian trễ khi chuyển sang điểm thả kế tiếp trên vector. Giúp rải đều quân theo hàng rào tự nhiên. |
| **Cột 7** | `$value6`<br>(`$delayDrop`) | Số đơn `100` hoặc khoảng `100-150` (ms) | Thời gian trễ giữa 2 lần bấm thả quân tại **cùng một điểm**. Tránh việc nhả 10 lính vào cùng 1 pixel trong cùng 1 mili-giây. |
| **Cột 8** | `$value7`<br>(`$sleepAfter`) | Số đơn `200` hoặc khoảng `200-500` (ms) | Thời gian nghỉ dừng lại sau khi đã thả xong toàn bộ số quân của dòng này trước khi chuyển sang đọc dòng lệnh tiếp theo. |
| **Cột 9 & 10** | `$value8` & `$value9` | Chuỗi tùy chọn | Được hàm `__ParseAttackCSV_ReadValues` đọc vào bộ đệm nhưng **không ảnh hưởng đến logic thả quân của `DROP`** (thường dùng làm chú thích ghi chú kịch bản hoặc mở rộng riêng). |

---

### 2.3. Chi tiết Lệnh `MAKE` (Tạo Vector Toạ độ Thả quân)

Lệnh `MAKE` dùng để sinh ra tập hợp danh sách các toạ độ pixel $(X, Y)$ dọc theo viền đỏ hoặc hướng về công trình mục tiêu:

Cú pháp:
```
MAKE | <VectorID> | <Side> | <DropPointsCount> | <AddTiles> | <IgnoreDirection> | <RandomX> | <RandomY> | <BuildingTarget>
```

```autoit
; [Trích từ ParseAttackCSV.au3, dòng 120-145]
If CheckCsvValues("MAKE", 8, $value8) Then ; Vector hướng vào công trình
    Local $tmpArray = MakeTargetDropPoints(Eval($sidex), $value3, $value4, $value8)
    Assign("ATTACKVECTOR_" & $value1, $tmpArray)
Else ; Vector chạy dọc theo viền đỏ Red Area
    Assign("ATTACKVECTOR_" & $value1, MakeDropPoints(Eval($sidex), $value3, $value4, $value5, $value6, $value7))
EndIf
```

* `$value1` (**VectorID**): Ký tự định danh vector từ `A` đến `Z`.
* `$value2` (**Side**): Cạnh bản đồ được chọn (`TOP-LEFT`, `TOP-RIGHT`, `BOTTOM-LEFT`, `BOTTOM-RIGHT`, `FRONT-LEFT`, `FRONT-RIGHT`, `BACK-LEFT`, `BACK-RIGHT`, `RANDOM`).
* `$value3` (**DropPointsCount**): Số lượng điểm neo tạo ra trên vector (Thường là `10` hoặc `12` điểm).
* `$value4` (**AddTiles**): Khoảng cách đẩy lùi (offset) tính bằng số ô tile từ viền đỏ ra xa mép ngoài bản đồ (Ví dụ: `1` hoặc `2` để tránh bị cối xay giã trúng lúc vừa chạm đất).
* `$value8` (**BuildingTarget**): Tùy chọn mục tiêu định hướng vector: `TH` (Town Hall), `DES` (Kho Dầu Đen), `MINE` (Mỏ vàng/dầu).

---

### 2.4. Chi tiết Lệnh `SIDEB` (Tính điểm Hướng Đánh theo Công trình Phòng thủ)

Lệnh `SIDEB` sử dụng toàn bộ 8 tham số từ `$value1` đến `$value8` để gán trọng số độ nguy hiểm cho từng loại công trình phòng thủ, từ đó tự động tính toán ra cạnh thả quân có lợi nhất:

```autoit
; [Trích từ ParseAttackCSV.au3, dòng 926-945]
Case "SIDEB"
    __AttackCSV_AddSideScore($g_aiCSVEagleArtilleryPos, $value1, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVInfernoPos,      $value2, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVXBowPos,         $value3, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVWizTowerPos,     $value4, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVMortarPos,       $value5, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVAirDefensePos,   $value6, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScores($g_aiCSVScatterPos,      $value7, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    __AttackCSV_AddSideScore($g_aiCSVMonolithPos,      $value8, $heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    Local $sidename = __AttackCSV_BestSide($heightTopLeft, $heightTopRight, $heightBottomLeft, $heightBottomRight)
    $MAINSIDE = $sidename
```

* `$value1`: Trọng số điểm cho Eagle Artillery (Pháo đại bàng).
* `$value2`: Trọng số điểm cho Inferno Tower (Tháp hỏa ngục).
* `$value3`: Trọng số điểm cho X-Bow (Cung nỏ).
* `$value4`: Trọng số điểm cho Wizard Tower (Tháp phù thủy).
* `$value5`: Trọng số điểm cho Mortar (Súng cối).
* `$value6`: Trọng số điểm cho Air Defense (Phòng không).
* `$value7`: Trọng số điểm cho Scattershot (Pháo tán xạ).
* `$value8`: Trọng số điểm cho Monolith (Trụ tảng đá cổ).

---

### 2.5. Chi tiết Các Lệnh Khác Trong CSV Parser

* **Lệnh `WAIT`:** `WAIT | <MinMS> | <MaxMS>` (Ví dụ: `WAIT | 1500 | 2500` $\implies$ Bot tạm dừng từ 1.5 đến 2.5 giây).
* **Lệnh `HERO`:** `HERO | <HeroName> | <Vector> | <Index>` (Thả Tướng và kích hoạt giám sát kỹ năng).
* **Lệnh `SIDE`:** `SIDE | <PrimarySide> | <SecondarySide>` (Xác định hướng đánh ưu tiên dựa vào vị trí Town Hall).

---

## PHẦN 3: GIẢI MÃ THUẬT TOÁN HÌNH HỌC & COMPUTER VISION TRONG CODEBASE AUTOIT

### 3.1. Thuật toán Biến đổi Toạ độ Hình thoi Isometric Diamond

Bản đồ Clash of Clans là ma trận lưới vuông $44 \times 44$ ô được xoay nghiêng $45^\circ$. Trong mã nguồn `AttackFromCSV.au3`, việc thiết lập hình thoi được tính toán dựa trên các điểm gốc cây mẫu `$g_afRefVillage`:

```autoit
; [Trích từ AttackFromCSV.au3, hàm ConvertInternalExternArea()]
Func ConvertInternalExternArea()
    Local $InnerDiamondLeft = $g_afRefVillage[$g_iTree][1]
    Local $InnerDiamondRight = $g_afRefVillage[$g_iTree][2]
    Local $InnerDiamondTop = $g_afRefVillage[$g_iTree][3]
    Local $InnerDiamondBottom = $g_afRefVillage[$g_iTree][4]

    Local $OuterDiamondLeft = $InnerDiamondLeft - $g_afRefVillage[$g_iTree][6]
    Local $OuterDiamondRight = $InnerDiamondRight + $g_afRefVillage[$g_iTree][7]
    Local $OuterDiamondTop = $InnerDiamondTop - $g_afRefVillage[$g_iTree][8]
    Local $OuterDiamondBottom = $InnerDiamondBottom + $g_afRefVillage[$g_iTree][9]

    Local $DiamondMiddleX = ($OuterDiamondLeft + $OuterDiamondRight) / 2
    Local $DiamondMiddleY = ($OuterDiamondTop + $OuterDiamondBottom) / 2
```

#### Công thức Toán học Kiểm tra Điểm $P(x, y)$ Nằm Trong Hình Thoi:
Cho 4 đỉnh hình thoi: $Top(X_m, Y_t)$, $Bottom(X_m, Y_b)$, $Left(X_l, Y_m)$, $Right(X_r, Y_m)$:
* Tâm hình thoi: $X_{center} = \frac{X_l + X_r}{2}, \quad Y_{center} = \frac{Y_t + Y_b}{2}$
* Bán trục ngang: $W_{half} = \frac{X_r - X_l}{2}$
* Bán trục dọc: $H_{half} = \frac{Y_b - Y_t}{2}$

Điều kiện cần và đủ để điểm $P(x, y)$ nằm bên trong vùng hình thoi:
$$\frac{|x - X_{center}|}{W_{half}} + \frac{|y - Y_{center}|}{H_{half}} \le 1.0$$

---

### 3.2. Thuật toán Quét Viền Đỏ Thả Quân (`_GetRedArea.au3` & C++ DLL)

```autoit
; [Trích từ _GetRedArea.au3, dòng 23-69]
Func _GetRedArea($iMode = $REDLINE_IMGLOC, $iMaxAllowedPixelDistance = 25, $fMinSideLengthFactor = 0.65)
    Local $colorVariation = 40
    Local $xSkip = 1
    Local $ySkip = 5

    ; Gọi trực tiếp hàm C++ trong MyBot.dll để quét nhanh bộ nhớ Bitmap
    Local $result = DllCallMyBot("getRedArea", "ptr", $g_hHBitmap2, "int", $xSkip, "int", $ySkip, "int", $colorVariation)

    If IsArray($result) Then
        $listPixelBySide = StringSplit($result[0], "#")
    EndIf
    $g_aiPixelTopLeft = GetPixelSide($listPixelBySide, 1)
    $g_aiPixelBottomLeft = GetPixelSide($listPixelBySide, 2)
    $g_aiPixelBottomRight = GetPixelSide($listPixelBySide, 3)
    $g_aiPixelTopRight = GetPixelSide($listPixelBySide, 4)

    ; Làm sạch các điểm nhiễu (Lọc bỏ các điểm văng quá xa)
    CleanRedArea($g_aiPixelTopLeft)
    CleanRedArea($g_aiPixelTopRight)
    CleanRedArea($g_aiPixelBottomLeft)
    CleanRedArea($g_aiPixelBottomRight)
```

---

### 3.3. Thuật toán Phân cụm Mỏ & Nhận diện Nhà Hoang (`checkDeadBase.au3`)

Một căn cứ chỉ được coi là **Dead Base** khi các mỏ tài nguyên đầy ắp nằm ngoài rìa và bình chứa trung tâm không đầy:

```autoit
; [Trích từ checkDeadBase.au3, hàm hasElixirStorage()]
Func hasElixirStorage($bForceCapture = False)
    Local $has = False
    Local $result = findMultiple($g_sImgElixirStorage, "ECD", $g_sImglocRedline, 0, 1000, 0, "objectname,objectpoints,objectlevel", $bForceCapture)

    If IsArray($result) Then
        For $matchedValues In $result
            Local $aPoints = StringSplit($matchedValues[1], "|", $STR_NOCOUNT)
            If UBound($aPoints) > 0 Then
                $has = True
                ExitLoop
            EndIf
        Next
    EndIf
    Return $has
EndFunc
```

> [!IMPORTANT]
> **Giải nghĩa Ngữ nghĩa Template `$g_sImgElixirStorage` (Tránh hiểu nhầm):**  
> Trong thư mục cấu hình `ImageDirectories.au3`:
> `$g_sImgElixirStorage = @ScriptDir & "\imgxml\deadbase\elix\storage\"` (chứa file descriptor `storage_0_90.xml`).
> Đây là **mẫu nhận diện kho Elixir ĐẦY** (chứa mức nước màu tím đầy ắp trong bình thủy tinh), KHÔNG PHẢI là nhận diện sự tồn tại của công trình kho.
> * Nếu `hasElixirStorage() == True`: Có ít nhất 1 bình chứa Elixir ĐẦY $\implies$ Tài nguyên nằm ở trung tâm (Live Base / Nhà sống).
> * Nếu `hasElixirStorage() == False`: Toàn bộ các bình chứa Elixir đều RỖNG $\implies$ Tài nguyên nằm trọn vẹn ở các mỏ ngoài rìa (Dead Base / Nhà hoang).
> * Khi porting sang ngôn ngữ mới, hàm này nên được đổi tên thành `hasFullElixirStorage()` để phản ánh chính xác ngữ nghĩa.

#### Thuật toán Gom Cụm Điểm Mỏ (Euclidean Distance Clustering):
Khi quét template matching mỏ Elixir đầy, 1 mỏ có thể bị khớp nhiều lần ở các pixel liền kề. Hàm `_AddCollectorIfUnique` giải quyết việc này:

```autoit
; [Trích từ checkDeadBase.au3, hàm _AddCollectorIfUnique()]
Func _AddCollectorIfUnique(ByRef $aCollectors, $x, $y, $iDistance)
    For $i = 0 To UBound($aCollectors, 1) - 1
        Local $a = $aCollectors[$i][0] - $x
        Local $b = $aCollectors[$i][1] - $y
        ; Nếu khoảng cách Euclid < 20 px -> Trùng 1 mỏ, bỏ qua
        If Sqrt($a * $a + $b * $b) < $iDistance Then Return False
    Next

    _ArrayAdd($aCollectors, $x & "|" & $y)
    Return True ; Phát hiện thêm 1 mỏ độc lập mới!
EndFunc
```

* **Quy tắc quyết định:** Base được xác nhận là Deadbase khi:
  $$\text{TotalUniqueCollectorsMatched} \ge \$g\_iCollectorMatchesMin \quad (\text{Thường từ } 4 \text{ đến } 6 \text{ mỏ})$$
  kết hợp điều kiện bình chứa rỗng (`hasElixirStorage() == False`).

---

## PHẦN 4: HỆ THỐNG STATE MACHINE, WATCHDOG TIMERS & TỰ PHỤC HỒI

### 4.1. Bảng Thời Gian Giới Hạn Từng Trạng Thái (Per-State Watchdog Table)

Để bot chạy 24/7 không bao giờ bị kẹt vĩnh viễn ở một màn hình trung gian, hệ thống quy định bộ đếm Watchdog nghiêm ngặt cho từng trạng thái:

| Trạng thái Máy | Thời gian Chờ Tối đa | Phương pháp Giám sát | Hành vi Cưỡng chế khi Quá Hạn (Timeout Action) |
| :--- | :---: | :--- | :--- |
| **`ATTACHING`** | $60 \text{ giây}$ | Timer đếm từ lúc gọi Attach | Kill tiến trình giả lập, mở lại từ đầu. |
| **`VILLAGE_LOOP`** | $180 \text{ giây}$ | Đếm thời gian ở màn hình chính | Nhấn phím `ESC` 3 lần, click vào toạ độ an toàn `(5, 5)` để đóng popup kẹt. |
| **`MATCHMAKING`** | $300 \text{ giây}$ | Đếm tổng thời gian tìm trận | Click nút "End Battle" / "Return Home", reset lượt tìm kiếm. |
| **`CLOUDS_WAIT`** | $90 - 180 \text{ giây}$ | Theo dõi mây trong `WaitForClouds.au3` | Click nút "Cancel" tìm trận ở toạ độ `(430, 600)`. |
| **`BATTLE`** | $210 \text{ giây}$ (3.5 phút) | Timer đếm từ khi thả lính đầu tiên | Cưỡng chế click nút "Surrender" `(60, 530)` và xác nhận "Okay". |
| **`RETURN_HOME`** | $45 \text{ giây}$ | Đợi hiệu ứng loading về làng | Khởi động lại ứng dụng CoC qua lệnh ADB Intent. |

---

### 4.2. Cơ chế Giám sát Độc lập (`MyBot.run.Watchdog.au3`)

MyBot không dựa vào chính tiến trình bot để tự phục hồi (vì nếu bot bị Crash hay Treo RAM thì không thể tự chạy code phục hồi được). Dự án sử dụng một tiến trình độc lập hoàn toàn:

```autoit
; [Trích từ MyBot.run.Watchdog.au3, dòng 57-60]
Global $iTimeoutBroadcast = 15000   ; 15 giây gửi tin nhắn ping kiểm tra bot
Global $iTimeoutCheckBot = 5000     ; 5 giây kiểm tra danh sách tiến trình bot
Global $iTimeoutRestartBot = 180000 ; Nếu bot không phản hồi sau 180s (3 phút) -> Tự động khởi động lại bot!
```

---

### 4.3. Thuật toán Nhận diện Mây & Giới hạn Tìm kiếm (`WaitForClouds.au3`)

```autoit
; [Trích từ WaitForClouds.au3, dòng 26-47]
Local $maxSearchCount = 360 ; 360 lần kiểm tra * 250ms = 90 giây chờ mây ở cúp thấp
If $League >= $eLeagueTitan Then
    $maxSearchCount = Random(360, 650, 1) ; Ở cúp cao (Titan/Legend): chờ từ 1.5 đến 2.8 phút
EndIf

; Kiểm tra thoát kẹt mây
If $iCount >= $maxSearchCount Then
    SetLog("Mây kẹt quá lâu, kích hoạt huỷ tìm kiếm!", $COLOR_WARNING)
    Click(430, 600) ; Click nút Cancel
EndIf
```

* **Giới hạn số lần Next (`$g_iMaxSearchCount`):** Trong file cấu hình profile, tham số `max_search_count` (Ví dụ: 150 lần). Khi số lần bấm Next vượt quá con số này, bot sẽ tự động dừng tìm kiếm và quay về làng để tránh việc bị cạn kiệt vàng (âm vàng) do trả phí tìm trận.

---

## PHẦN 5: DANH MỤC XỬ LÝ POPUP, PHÁT HIỆN GAME TREO & GRACEFUL SHUTDOWN

### 5.1. Bảng Ma trận Nhận diện & Toạ độ Xử lý Popup

Toàn bộ các bảng thông báo làm gián đoạn bot được chuẩn hóa toạ độ trên khung $860 \times 732$:

| Mã Popup | Dấu hiệu Nhận diện Đặc trưng | Toạ độ Nút Đóng (860x732) | Hành vi / Xử lý Tiếp theo |
| :--- | :--- | :---: | :--- |
| **`POPUP_CONN_LOST`** | Chữ "Connection lost" / "Out of sync" | `(430, 485)` ("Try Again") | Chờ 5 giây rồi click. Thử quá 3 lần $\implies$ Kích hoạt Restart game. |
| **`POPUP_MAINTENANCE`**| Chữ "Maintenance Break" / "Try again in"| `(430, 485)` ("Reload") | Đọc OCR số phút còn lại $\implies$ Tự đặt lịch ngủ sâu đúng số phút đó. |
| **`POPUP_ANOTHER_DEV`** | Chữ "Another device is connecting" | `(430, 485)` ("Okay") | Có người đăng nhập đè $\implies$ Chuyển sang tài khoản tiếp theo ngay lập tức. |
| **`POPUP_SPECIAL_OFFER`**| Icon nút "X" màu đỏ/trắng góc trên | `(740, 120)` (Nút Close X) | Click đóng. Tuyệt đối không click vào giữa tránh bấm mua gói nạp. |
| **`POPUP_SEASON_END`** | Nút "Okay" màu xanh lá lớn | `(430, 560)` | Click xác nhận nhận tài nguyên mùa giải. |
| **`POPUP_PERSONAL_BREAK`**| Chữ "Personal Break" / "Villagers need rest"| `(430, 485)` | Bị hệ thống CoC giới hạn giờ chơi $\implies$ Tắt bot ngủ đúng 15 phút. |

---

### 5.2. Nhận diện Đơ Hình Game (Freeze Detection)

Trong mã nguồn MyBot, nếu một frame không có chuyển động trong thời gian dài:
* Lấy mẫu 16 điểm pixel tại các vùng động (Vùng thác nước, đốm lửa trại lính, cờ hiệu).
* Nếu giá trị màu RGB của toàn bộ 16 điểm này hoàn toàn không thay đổi sau $15 \text{ giây}$ $\implies$ Xác định game đã bị đơ cứng (Frozen).
* **Quy trình xử lý:**
  1. Ghi lại snapshot ảnh màn hình vào `logs/crashes/`.
  2. Gửi lệnh ADB kết liễu tiến trình: `adb shell am force-stop com.supercell.clashofclans`.
  3. Chờ $5 \text{ giây}$ và mở lại game: `adb shell monkey -p com.supercell.clashofclans 1`.

---

### 5.3. Giao thức Tắt Máy An Toàn (Graceful Shutdown Protocol)

Khi người dùng nhấn `Ctrl+C`, tắt cửa sổ bot hoặc nhận tín hiệu hệ thống `SIGTERM` / `SIGINT`:
1. **Dừng vòng lặp Input:** Ngắt ngay luồng gửi click/drag ảo để tránh click vào vị trí không mong muốn trên Desktop.
2. **Ghi Checkpoint:** Lưu trạng thái hiện tại (Account ID, số vàng đã cướp, trạng thái trận đánh) vào SQLite.
3. **Flush Database WAL:** Thực thi lệnh `PRAGMA wal_checkpoint(FULL);` để đồng bộ toàn bộ dữ liệu từ Write-Ahead Log vào file `.db`, ngăn ngừa hỏng cơ sở dữ liệu.
4. **Giải phóng Tài nguyên:**
   * Gọi `DeleteDC` và `DeleteObject` cho toàn bộ GDI device contexts và bitmaps trong RAM.
   * Đóng toàn bộ Persistent TCP Sockets kết nối tới ADB server.
   * Mở khoá và giải phóng Mutex hệ điều hành (`CloseHandle($hMutex)`).
5. **Ghi nhật ký kết thúc:** Xuất log `[SYSTEM] Graceful shutdown completed cleanly.` trước khi tiến trình thoát mã lỗi 0.

---

## PHẦN 6: NGUYÊN TẮC HÀNH VI CON NGƯỜI & BẢO VỆ TÀI KHOẢN (ANTI-BAN)

### 6.1. Phân phối Thời gian Trễ (Gaussian / Human Jitter)

Mã nguồn AutoIt nguyên bản của MyBot sử dụng hàm `Random($min, $max, 1)` (phân phối đều - Uniform Distribution). Để mô phỏng phản xạ tự nhiên của con người, hệ thống chuẩn hoá áp dụng **Phân phối Chuẩn (Gaussian Distribution)**:

$$\Delta t = \text{clamp}\left( \mu + \sigma \cdot Z, \quad t_{min}, \quad t_{max} \right)$$
*Trong đó $Z$ là biến ngẫu nhiên chuẩn hoá $\mathcal{N}(0, 1)$, $\mu$ là độ trễ kỳ vọng, $\sigma$ là độ lệch chuẩn.*

* **Thả lính nhanh:** $\mu = 80\text{ ms}, \sigma = 15\text{ ms}$, chặn trong đoạn $[50, 130]\text{ ms}$.
* **Bấm Next tìm trận:** $\mu = 1800\text{ ms}, \sigma = 350\text{ ms}$, chặn trong đoạn $[1100, 3200]\text{ ms}$.

### 6.2. Quỹ đạo Di chuột Cong & Bounding Box Offset
* Không bao giờ click vào đúng tâm lý thuyết $(X_{center}, Y_{center})$ của button hay công trình.
* Điểm click thực tế được tính phân tán ngẫu nhiên trong vùng 60% trung tâm của Bounding Box công trình.
* Kiểm tra lịch sử toạ độ: Nếu toạ độ mới sinh ra cách cú click liền trước $< 4\text{ px}$ $\implies$ Tự động dịch chuyển sang toạ độ mới để tránh bị hệ thống giám sát phát hiện click máy móc.

### 6.3. Cảnh báo Phát hiện Phía Máy chủ (Server-Side Awareness)
* Dự án công bố rõ ràng trong tài liệu: Hệ thống máy chủ Supercell theo dõi tần suất cướp bóc và thời gian online.
* **Quy tắc an toàn khuyến nghị:**
  * Không cắm bot quá $4 \text{ giờ}$ liên tục mà không có quãng nghỉ (Micro-break $5 - 15 \text{ phút}$).
  * Cấu hình khung giờ ngủ đêm (từ 00:00 đến 06:00).
  * Giới hạn tối đa không quá $100 - 120 \text{ trận đánh / ngày}$.

---

## PHẦN 7: KIẾN TRÚC MỞ RỘNG (PLUGINS) & KIỂM THỬ RECORD/REPLAY TOÀN DIỆN

### 7.1. Giao diện Plugin Driver (SPI Architecture)

Để cộng đồng có thể bổ sung thêm bất kỳ phần mềm giả lập nào mà không cần sửa đổi mã nguồn cốt lõi (Core Engine), hệ thống áp dụng mô hình 3 Interface trừu tượng:

```
[ Core Engine ]
       │
       ├── ICaptureBackend   --> Hàm grab_frame() trả về mảng BGR 860x732
       │
       ├── IInputBackend     --> Hàm tap(x, y), swipe(x1, y1, x2, y2, ms)
       │
       └── IEmulatorDriver   --> Hàm detect_instances(), launch(), attach()
```

---

### 7.2. Đặc tả Triển khai `MockInputBackend` cho Replay Testing

Khi chạy kiểm thử tự động với video recording, bot không được phép gửi lệnh click ra màn hình thật của máy tính. Đồng thời, hệ thống kiểm thử cần ghi nhận lại mọi thao tác click của bot để so sánh với kịch bản chuẩn:

```python
# [Đặc tả Kiến trúc: MockInputBackend]
class MockInputBackend(IInputBackend):
    """Driver giả lập nhận click phục vụ kiểm thử DAV Pattern và Combat Engine trong CI"""
    def __init__(self, expected_actions_list=None):
        self.recorded_actions = []
        self.expected_actions = expected_actions_list or []

    def tap(self, x: int, y: int) -> None:
        # Ghi nhận thao tác click vào mảng nhật ký kiểm thử
        self.recorded_actions.append({
            "type": "tap",
            "x": x,
            "y": y,
            "timestamp": time.monotonic()
        })

    def swipe(self, x1: int, y1: int, x2: int, y2: int, duration_ms: int) -> None:
        self.recorded_actions.append({
            "type": "swipe",
            "from": (x1, y1),
            "to": (x2, y2),
            "duration": duration_ms,
            "timestamp": time.monotonic()
        })

    def assert_actions_match(self, tolerance_px: int = 15) -> bool:
        """Kiểm chứng toạ độ thả lính của bot có khớp với kịch bản chuẩn (Golden Dataset)"""
        if len(self.recorded_actions) != len(self.expected_actions):
            return False
        for actual, expected in zip(self.recorded_actions, self.expected_actions):
            dist = math.hypot(actual["x"] - expected["x"], actual["y"] - expected["y"])
            if dist > tolerance_px:
                return False
        return True
```

---

### 7.3. Định dạng Dữ liệu Golden Test (`telemetry.jsonl`) & Tiêu chí Assert CI

Mỗi bộ kịch bản test trong `tests/recordings/` bao gồm video khung hình kèm theo file dòng thời gian `telemetry.jsonl`:

```json
{"frame_idx": 1, "expected_state": "MATCHMAKING", "confidence_min": 0.90, "expected_action": null}
{"frame_idx": 45, "expected_state": "MATCHMAKING", "confidence_min": 0.90, "expected_action": {"type": "tap", "x": 750, "y": 530, "target": "NextButton"}}
{"frame_idx": 120, "expected_state": "BATTLE", "confidence_min": 0.95, "expected_action": {"type": "tap", "x": 430, "y": 650, "target": "BarbarianSlot"}}
```

#### Tiêu chí Đánh giá Tự động trên GitHub Actions (Pass/Fail Criteria):
1. **Độ chính xác Chuỗi Trạng thái (State Sequence Accuracy):** Tỷ lệ khung hình nhận diện đúng trạng thái phải đạt:
   $$\text{Accuracy} = \frac{\sum \text{Frames Match State}}{\text{Total Frames}} \ge 95.0\%$$
2. **Dung sai Thao tác Thả quân (Action Tolerance):** Toạ độ click thực tế của bot không được lệch quá $15\text{ px}$ so với vị trí Dropline chuẩn trong telemetry.
3. **Báo cáo Lỗi khi Thất bại:** Nếu test không đạt, CI tự động xuất file `state_mismatch.json` ghi lại chi tiết: Frame index bị sai, trạng thái kỳ vọng, trạng thái thực tế và điểm số confidence để lập trình viên sửa code nhanh chóng.

---

### 7.4. Chiến lược Nén & Lưu trữ Dữ liệu Video Recording

Một trận đánh Clash of Clans kéo dài 3 phút ở tốc độ 30 FPS tương đương $5,400$ khung hình $860 \times 732$. Nếu lưu ảnh PNG không nén, 10 trận đánh mẫu sẽ chiếm $> 5\text{ GB}$, gây tắc nghẽn kho Git.

#### Giải pháp Lưu trữ Chuẩn mực:
1. **Codec Nén Video:** Sử dụng video **H.265 (HEVC) với CRF 15** (Near-lossless, chất lượng điểm ảnh bảo toàn nguyên vẹn cho OpenCV template matching) $\implies$ Dung lượng chỉ còn **$\approx 35 - 50 \text{ MB} / \text{trận}$**.
2. **Bộ Khung hình Trọng yếu (Golden Keyframes):** Với các bài unit test nhanh, chỉ trích xuất $5 - 10$ khung hình đại diện cho từng bước chuyển đổi trạng thái thay vì tải toàn bộ video.
3. **Phân phối qua GitHub Releases / Git LFS:** Mã nguồn bot chỉ lưu trữ các file cấu hình và kịch bản test (`telemetry.jsonl`). Video nặng được tải tự động từ CDN lưu trữ khi chạy CI lần đầu tiên.

---

### 7.5. Chiến lược Chống Mục nát Mẫu Nhận diện (Template Rot Defense)

* **Tách biệt kho mã nguồn và kho ảnh mẫu:** Toàn bộ thư mục ảnh mẫu `templates/` được cấu hình dưới dạng **Git Submodule** trỏ về kho cộng đồng `coc-templates-pack`.
* **Metadata Tương thích Phiên bản:** Mỗi nhóm template đi kèm file `metadata.json` khai báo rõ:
  `{"supported_versions": ["15.3", "15.4", "16.0"], "theme": "default_and_snow", "resolution": "860x732"}`.
* **Công cụ `validate_templates`:** Tự động chạy quét toàn bộ kho ảnh mẫu đối chiếu với bộ video test. Nếu mẫu nào có độ khớp trung bình $< 0.70$, công cụ sẽ xuất cảnh báo để cộng đồng cập nhật lại ảnh mẫu.

---

## PHẦN 8: LƯỢC ĐỒ CƠ SỞ DỮ LIỆU, CẤU TRÚC THƯ MỤC & LỘ TRÌNH 48+ TASKS

### 8.1. Lược đồ Cơ sở Dữ liệu SQLite Chuẩn (Có Indexes Tối ưu)

```sql
CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    profile_name TEXT UNIQUE NOT NULL,
    supercell_id_alias TEXT NOT NULL,
    th_level INTEGER DEFAULT 0,
    trophies INTEGER DEFAULT 0,
    status TEXT DEFAULT 'ACTIVE',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS battle_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    gold_stolen INTEGER,
    elixir_stolen INTEGER,
    dark_elixir_stolen INTEGER,
    destruction_percent INTEGER,
    stars INTEGER,
    strategy_used TEXT,
    screenshot_path TEXT,
    FOREIGN KEY(account_id) REFERENCES accounts(id)
);
CREATE INDEX IF NOT EXISTS idx_battle_account_time ON battle_history(account_id, timestamp);

CREATE TABLE IF NOT EXISTS error_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    error_state TEXT,
    error_message TEXT,
    snapshot_path TEXT
);
```

---

### 8.2. Cấu trúc Thư mục Chuẩn Public Open-Source

```
clash-bot/
├── .github/
│   ├── workflows/             # CI/CD: lint, replay-test, build
│   └── PULL_REQUEST_TEMPLATE.md
├── core/                      # Framework lõi độc lập, không dính logic CoC
│   ├── capture/               # ICaptureBackend, Win32, ADB, MockReplay
│   ├── input/                 # IInputBackend, PostMessage, ADBInput, MockInput
│   ├── cv/                    # MultiTemplateMatcher, OCREngine
│   ├── fsm/                   # Generic Finite State Machine, Watchdog
│   ├── anti_ban/              # GaussianDelay, BezierPath, HumanBehavior
│   └── action_contract/       # DAV Pattern Executor, VisualAssertion
├── games/
│   └── clash_of_clans/        # Toàn bộ nghiệp vụ đặc thù của Clash of Clans
│       ├── states/            # Village, Matchmaking, Battle, Recovery
│       ├── combat/            # AttackCSVParser, DroplineGenerator, Spline
│       ├── village/           # CollectorHarvester, WallUpgrader, TrainManager
│       └── templates/         # [GIT SUBMODULE -> coc-templates-pack]
├── plugins/
│   └── emulators/             # Plugin cắm giả lập (BlueStacks, LDPlayer, MEmu, Nox)
├── ui/                        # Dashboard giao diện điều khiển người dùng
├── tools/                     # Bộ công cụ cho cộng đồng
│   ├── calibrate_coords       # Cân chỉnh toạ độ màn hình
│   ├── learn_popup            # Công cụ dạy bot popup mới bằng chuột
│   ├── record_session         # Ghi lại video trận đánh làm testcase
│   └── validate_templates     # Kiểm tra độ mục nát (Rot) của template
├── tests/
│   ├── recordings/            # Dataset video H.265 kèm telemetry.jsonl
│   ├── unit/                  # Unit tests: Math, Isometric, Bezier, CSV
│   └── test_replay_golden     # Chạy test hồi quy trên video mẫu
├── docs/
│   ├── adr/                   # Architecture Decision Records (Giải thích "Tại sao")
│   ├── CONTRIBUTING.md        # Hướng dẫn đóng góp cho cộng đồng
│   └── KNOWN_LIMITATIONS.md   # Công khai các giới hạn thực tế
├── LICENSE                    # Giấy phép bản quyền (GPL-3.0)
├── DISCLAIMER.md              # Tuyên bố từ chối trách nhiệm pháp lý
└── README.md                  # Hướng dẫn cài đặt và khởi động nhanh
```

---

### 8.3. Lộ trình Phân rã 48+ Nhiệm vụ Chi tiết (Từ Phase 0 đến Phase 7)

#### GIAI ĐOẠN 0: NỀN MÓNG CỘNG ĐỒNG & BỘ CÔNG CỤ (SPRINT 1)
* [ ] **Task 0.1:** Khởi tạo cấu trúc repository chuẩn OSS, LICENSE GPL-3.0 và tài liệu hướng dẫn `CONTRIBUTING.md`.
* [ ] **Task 0.2:** Viết 5 bản ghi kiến trúc lớn (`docs/adr/`): Mô hình SPI, DAV Pattern, Replay Test, Submodule Template, Anti-Ban.
* [ ] **Task 0.3:** Xây dựng công cụ ghi hình trận đánh lossless H.265 `tools/record_session`.
* [ ] **Task 0.4:** Thu thập bộ dữ liệu Golden Dataset (10 kịch bản trận đánh tiêu biểu kèm `telemetry.jsonl`).
* [ ] **Task 0.5:** Thiết lập luồng GitHub Actions CI tự động kiểm tra code và chạy Replay Test.

#### GIAI ĐOẠN 1: TẦNG GIAO TIẾP PHẦN CỨNG & HỆ THỐNG PLUGIN (SPRINT 2)
* [ ] **Task 1.1:** Định nghĩa bộ Interface trừu tượng `ICaptureBackend`, `IInputBackend`, `IEmulatorDriver`.
* [ ] **Task 1.2:** Triển khai Plugin hoàn chỉnh cho **LDPlayer 9** (DirectX BitBlt + ADB Input).
* [ ] **Task 1.3:** Triển khai Plugin hoàn chỉnh cho **BlueStacks 5** (Quản lý handle con `subWin`).
* [ ] **Task 1.4:** Triển khai Plugin hoàn chỉnh cho **MEmu Play** và **NoxPlayer**.
* [ ] **Task 1.5:** Lập trình cơ chế Runtime Detection tự động kiểm tra và chọn chuỗi Fallback Capture.
* [ ] **Task 1.6:** Triển khai driver `MockReplayCaptureBackend` và `MockInputBackend` phục vụ chạy CI.

#### GIAI ĐOẠN 2: THỰC THI THAO TÁC CÓ KIỂM CHỨNG & ANTI-BAN (SPRINT 3)
* [ ] **Task 2.1:** Lập trình bộ thực thi hợp đồng hành động theo mẫu Declare-Act-Verify (DAV Pattern).
* [ ] **Task 2.2:** Xây dựng bộ sinh độ trễ ngẫu nhiên theo phân phối chuẩn Gaussian và Log-Normal.
* [ ] **Task 2.3:** Triển khai thuật toán di chuyển chuột đường cong Bezier bậc 3 kèm Jitter và Overshoot.
* [ ] **Task 2.4:** Lập trình bộ tính toán tọa độ click ngẫu nhiên trong Bounding Box tránh lặp lại điểm cũ.
* [ ] **Task 2.5:** Xây dựng bộ lập lịch nghỉ ngơi tự nhiên (Micro-breaks và Circadian sleep).

#### GIAI ĐOẠN 3: THỊ GIÁC MÁY TÍNH & HỆ THỐNG BẢO VỆ MẪU (SPRINT 4)
* [ ] **Task 3.1:** Tách submodule quản lý ảnh mẫu `coc-templates-pack`.
* [ ] **Task 3.2:** Xây dựng bộ so khớp đa mẫu (Multi-Scale Template Matching) song song hóa.
* [ ] **Task 3.3:** Huấn luyện / Tích hợp mô hình OCR chữ số CoC đặc thù (Chính xác 100% Vàng, Dầu, Cúp, Level).
* [ ] **Task 3.4:** Lập trình công cụ kiểm định độ mục nát của mẫu nhận diện `tools/validate_templates`.
* [ ] **Task 3.5:** Triển khai thuật toán kiểm tra đơ hình game qua Perceptual Hash (`FreezeDetector`).
* [ ] **Task 3.6:** Lập trình công cụ dạy bot popup mới bằng giao diện đồ họa `tools/learn_popup`.

#### GIAI ĐOẠN 4: ĐỘNG CƠ HÌNH HỌC & ĐÁNH GIÁ CĂN CỨ (SPRINT 5)
* [ ] **Task 4.1:** Lập trình bộ biến đổi toạ độ hình thoi Isometric Diamond Grid CoC $44 \times 44$ từ `ConvertInternalExternArea`.
* [ ] **Task 4.2:** Triển khai thuật toán quét pixel biên đỏ cấm thả quân từ `_GetRedArea.au3`.
* [ ] **Task 4.3:** Lập trình module lọc nhiễu Outlier và phân loại 4 cạnh Dropline (TL, BL, TR, BR).
* [ ] **Task 4.4:** Xây dựng thuật toán nội suy đường thả quân liên tục (Dropline Spline Interpolation).
* [ ] **Task 4.5:** Triển khai thuật toán phân cụm khoảng cách Euclid để nhận diện chính xác Nhà Hoang (`Deadbase Clustering`).
* [ ] **Task 4.6:** Lập trình bộ phân loại căn cứ (Base Layout Classifier: TH ngoài, Deadbase, Livebase).

#### GIAI ĐOẠN 5: BỘ MÁY QUẢN LÝ LÀNG & HOẠT ĐỘNG PHỤ (SPRINT 6)
* [ ] **Task 5.1:** Lập trình module tự động thu hoạch mỏ tài nguyên và xe chiến lợi phẩm (Loot Cart).
* [ ] **Task 5.2:** Triển khai logic phát hiện và dọn dẹp chướng ngại vật khi có thợ rảnh.
* [ ] **Task 5.3:** Cài đặt thuật toán Greedy xả vàng/dầu thừa nâng cấp tường.
* [ ] **Task 5.4:** Lập trình module tự động luyện quân thông qua giao diện Quick Train.
* [ ] **Task 5.5:** Xây dựng hệ thống quyên góp quân Clan thông minh dựa trên OCR text.
* [ ] **Task 5.6:** Triển khai module Làng Đêm (Builder Base 2.0 2-Phase Attack).
* [ ] **Task 5.7:** Lập trình module tự động nâng cấp Laboratory và Blacksmith.

#### GIAI ĐOẠN 6: ĐỘNG CƠ CHIẾN ĐẤU & THÔNG DỊCH CSV 2.0 (SPRINT 7)
* [ ] **Task 6.1:** Xây dựng vòng lặp tìm trận (Matchmaking Loop) kèm bộ lọc điều kiện tài nguyên động từ `WaitForClouds.au3`.
* [ ] **Task 6.2:** Xây dựng Parser thông dịch đầy đủ ngữ pháp Attack CSV từ `ParseAttackCSV.au3` & `DropTroopFromINI.au3`.
* [ ] **Task 6.3:** Cài đặt logic kích hoạt kỹ năng Hero theo ngưỡng máu và thời gian chiến trường.
* [ ] **Task 6.4:** Lập trình logic điều khiển máy công thành và kích hoạt phá máy sớm (Early Pop).
* [ ] **Task 6.5:** Triển khai thuật toán giám sát trận đấu và dừng trận sớm thông minh (Smart Early Surrender).

#### GIAI ĐOẠN 7: ĐA TÀI KHOẢN, DATABASE, GIAO DIỆN & HOÀN THIỆN (SPRINT 8)
* [ ] **Task 7.1:** Lập trình giao thức tự động chuyển đổi tài khoản Supercell ID (Không lưu mật khẩu plain-text).
* [ ] **Task 7.2:** Thiết lập cơ sở dữ liệu SQLite lưu lịch sử trận đánh, tài nguyên cướp và log lỗi có index.
* [ ] **Task 7.3:** Xây dựng Giao diện Điều khiển Hiện đại (Dashboard GUI, bảng thống kê Loot/giờ, đồ thị tài nguyên).
* [ ] **Task 7.4:** Triển khai cửa sổ Debug Overlay thời gian thực (Hiển thị Dropline, detected objects, bounding boxes).
* [ ] **Task 7.5:** Kiểm thử tích hợp chạy liên tục 72 giờ (với cờ `--dev-mode` tắt bộ đếm session governor).
* [ ] **Task 7.6:** Cài đặt quy trình Graceful Shutdown xử lý bắt tín hiệu SIGINT/SIGTERM đồng bộ SQLite WAL.

---
*Bản quyền tài liệu thuộc về Dự án Tự động hóa Mã nguồn mở MyBot Modern Re-Engineering Spec 3.2.*
