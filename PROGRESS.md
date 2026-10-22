# 🚀 MYBOT C# WPF - PROJECT PROGRESS & CONTEXT

> **Cập nhật lần cuối:** Sau Task T7.27 (Nối ADB Capture vào nút Test Capture)
> **Trạng thái:** Đã xong Phase 0, 1, 2, R-Refactor, Phase 7 (UI Cơ bản), T3.1, T7.25, T7.26 và T7.27.
> **Tổng số Tests:** 111/111 PASS (108 Core + 1 CoC + 1 Integration + 1 UI).
> **Commit gần nhất:** xem `git log -1` — `feat(T7.27): wire ADB capture backend to UI Test Capture button`.

## 1. TỔNG QUAN TIẾN ĐỘ
- **Ngôn ngữ:** C# 12 / .NET 8 / WPF / MVVM (CommunityToolkit).
- **Testing:** xUnit + FluentAssertions + Moq.
- **Đã hoàn thành:** ~32 tasks / 148 tasks (~22%).
- **Kiến trúc:** Clean Architecture, SOLID, Test-first.

## 2. CẤU TRÚC THƯ MỤC HIỆN TẠI
- `src/MyBot.Core/` (Interfaces, Logger, TimeProvider, ProcessRunner)
- `src/MyBot.Core.Capture/` (Win32 GDI, ADB Screencap, MockReplay)
- `src/MyBot.Core.Input/` (Win32 PostMessage, ADB Input, MockInput)
- `src/MyBot.Core.FSM/` (BotState, StateMachine, Watchdog)
- `src/MyBot.Core.ActionContract/` (DAV Executor, VisualAssertion)
- `src/MyBot.Core.AntiBan/` (HumanDelay, BezierPath, BoundingBoxClick, CoordinateHistory, MicroBreak, Circadian, BehavioralProfile)
- `src/MyBot.Core.CV/` (TemplateMatcher - OpenCV matchTemplate)
- `src/MyBot.UI/` (MainWindow, 6 Tabs, ViewModels)
- `src/MyBot.App/` (App.xaml, Startup.cs - DI Composition Root)
- `tests/MyBot.Core.Tests/` (108 tests)
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

## 4. BÀI HỌC KINH NGHIỆM (LESSONS LEARNED)
- **Flaky Tests:** Không dùng `Thread.Sleep`, luôn dùng `ITimeProvider`.
- **GDI Leak:** Luôn bọc P/Invoke trong `try/finally` hoặc `SafeHandle`.
- **Token Optimization:** Luôn dùng `Select-String` filter log. Mỗi task chạy trong 1 New Chat.
- **Lỗi Compile C#:** Tránh dùng anonymous type lẫn lộn với tuple (VD: `(double X, double Y)` vs `(int X, int Y)`). Khai báo tường minh.
- **FluentAssertions:** Dùng `.BeInRange(a,b)` thay vì `.BeBetween(a,b)`.

## 5. NHIỆM VỤ TIẾP THEO (TUẦN 2)
- **Task ngay tiếp theo:** Phase 3 — T3.2/T3.3 (tiền xử lý ảnh: grayscale, threshold, resize) hoặc `StateClassifier` (T3.7) dựa trên `TemplateMatcher` vừa xong.
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
