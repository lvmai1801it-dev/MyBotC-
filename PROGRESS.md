# 🚀 MYBOT C# WPF - PROJECT PROGRESS & CONTEXT

> **Cập nhật lần cuối:** Hết Tuần 1 (Sprint 1 & 2)
> **Trạng thái:** Đã xong Phase 0, 1, 2, R-Refactor và Phase 7 (UI Cơ bản).
> **Tổng số Tests:** 106/106 PASS (103 Core + 1 CoC + 1 Integration + 1 UI).
> **Commit gần nhất:** `218488e` feat(T7.19-T7.24): complete 6-tab UI with Bot, Stats, About.

## 1. TỔNG QUAN TIẾN ĐỘ
- **Ngôn ngữ:** C# 12 / .NET 8 / WPF / MVVM (CommunityToolkit).
- **Testing:** xUnit + FluentAssertions + Moq.
- **Đã hoàn thành:** ~28 tasks / 148 tasks (~19%).
- **Kiến trúc:** Clean Architecture, SOLID, Test-first.

## 2. CẤU TRÚC THƯ MỤC HIỆN TẠI
- `src/MyBot.Core/` (Interfaces, Logger, TimeProvider, ProcessRunner)
- `src/MyBot.Core.Capture/` (Win32 GDI, ADB Screencap, MockReplay)
- `src/MyBot.Core.Input/` (Win32 PostMessage, ADB Input, MockInput)
- `src/MyBot.Core.FSM/` (BotState, StateMachine, Watchdog)
- `src/MyBot.Core.ActionContract/` (DAV Executor, VisualAssertion)
- `src/MyBot.Core.AntiBan/` (HumanDelay, BezierPath, BoundingBoxClick, CoordinateHistory, MicroBreak, Circadian, BehavioralProfile)
- `src/MyBot.UI/` (MainWindow, 6 Tabs, ViewModels)
- `src/MyBot.App/` (App.xaml, Startup.cs - DI Composition Root)
- `tests/MyBot.Core.Tests/` (103 tests)
- `AGENTS.md` (Luật sắt cho Antigravity Agent)

## 3. CÁC MODULE ĐÃ HOÀN THIỆN (ĐÃ CÓ TEST)
1. **Infrastructure (Phase 0):** DI Container, Config (JSON + Schema), Logging (Serilog).
2. **Drivers (Phase 1):** Chụp màn hình (GDI/ADB), Gửi click (PostMessage/ADB), Mock backends cho test.
3. **Action Contract (Phase 2):** Mẫu DAV (Detect-Act-Verify) chống double-click.
4. **Anti-Ban Core (Phase 2):** Delay Gaussian, Chuột Bezier, Random Bounding Box, Lịch nghỉ (MicroBreak, Circadian), Behavioral Seed (đa tài khoản).
5. **UI/UX (Phase 7):** Khung 475x650, Custom Title Bar, Bottom Panel (Hero shields, Loot ticker), 6 Tab đầy đủ (Log, Village, Attack, Bot, Stats, About) với Mock Data.

## 4. BÀI HỌC KINH NGHIỆM (LESSONS LEARNED)
- **Flaky Tests:** Không dùng `Thread.Sleep`, luôn dùng `ITimeProvider`.
- **GDI Leak:** Luôn bọc P/Invoke trong `try/finally` hoặc `SafeHandle`.
- **Token Optimization:** Luôn dùng `Select-String` filter log. Mỗi task chạy trong 1 New Chat.
- **Lỗi Compile C#:** Tránh dùng anonymous type lẫn lộn với tuple (VD: `(double X, double Y)` vs `(int X, int Y)`). Khai báo tường minh.
- **FluentAssertions:** Dùng `.BeInRange(a,b)` thay vì `.BeBetween(a,b)`.

## 5. NHIỆM VỤ TIẾP THEO (TUẦN 2)
- **Task ngay tiếp theo:** Cài đặt Logic thật vào UI (Kết nối `ConfigManager` vào Tab Bot để Lưu/Đọc file `config.json`).
- **Sau đó:** Quay lại Phase 3 (Computer Vision) - Bắt đầu với `TemplateMatcher` (T3.1) và `StateClassifier` (T7.3).
- **Mục tiêu tuần 2:** Bot có thể chụp ảnh MEmu, nhận diện màn hình Làng (Main Village) và hiển thị trạng thái lên UI.

## 6. QUY TẮC CẬP NHẬT
- Khi bắt đầu Chat mới, Agent PHẢI đọc `AGENTS.md` và file `PROGRESS.md` này.
- Sau mỗi task DONE, Agent phải tự cập nhật lại mục "TỔNG QUAN TIẾN ĐỘ" và "COMMIT GẦN NHẤT" trong file này.
