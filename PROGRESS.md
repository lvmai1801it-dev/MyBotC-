# 🚀 MYBOT C# WPF - PROJECT PROGRESS & CONTEXT

> **Cập nhật lần cuối:** Sau Task FIX-OCR-7 (Cắt số theo lưới monospaced)
> **Trạng thái:** Đã xong Phase 0, 1, 2, R-Refactor, Phase 7 (UI Cơ bản), T3.1, T3.7, T4.1–T4.8, T4.R, T5.1 (tối giản), INT-1, INT-1.1, INT-2.5, FIX-INT2, FIX-OCR-2, FIX-OCR-4, FIX-OCR-6, FIX-OCR-7, T7.25, T7.26, T7.27 và T7.28.
> **Tổng số Tests:** 156/156 PASS (153 Core + 1 CoC + 1 Integration + 1 UI).
> **Commit gần nhất:** xem `git log -1` — `fix(OCR): use fixed-width slicing for monospaced game font`.

## 1. TỔNG QUAN TIẾN ĐỘ
- **Ngôn ngữ:** C# 12 / .NET 8 / WPF / MVVM (CommunityToolkit).
- **Testing:** xUnit + FluentAssertions + Moq.
- **Đã hoàn thành:** ~44 tasks / 148 tasks (~30%).
- **Kiến trúc:** Clean Architecture, SOLID, Test-first.

## 2. CẤU TRÚC THƯ MỤC HIỆN TẠI
- `src/MyBot.Core/` (Interfaces, Logger, TimeProvider, ProcessRunner)
- `src/MyBot.Core.Capture/` (Win32 GDI, ADB Screencap, MockReplay)
- `src/MyBot.Core.Input/` (Win32 PostMessage, ADB Input, MockInput)
- `src/MyBot.Core.FSM/` (BotState, StateMachine, Watchdog)
- `src/MyBot.Core.ActionContract/` (DAV Executor, VisualAssertion)
- `src/MyBot.Core.AntiBan/` (HumanDelay, BezierPath, BoundingBoxClick, CoordinateHistory, MicroBreak, Circadian, BehavioralProfile)
- `src/MyBot.Core.CV/` (TemplateMatcher, DigitOcrEngine - OpenCV matchTemplate)
- `src/MyBot.Game.CoC/` (StateClassifier; `Math/` - geometry, red area, dropline, classifier; `Detection/` - StorageDetector; `Village/` - CollectorHarvester)
- `src/MyBot.UI/` (MainWindow, 6 Tabs, ViewModels)
- `src/MyBot.App/` (App.xaml, Startup.cs - DI Composition Root)
- `tests/MyBot.Core.Tests/` (153 tests)
- `AGENTS.md` (Luật sắt cho Antigravity Agent)

## 3. CÁC MODULE ĐÃ HOÀN THIỆN (ĐÃ CÓ TEST)
1. **Infrastructure (Phase 0):** DI Container, Config (JSON + Schema), Logging (Serilog).
2. **Drivers (Phase 1):** Chụp màn hình (GDI/ADB), Gửi click (PostMessage/ADB), Mock backends cho test.
3. **Action Contract (Phase 2):** Mẫu DAV (Detect-Act-Verify) chống double-click.
4. **Anti-Ban Core (Phase 2):** Delay Gaussian, Chuột Bezier, Random Bounding Box, Lịch nghỉ (MicroBreak, Circadian), Behavioral Seed (đa tài khoản).
5. **UI/UX (Phase 7):** Khung 475x650, Custom Title Bar, Bottom Panel (Hero shields, Loot ticker), 6 Tab đầy đủ (Log, Village, Attack, Bot, Stats, About) với Mock Data.
6. **Computer Vision (Phase 3 - T3.1):** `TemplateMatcher` dùng `OpenCvSharp4` (`CCoeffNormed`), `Find` (khớp tốt nhất) + `FindAll` (nhiều khớp, tự vô hiệu vùng đã tìm).
7. **Config ↔ UI (T7.25):** Tab Bot bind 2 chiều với `IConfigManager`; `AppConfig` có thêm `ActiveLanguage`, `DeleteLogsDays`, `EmulatorType`, `AdbPort`, `EnableDebugLog`; nút **Save Config / Load Config** ghi/đọc `config.json` và ghi nhật ký.
8. **Dark Theme (T7.26):** `Themes/DarkTheme.xaml` gom brush + implicit styles (TextBox, ComboBox có template dropdown tối, CheckBox, TabControl/TabItem có gạch chân Accent, Button). Nút Start/Stop/Pause và màu Gold/Elixir/DE đã dùng brush chung.
9. **ADB Capture ↔ UI (T7.27):** Nút **Test Capture** gọi `AdbScreencapCaptureBackend` qua `IProcessRunner`; serial lấy từ `127.0.0.1:{AdbPort}` trong config; lưu `screenshot.png` cạnh file exe và tự mở bằng shell. `Startup` đã đăng ký `IProcessRunner`.
10. **UI State Matrix (T7.28):** `CurrentState` là `[ObservableProperty]` + `[NotifyPropertyChangedFor]`; Start/Stop/Pause/Resume bật/tắt và đổi chữ theo FSM; chuỗi thật `Starting(2s) → Searching`, `Pausing(1s) → Paused`, `Resuming(1s) → Searching`, `Stopping(1s) → Idle`. Nút disabled mờ 50%.
11. **State Classifier (T3.7):** `StateClassifier` trong `MyBot.Game.CoC` dùng `ITemplateMatcher`, ưu tiên Popup → Clouds → Battle → MainVillage, trả `Unknown` cho frame rỗng/không khớp; `GetLastConfidence()` lưu score lần phân loại gần nhất. Template hiện là Mat dummy 10x10 chờ ảnh thật.
12. **Diamond Area Geometry (T4.1):** `Point2D`, `DiamondArea` (8 điểm) và `DiamondAreaCalculator.Calculate(...)` port từ AutoIt `ConvertInternalExternArea()`; validate `innerLeft < innerRight` và `innerTop < innerBottom`, hỗ trợ offset không đối xứng cho 2 hình thoi Inner/Outer.
13. **Diamond Containment (T4.2):** `IsPointInside(x, y, cx, cy, halfW, halfH)` dùng công thức `|dx|/W + |dy|/H <= 1.0` (biên tính là trong), ném `ArgumentException` nếu bán trục <= 0; `IsPointInsideArea(point, area)` suy ra tâm và bán trục từ 4 đỉnh `DiamondArea`.
14. **Red Area Scanner (T4.3):** `RedAreaScanner.Scan(Mat)` dùng `Cv2.InRange` (BGR: B<60, G<60, R>150) + `Cv2.FindNonZero`, chia pixel đỏ vào 4 góc phần tư theo tâm frame; trả `RedAreaResult` với 4 list `Point2D`.
15. **Red Area Cleaner (T4.4):** `RedAreaCleaner.Clean(points, maxDistance=25)` sắp xếp theo X rồi loại điểm có khoảng cách Euclid tới điểm giữ lại trước đó > ngưỡng; `CleanAll(result)` áp dụng cho cả 4 góc phần tư.
16. **Dropline Interpolator (T4.5):** `DroplineInterpolator.Interpolate(controlPoints, numPoints, useSpline)` dựng dense path Catmull-Rom (20 mẫu/đoạn) hoặc tuyến tính, chia đều theo độ dài cung, ghim 2 đầu; trả list rỗng nếu input null.
17. **Euclidean Clusterer (T4.6):** `EuclideanClusterer.Cluster(points, maxDistance=20)` gom điểm theo greedy — giữ điểm đầu tiên của mỗi cụm, bỏ điểm cách cụm đã giữ <= ngưỡng; dùng cho phát hiện điểm mỏ (deadbase).
18. **Storage Detector (T4.7):** `StorageDetector.HasFullStorage(frame, template)` trả false nếu frame/template null hoặc rỗng, ngược lại gọi `ITemplateMatcher.Find(..., 0.75)` và trả `match != null`.
19. **Base Classifier (T4.8):** `BaseType` enum (Unknown/DeadBase/LiveBase/THOutside/ProgressBase) + `BaseClassifier.Classify(...)` ưu tiên THOutside → DeadBase (≥5 mỏ, kho cạn) → LiveBase (<5 mỏ, kho đầy) → ProgressBase (0 mỏ, kho cạn) → Unknown.
20. **Phase 4 Refactor (T4.R):** `dotnet format` sạch; XML docs đầy đủ cho toàn bộ `MyBot.Game.CoC` (class/method/record params); không còn `Thread.Sleep`/anonymous type; phép chia tâm frame trong `RedAreaScanner` dùng `double`.
21. **Collector Harvester tối giản (T5.1):** `CollectorHarvester.HarvestAllAsync(frame, template)` dùng `ITemplateMatcher.FindAll` (threshold 0.8), tap tâm từng khớp qua `IInputBackend`, delay Gaussian bằng `HumanDelay`; trả số mỏ đã click.
22. **UI Test Harness (INT-1):** Nút **Test Harvest** ở tab Bot → Debug; `TestHarvestAsync` chụp MEmu qua ADB (`127.0.0.1:{AdbPort}`), đọc `gold_mine.png` từ thư mục exe, chạy harvester với ADB input; `Startup` đăng ký `ITemplateMatcher` + `HumanDelay`.
23. **Debug Harvest (INT-1.1):** `TestHarvestAsync` lưu `debug_frame.png` cạnh exe, log kích thước template/`gold_mine.png` load, log số mỏ tìm thấy; hạ threshold harvester từ 0.8 → 0.6 để dễ khớp trong điều kiện ánh sáng khác nhau.
24. **UI Polish + Log Controls (INT-2.5):** Tab Log có nút **Clear Log**/**Copy Log**; Bottom Panel chia 3 cột Auto/*/Auto (nút điều khiển dọc, hero + timer trái, tài nguyên căn phải `TextAlignment="Right"`).
25. **OCR Gold (INT-2.5):** `TesseractOcrEngine` (`IOcrEngine`) tiền xử lý Gray + Threshold 150, `PageSegMode.SingleLine`, lọc `[^\d]`; nút **Read Gold** cắt vùng `(580,20,180,32)`, lưu `gold_crop.png`, cập nhật `Gold`. Cần `tessdata/eng.traineddata` cạnh exe.
26. **Fix Tesseract + Debug mỏ vàng (FIX-INT2):** `MyBot.App.csproj` copy `tessdata\**\*.*` ra output cho cả Debug/Release; `TesseractOcrEngine` chuẩn hoá đường dẫn tuyệt đối, tự dò ngược lên thư mục project khi output chưa có `tessdata`, và ném `FileNotFoundException` rõ ràng nếu thiếu `eng.traineddata`; `CollectorHarvester` hạ threshold 0.5, giữ `LastMatches`, UI log điểm khớp cao nhất và lưu `debug_frame_with_box.png` (khoanh đỏ + score).
27. **Toạ độ Vàng + ảnh debug (FIX-OCR-2):** `ReadGoldAsync` cắt vùng `Rect(643, 11, 208, 35)`, vẽ ô xanh lá và lưu `debug_frame_with_gold_box.png` + `gold_crop.png`; `TesseractOcrEngine.PreprocessImage` (Gray → Invert → Otsu) dùng chung cho `Recognize`, lưu `gold_crop_binary.png` khi OCR trả chuỗi rỗng.
28. **Tiền xử lý font Supercell (FIX-OCR-4):** thu hẹp vùng Vàng còn `Rect(643, 11, 150, 35)` để bỏ đồng xu; `PreprocessImage` chuyển Gray → Resize 4x Cubic → GaussianBlur 3x3 → ngưỡng cố định 200 (chỉ giữ lõi trắng) → `BitwiseNot`; `Recognize` lọc `[^\d]` và lưu `gold_crop_binary.png` khi rỗng.
29. **DigitOcrEngine (FIX-OCR-6):** thay hoàn toàn Tesseract bằng OCR template số: Gray → `BinaryInv` ngưỡng 150 → `FindContours` → lọc contour cao 15–35 px → sắp xếp theo X → resize 24x32 → `MatchTemplate` với 10 mẫu `templates/digits/0.png`..`9.png`; lưu `gold_crop_binary.png` để debug. `Startup` khởi tạo engine với `BaseDirectory/templates/digits`; đã gỡ package Tesseract, `OcrEngine.cs` và bước copy `tessdata`.
30. **Cắt số theo lưới (FIX-OCR-7):** bỏ `FindContours` (glow làm dính chữ); dùng Gray → ngưỡng lõi trắng 220 (`Binary`) → `Dilate` 2x2 → chia vùng đọc thành 9 ô đều (`charWidth = Width / 9`) → resize template về kích thước ô → `MatchTemplate`; chỉ nối số khi score > 0.5, ô thấp hơn coi là dấu cách.

## 4. BÀI HỌC KINH NGHIỆM (LESSONS LEARNED)
- **Flaky Tests:** Không dùng `Thread.Sleep`, luôn dùng `ITimeProvider`.
- **GDI Leak:** Luôn bọc P/Invoke trong `try/finally` hoặc `SafeHandle`.
- **Token Optimization:** Luôn dùng `Select-String` filter log. Mỗi task chạy trong 1 New Chat.
- **Lỗi Compile C#:** Tránh dùng anonymous type lẫn lộn với tuple (VD: `(double X, double Y)` vs `(int X, int Y)`). Khai báo tường minh.
- **FluentAssertions:** Dùng `.BeInRange(a,b)` thay vì `.BeBetween(a,b)`.

## 5. NHIỆM VỤ TIẾP THEO (TUẦN 2)
- **Task ngay tiếp theo:** Chạy thử OCR số với MEmu thật (cần `templates/digits/0.png`..`9.png` cạnh exe), sau đó bổ sung Unit Test cho T5.1 và ghép T4.1–T4.8 thành module chọn điểm thả quân an toàn.
- **Đã xong (T7.25):** Nối `ConfigManager` vào Tab Bot để Lưu/Đọc `config.json`.
- **Mục tiêu tuần 2:** Bot có thể chụp ảnh MEmu, nhận diện màn hình Làng (Main Village) và hiển thị trạng thái lên UI.

## 5.1. GHI CHÚ KỸ THUẬT T3.1 (QUAN TRỌNG)
- `CCoeffNormed` là **hệ số tương quan**, không phải so khớp điểm ảnh. Ảnh phẳng (phương sai = 0)
  là trường hợp suy biến: OpenCV trả `score = 1.0` ở **mọi** vị trí.
- Vì vậy fixture test phải có texture (nhiễu), không dùng nền đen + khối trắng đặc.
- Trong test, tránh `Mat.Set` từng pixel (làm crash native test host); dựng `Mat` từ `byte[]`
  và ghi vùng bằng `ROI.CopyTo`.

## 6. QUY TẮC CẬP NHẬT
- Khi bắt đầu Chat mới, Agent PHẢI đọc `AGENTS.md` và file `PROGRESS.md` này.
- Sau mỗi task DONE, Agent phải tự cập nhật lại mục "TỔNG QUAN TIẾN ĐỘ" và "COMMIT GẦN NHẤT" trong file này.
