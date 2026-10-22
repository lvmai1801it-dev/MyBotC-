# 📊 TIẾN ĐỘ TRIỂN KHAI MYBOT C# WPF — BẢNG THEO DÕI

> **Cập nhật lần cuối:** 2026-10-21
>
> **Tham chiếu:** [05_KE_HOACH_TRIEN_KHAI_CSHARP_WPF_124_TASKS.md](file:///f:/Mybot_PORT/docs/context/05_KE_HOACH_TRIEN_KHAI_CSHARP_WPF_124_TASKS.md)
>
> **Tổng test hiện tại:** 48 Passed / 0 Failed ✅

---

## 📈 TỔNG QUAN TIẾN ĐỘ

| Phase | Tên | Tổng Tasks | Hoàn thành | Tiến độ |
|:---:|---|:---:|:---:|:---:|
| **0** | Solution Foundation, FSM Primitives & Dataset | 13 | 6 | ██████░░░░░░░ 46% |
| **1** | Driver Layer & Emulator Plugins | 12 | 5 | █████░░░░░░░░ 42% |
| **2** | Action Contract & Anti-Ban | 10 | 4 | █████░░░░░░░░ 40% |
| **3** | Computer Vision & OCR | 10 | 0 | ░░░░░░░░░░░░░ 0% |
| **4–10** | Game Logic, UI, Integration... | ~100 | 0 | ░░░░░░░░░░░░░ 0% |
| **R** | Refactor Tasks (bổ sung) | 3 | 3 | █████████████ 100% |
| | **TỔNG** | **~148** | **18** | **~12%** |

---

## 🏗️ PHASE 0 — SOLUTION FOUNDATION, FSM PRIMITIVES & DATASET

### ✅ T0.1 — Khởi tạo Solution & Project Structure
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Tạo `MyBot.sln` với 20+ project, `Directory.Build.props` (LangVersion=12, Nullable=enable).
- **Files chính:**
  - [`MyBot.sln`](file:///f:/Mybot_PORT/MyBot.sln)
  - [`Directory.Build.props`](file:///f:/Mybot_PORT/Directory.Build.props)
  - 20 project directories trong `src/`, `tests/`, `plugins/`
- **Test kết quả:**
  - `T0.1.1` ✅ `dotnet restore` thành công
  - `T0.1.2` ✅ `dotnet build` ra các `.dll`
  - `T0.1.3` ✅ Reference graph đúng, không circular dependency

---

### ✅ T0.2 — Core Abstractions (Interfaces)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Định nghĩa tất cả interface cốt lõi trong `MyBot.Core/Abstractions/`.
- **Files chính:**
  - [`ICaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/ICaptureBackend.cs) — `Initialize(EmulatorTarget)`, `GrabFrame()`, `IsRunning`
  - [`IInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IInputBackend.cs) — `Initialize(EmulatorTarget)`, `Tap()`, `Swipe()`
  - [`IEmulatorDriver.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IEmulatorDriver.cs)
  - [`INotifier.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/INotifier.cs)
  - [`ITab.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/ITab.cs)
  - [`IStateClassifier.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IStateClassifier.cs)
  - [`ITemplateMatcher.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/ITemplateMatcher.cs)
  - [`IOcrEngine.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IOcrEngine.cs)
  - [`ITimeProvider.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/ITimeProvider.cs) — `UtcNow`, `DelayAsync()`
  - [`IBotLogger.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IBotLogger.cs)
  - [`IProcessRunner.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IProcessRunner.cs) — `ProcessResult` record + interface
  - [`EmulatorTarget.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/EmulatorTarget.cs) — `record EmulatorTarget(IntPtr? WindowHandle, string? AdbSerial, string? MockRecordingPath)`
- **Test kết quả:**
  - `T0.2.1` ✅ — [`AbstractionsTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AbstractionsTests.cs) pass
  - `T0.2.2` ✅ — `IStateClassifier.Classify(Mat) → GameState`

---

### ✅ T0.3 — DI Container + Composition Root
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Setup `Microsoft.Extensions.DependencyInjection`. Composition root trong `Program.cs`.
- **Files chính:**
  - [`Program.cs`](file:///f:/Mybot_PORT/src/MyBot.App/Program.cs)
- **Test kết quả:**
  - `T0.3.1` ✅ — [`DependencyInjectionTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/DependencyInjectionTests.cs): `ITimeProvider` singleton
  - `T0.3.2` ✅ — `ITab` transient

---

### ✅ T0.4 — Logging Infrastructure (Serilog)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Serilog với Console + File sink. `IBotLogger` + `SerilogBotLogger`.
- **Files chính:**
  - [`SerilogBotLogger.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Logging/SerilogBotLogger.cs)
- **Test kết quả:**
  - `T0.4.1` ✅ — [`LoggingTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/LoggingTests.cs): Log to file + name based on TimeProvider
  - `T0.4.3` ✅ — Log level filter hoạt động

---

### ✅ T0.5 — Configuration System (JSON + Schema Validation)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** `ConfigManager` load/save/validate/migrate `config.json`.
- **Files chính:**
  - [`ConfigManager.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Config/ConfigManager.cs)
- **Test kết quả:**
  - `T0.5.1` ✅ — [`ConfigTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/ConfigTests.cs): Load default config
  - `T0.5.2` ✅ — Config thiếu field → throw exception
  - `T0.5.3` ✅ — Auto-migrate V1 → V2

---

### ✅ T0.9 — FSM Core (BotState Enum & StateMachine)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Enum `BotState` (10 trạng thái) + `BotStateMachine` thread-safe + `StateChanged` event.
- **Files chính:**
  - [`BotState.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.FSM/BotState.cs) — `Uninitialized, Idle, Starting, Searching, Pausing, Paused, Resuming, Stopping, Recovering, Error`
  - [`BotStateMachine.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.FSM/BotStateMachine.cs)
  - [`TransitionGuard.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.FSM/TransitionGuard.cs) — Ma trận chuyển đổi hợp lệ
  - [`StateWatchdog.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.FSM/StateWatchdog.cs) — Timeout giám sát mỗi trạng thái
- **Test kết quả:**
  - `T0.9.1` ✅ — [`FsmTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/FsmTests.cs): Valid transitions pass
  - `T0.9.2` ✅ — Invalid transitions throw
  - ✅ Watchdog fire event on timeout

---

### ⬜ T0.6 — GitHub Actions CI/CD Pipeline
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T0.7 — Documentation Starter Pack
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T0.8 — Record Session Tool (CLI)
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T0.10 — Transition Guard Matrix (mở rộng)
- **Trạng thái:** ⚠️ ĐÃ GỘP VÀO T0.9 — `TransitionGuard.cs` đã tạo xong
### ⬜ T0.11 — OCR Dataset Collector Tool
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T0.12 — OCR Engine Model Prep (ONNX)
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T0.13 — License Compliance Audit
- **Trạng thái:** ⬜ CHƯA LÀM

---

## 🎮 PHASE 1 — DRIVER LAYER & EMULATOR PLUGINS

### ✅ T1.1 — ICaptureBackend: Win32 GDI (BitBlt)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** `Win32GdiCaptureBackend` dùng `BitBlt` từ DC cửa sổ emulator, reuse GDI resources qua `IDisposable`.
- **Files chính:**
  - [`Win32GdiCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/Win32GdiCaptureBackend.cs)
- **Test kết quả:**
  - `T1.1.2` ✅ — [`CaptureTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/CaptureTests.cs): Correct size + Dispose cleanly
  - ✅ Invalid handle trả Empty Mat / throw ArgumentException

---

### ✅ T1.2 — ICaptureBackend: ADB Screencap
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** `AdbScreencapCaptureBackend` dùng `adb exec-out screencap -p`, decode PNG bytes thành `Mat` OpenCV.
- **Files chính:**
  - [`AdbScreencapCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/AdbScreencapCaptureBackend.cs)
- **Test kết quả (ADB mock via IProcessRunner):**
  - `T1.2.1` ✅ — [`AdbBackendTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AdbBackendTests.cs): Valid PNG decode thành Mat 100×100
  - ✅ Device online/offline detection
  - ✅ Process fail / empty bytes → Empty Mat
  - ✅ Timeout qua IProcessRunner
- **Test thực tế trên MEmu Headless:**
  - ✅ Chụp thành công 860×732px, benchmark ~1.7 FPS (ADB screencap baseline)

---

### ✅ T1.3 — MockReplayCaptureBackend
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Đọc chuỗi ảnh `.png/.jpg` từ thư mục, hỗ trợ `loop` flag.
- **Files chính:**
  - [`MockReplayCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/MockReplayCaptureBackend.cs)
- **Lưu ý:** Hiện đọc từ thư mục ảnh, chưa hỗ trợ `.mkv` (FFV1). Sẽ mở rộng khi có T0.8 (Record Session Tool).
- **Test kết quả:**
  - `T1.3.1` ✅ — [`MockLayerTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/MockLayerTests.cs): Đọc 3 frames tuần tự
  - `T1.3.2` ✅ — `loop=false` → Mat rỗng sau frame cuối

---

### ✅ T1.4 — IInputBackend: Win32 PostMessage
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Gửi `WM_LBUTTONDOWN`, `WM_MOUSEMOVE`, `WM_LBUTTONUP` tới hwnd emulator. Tap + Swipe qua `ITimeProvider`.
- **Files chính:**
  - [`Win32PostMessageInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/Win32PostMessageInputBackend.cs)
- **Test kết quả:**
  - ✅ Click background window thành công

---

### ✅ T1.5 — IInputBackend: ADB Input
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** `AdbInputBackend` thực thi `adb shell input tap/swipe` qua `IProcessRunner`.
- **Files chính:**
  - [`AdbInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/AdbInputBackend.cs)
- **Test kết quả:**
  - `T1.5.1` ✅ — [`AdbBackendTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AdbBackendTests.cs): Đúng cú pháp `input tap x y`, `input swipe x1 y1 x2 y2 d`
  - ✅ TapBeforeInitialize throws
- **Test thực tế trên MEmu Headless:**
  - ✅ Tap và Swipe đều hoạt động chính xác

---

### ✅ T1.6 — MockInputBackend
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Ghi lại tất cả hành động tap/swipe vào `RecordedActions`, hỗ trợ `Clear()`.
- **Files chính:**
  - [`MockInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/MockInputBackend.cs)
- **Test kết quả:**
  - `T1.6.1` ✅ — [`MockLayerTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/MockLayerTests.cs): Record Tap/Swipe chính xác
  - `T1.6.2` ✅ — `Clear()` giải phóng sạch

---

### ⬜ T1.0 — Window Normalizer & Resize Utility
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T1.7 — IEmulatorDriver Interface + Plugin Registry
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T1.8 — LDPlayer 9 Driver Plugin
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T1.9 — BlueStacks 5 Driver Plugin
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T1.10 — MEmu + Nox Plugins
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T1.11 — Runtime Detection & Fallback Chain
- **Trạng thái:** ⬜ CHƯA LÀM

---

## 🛡️ PHASE 2 — ACTION CONTRACT & ANTI-BAN

### ✅ T2.1 — VisualAssertion Record Class
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** `record VisualAssertion(Rect Region, Func<Mat,Mat,bool> VerifyFn, int TimeoutMs, string Description)` — immutable contract.
- **Files chính:**
  - [`VisualAssertion.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.ActionContract/VisualAssertion.cs)

---

### ✅ T2.2 — ActionContractExecutor: Live Mode (DAV)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** DAV Pattern executor: preFrame → Tap → poll postFrame mỗi 33ms → verify → retry nếu fail.
- **Files chính:**
  - [`ActionContractExecutor.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.ActionContract/ActionContractExecutor.cs)
  - [`ExecutionMode.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.ActionContract/ExecutionMode.cs) — `enum { Live, Replay }`
- **Test kết quả:**
  - `T2.2.1` ✅ — [`ActionContractTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/ActionContractTests.cs): `Live_VerifyPass` — verify pass nhanh
  - `T2.2.2` ✅ — `Live_VerifyFail` — timeout + log warning/error
  - `T2.2.3` ✅ — `Live_Retry` — fail lần 1, pass lần 2
  - ✅ `Live_NullAssertion` — fire-and-forget
  - ✅ `InvalidClick_Negative` — throw ArgumentException
  - ✅ `RegionClamp` — vượt biên không crash

---

### ✅ T2.3 — ActionContractExecutor: Replay Mode (Bypass)
- **Trạng thái:** ✅ HOÀN THÀNH (gộp chung với T2.2)
- **Mô tả:** Chế độ `Replay` bỏ qua capture/poll, gửi thẳng input, pass tức thì.
- **Test kết quả:**
  - `T2.3.1` ✅ — `Replay_Bypass` — pass < 50ms, không gọi GrabFrame

---

### ✅ T2.4 — Gaussian Delay Generator (HumanDelay)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Box-Muller transform sinh delay Gaussian, clamp `[min, max]`. Constructor overloads cho deterministic test (seed).
- **Files chính:**
  - [`HumanDelay.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.AntiBan/HumanDelay.cs)
- **Presets:** `DropTroopTiming(80, 15, 50, 130)`, `NextButtonTiming(1800, 350, 1100, 3200)`
- **Test kết quả:**
  - `T2.4.1` ✅ — [`AntiBanTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AntiBanTests.cs): `NextDelay_StatisticalMean` — mean thực tế đúng khoảng
  - `T2.4.2` ✅ — `NextDelay_ClampBounds` — không vượt [min, max]
  - ✅ `NextDelay_SeedReproducibility` — cùng seed cho cùng kết quả
  - ✅ `NextDelay_MinEqualsMax`, `NextDelay_SigmaZero`
  - ✅ `NextDelay_MinGreaterThanMax_Throws`, `NextDelay_MeanBelowMin_Throws`, `NextDelay_MeanAboveMax_Throws`
  - ✅ `DelayAsync_CallsTimeProvider`, `DelayAsync_CancellationRespected`

---

### ⬜ T2.5 — Bezier Curve Mouse Path
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T2.6 — Bounding Box Offset Click
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T2.7 — Coordinate History (Anti-Repeat)
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T2.8 — Micro-Break Scheduler
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T2.9 — Circadian Sleep Schedule
- **Trạng thái:** ⬜ CHƯA LÀM
### ⬜ T2.10 — Per-Account Behavioral Seed
- **Trạng thái:** ⬜ CHƯA LÀM

---

## 🔧 REFACTOR TASKS (Bổ sung trong quá trình phát triển)

### ✅ R1 — ITimeProvider injection vào ActionContractExecutor + Win32PostMessageInputBackend
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Thay thế `Thread.Sleep()` bằng `ITimeProvider.DelayAsync()` để testable. Thêm `SystemTimeProvider` production implementation.
- **Files thay đổi:**
  - [`ActionContractExecutor.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.ActionContract/ActionContractExecutor.cs) — Constructor thêm `ITimeProvider`
  - [`Win32PostMessageInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/Win32PostMessageInputBackend.cs) — Dùng `_time.DelayAsync()` thay `Thread.Sleep()`
  - [`SystemTimeProvider.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Utils/SystemTimeProvider.cs) — Production implementation

---

### ✅ R2 — IProcessRunner abstraction cho ADB backends
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Tách `Process.Start()` thành interface `IProcessRunner` + `ProcessResult` record, cho phép mock ADB trong unit tests.
- **Files tạo mới:**
  - [`IProcessRunner.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IProcessRunner.cs) — `ProcessResult(ExitCode, RawStdOut, StdErr)` + interface
  - [`ProcessRunner.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Utils/ProcessRunner.cs) — Production implementation
  - [`MockProcessRunner.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/Mocks/MockProcessRunner.cs) — Queue-based mock
- **Files refactor:**
  - [`AdbScreencapCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/AdbScreencapCaptureBackend.cs) — Constructor nhận `IProcessRunner`
  - [`AdbInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/AdbInputBackend.cs) — Constructor nhận `IProcessRunner`
- **Test kết quả:**
  - ✅ [`AdbBackendTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AdbBackendTests.cs) — 11 tests pass, full coverage

---

### ✅ R3 — EmulatorTarget record (BREAKING CHANGE)
- **Trạng thái:** ✅ HOÀN THÀNH
- **Mô tả:** Thống nhất `Initialize()` signature thành `Initialize(EmulatorTarget target)` thay vì `Initialize(IntPtr, string)`. `EmulatorTarget` là immutable record với 3 optional fields.
- **Record definition:**
  ```csharp
  record EmulatorTarget(
      IntPtr? WindowHandle = null,
      string? AdbSerial = null,
      string? MockRecordingPath = null
  );
  ```
- **Files thay đổi (tất cả 8 implementations + callers):**
  - [`EmulatorTarget.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/EmulatorTarget.cs) — Record mới
  - [`ICaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/ICaptureBackend.cs) — `Initialize(EmulatorTarget)`
  - [`IInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core/Abstractions/IInputBackend.cs) — `Initialize(EmulatorTarget)`
  - [`Win32GdiCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/Win32GdiCaptureBackend.cs) — Dùng `target.WindowHandle`
  - [`AdbScreencapCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/AdbScreencapCaptureBackend.cs) — Dùng `target.AdbSerial`
  - [`MockReplayCaptureBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Capture/MockReplayCaptureBackend.cs) — Dùng `target.MockRecordingPath`
  - [`Win32PostMessageInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/Win32PostMessageInputBackend.cs) — Dùng `target.WindowHandle`
  - [`AdbInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/AdbInputBackend.cs) — Dùng `target.AdbSerial`
  - [`MockInputBackend.cs`](file:///f:/Mybot_PORT/src/MyBot.Core.Input/MockInputBackend.cs) — Always returns true
  - [`Program.cs`](file:///f:/Mybot_PORT/src/MyBot.App/Program.cs) — `new EmulatorTarget(...)`
  - [`CaptureTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/CaptureTests.cs)
  - [`AdbBackendTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AdbBackendTests.cs)
  - [`MockLayerTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/MockLayerTests.cs)

---

## 📦 BẢN ĐỒ FILE HIỆN TẠI

### Source Projects (`src/`)

| Project | Files chính | Trạng thái |
|---|---|:---:|
| `MyBot.Core` | 12 abstractions, `SerilogBotLogger`, `ProcessRunner`, `SystemTimeProvider` | ✅ Ổn định |
| `MyBot.Core.FSM` | `BotState`, `BotStateMachine`, `TransitionGuard`, `StateWatchdog` | ✅ Ổn định |
| `MyBot.Core.Capture` | `Win32GdiCaptureBackend`, `AdbScreencapCaptureBackend`, `MockReplayCaptureBackend` | ✅ Ổn định |
| `MyBot.Core.Input` | `Win32PostMessageInputBackend`, `AdbInputBackend`, `MockInputBackend` | ✅ Ổn định |
| `MyBot.Core.ActionContract` | `ActionContractExecutor`, `VisualAssertion`, `ExecutionMode` | ✅ Ổn định |
| `MyBot.Core.AntiBan` | `HumanDelay` | ✅ Ổn định |
| `MyBot.Core.Config` | `ConfigManager` | ✅ Ổn định |
| `MyBot.App` | `Program.cs` (Composition Root) | ✅ Ổn định |

### Test Projects (`tests/`)

| File Test | Số Tests | Trạng thái |
|---|:---:|:---:|
| [`AbstractionsTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AbstractionsTests.cs) | 2 | ✅ Pass |
| [`ActionContractTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/ActionContractTests.cs) | 6 | ✅ Pass |
| [`AdbBackendTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AdbBackendTests.cs) | 11 | ✅ Pass |
| [`AntiBanTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/AntiBanTests.cs) | 10 | ✅ Pass |
| [`CaptureTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/CaptureTests.cs) | 2 | ✅ Pass |
| [`ConfigTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/ConfigTests.cs) | 4 | ✅ Pass |
| [`DependencyInjectionTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/DependencyInjectionTests.cs) | 3 | ✅ Pass |
| [`FsmTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/FsmTests.cs) | 4 | ✅ Pass |
| [`LoggingTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/LoggingTests.cs) | 2 | ✅ Pass |
| [`MockLayerTests.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/MockLayerTests.cs) | 2 | ✅ Pass |
| [`Mocks/MockProcessRunner.cs`](file:///f:/Mybot_PORT/tests/MyBot.Core.Tests/Mocks/MockProcessRunner.cs) | — | Helper |
| **TỔNG** | **48** | **✅ 100% Pass** |

---

## 🚀 CÁC TASK NÊN LÀM TIẾP THEO (Theo thứ tự ưu tiên)

| Ưu tiên | Task | Lý do |
|:---:|---|---|
| 1 | **T2.5** — Bezier Curve Mouse Path | Hoàn thiện Anti-Ban core engine |
| 2 | **T2.6** — Bounding Box Offset Click | Anti-Ban, deps T2.4 (đã xong) |
| 3 | **T2.7** — Coordinate History | Anti-Ban, deps T2.6 |
| 4 | **T3.1** — Template Matcher (Single Scale) | Mở khóa Phase 3 (CV) |
| 5 | **T3.7** — State Classifier | "Bộ não" nhận diện màn hình game |
| 6 | **T1.7** — IEmulatorDriver + Plugin Registry | Mở khóa emulator plugins |
| 7 | **T2.8** — Micro-Break Scheduler | Anti-Ban scheduling |
| 8 | **T2.9** — Circadian Sleep Schedule | Anti-Ban 24/7 |

---

## 📝 GHI CHÚ KỸ THUẬT

### Quyết định kiến trúc quan trọng đã thực hiện
1. **EmulatorTarget record** (R3): Tất cả backends dùng chung `Initialize(EmulatorTarget)` thay vì params rời rạc. Mở rộng dễ dàng bằng cách thêm property mới vào record.
2. **IProcessRunner abstraction** (R2): ADB backends testable 100% mà không cần device thật.
3. **ITimeProvider injection** (R1): Loại bỏ `Thread.Sleep()` khỏi production code, test chạy instant.
4. **FSM đẩy lên Phase 0**: `BotState` + `TransitionGuard` có trước để toàn bộ logic kế thừa.

### Giới hạn hiện tại
- **ADB Screencap FPS**: ~1.7 FPS do khởi tạo process mới mỗi frame. Sẽ cải thiện ở T1.11 (Fallback Chain) hoặc khi tích hợp Scrcpy.
- **MockReplayCaptureBackend**: Chỉ đọc ảnh từ thư mục, chưa hỗ trợ `.mkv` FFV1 (đợi T0.8).
- **Win32 GDI Capture**: Chưa có fallback `PrintWindow`/`DesktopDuplication` (đợi T1.11).
