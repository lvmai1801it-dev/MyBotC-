# KẾ HOẠCH TRIỂN KHAI MYBOT C# WPF (BẢN HOÀN THIỆN V2.0 — FIX DEPENDENCIES & BỔ SUNG ĐỦ SCOPE)

> **Phương pháp:** Chia dự án thành **11 Phase / 16 Sprint** (~4 tháng). Mỗi task ≤ 4 giờ. Mỗi task có ≥ 2 test case (Happy + Edge). Tổng **145 tasks** với **~240 test cases**.
>
> **Ước tính Effort thực tế:** ~415 giờ (đã áp dụng hệ số an toàn $1.35\times$ tính cả debug, edge cases và test integration).
>
> **Tech stack:** .NET 8, WPF, MVVM (CommunityToolkit.Mvvm), DI (Microsoft.Extensions.DependencyInjection), OpenCvSharp4.Windows, xUnit + FluentAssertions + Moq, EF Core (SQLite), Serilog, GitHub Actions.
>
> **Tham chiếu:** Spec 3.2 §X.Y, ARCHITECTURE_DECISIONS.md §X, UI/UX Spec §X.Y.

---

## 📐 CẤU TRÚC SOLUTION HOÀN CHỈNH

```
MyBot.sln
├── src/
│   ├── MyBot.Core/                    # Interfaces, Domain Primitives, BotState enum, Watchdog contracts
│   ├── MyBot.Core.FSM/                # FSM Core + Transition Guard Matrix (Đẩy lên Phase 0)
│   ├── MyBot.Core.Capture/            # ICaptureBackend + Win32/ADB/MockReplay
│   ├── MyBot.Core.Input/              # IInputBackend + Win32/ADB/MockInput
│   ├── MyBot.Core.CV/                 # Template matcher, OCR ONNX engine
│   ├── MyBot.Core.AntiBan/            # Gaussian, Bezier, scheduler, behavioral seed
│   ├── MyBot.Core.ActionContract/     # DAV pattern executor + Replay bypass
│   ├── MyBot.Core.Config/             # JSON config, schema migration, backup/export
│   ├── MyBot.Core.Data/               # SQLite + EF Core
│   ├── MyBot.Core.Notification/       # INotifier (Discord, Telegram)
│   ├── MyBot.Game.CoC/                # Game logic
│   │   ├── Math/                      # Isometric, Red Area, Deadbase classifier
│   │   ├── Combat/                    # CSV parser, drop executor, pet & equipment runtime
│   │   └── Village/                   # Harvest, upgrade, train, Clan Games, Clan Capital, BB
│   ├── MyBot.UI/                      # WPF main app shell (475x650)
│   ├── MyBot.UI.Tabs.Log/             # 7 dynamic tab projects
│   ├── MyBot.UI.Tabs.Village/
│   ├── MyBot.UI.Tabs.Attack/
│   ├── MyBot.UI.Tabs.Bot/
│   ├── MyBot.UI.Tabs.Stats/
│   ├── MyBot.UI.Tabs.About/
│   ├── MyBot.UI.Tabs.Instances/        # Multi-instance Dashboard
│   ├── MyBot.Tools/                   # record_session, validate_templates, ocr_dataset_builder
│   └── MyBot.App/                     # Composition root (Program.cs, DPI Awareness)
├── plugins/
│   ├── MyBot.Plugins.Emulator.LDPlayer/
│   ├── MyBot.Plugins.Emulator.BlueStacks5/
│   ├── MyBot.Plugins.Emulator.MEmu/
│   └── MyBot.Plugins.Emulator.Nox/
├── tests/
│   ├── MyBot.Core.Tests/
│   ├── MyBot.Game.CoC.Tests/
│   ├── MyBot.UI.Tests/
│   ├── MyBot.Integration.Tests/
│   └── recordings/                    # Golden recordings (.mkv + .jsonl)
├── tools/
├── docs/
│   ├── adr/
│   ├── CONTRIBUTING.md
│   └── KNOWN_LIMITATIONS.md
├── .github/workflows/
├── Directory.Build.props
├── LICENSE                              # GPL-3.0
├── DISCLAIMER.md
└── README.md
```

---

## 🏗️ PHASE 0 — SOLUTION FOUNDATION, FSM PRIMITIVES & DATASET (Sprint 1, Week 1)

Mục tiêu: Repo build xanh, CI chạy được, FSM Core & State Matrix định nghĩa xong ngay từ đầu để toàn bộ UI và Logic kế thừa, bộ OCR Dataset sẵn sàng.

### **T0.1 — Khởi tạo Solution & Project Structure**
- **Mô tả:** Tạo `.sln` với 16+ project theo cấu trúc trên. Cấu hình `Directory.Build.props` (LangVersion=12, Nullable=enable, TreatWarningsAsErrors=true).
- **Files:** `MyBot.sln`, `Directory.Build.props`, các file `.csproj`
- **Effort:** 1h | **Deps:** Không
- **Test cases:**
  - `T0.1.1` — `dotnet restore` thành công không warning.
  - `T0.1.2` — `dotnet build` ra các file `.dll` trong `bin/`.
  - `T0.1.3` — Reference graph đúng: `MyBot.Game.CoC` không reference `MyBot.UI` (dependency inversion).
- **Acceptance:** `dotnet build` xanh, không circular dependency.

### **T0.2 — Core Abstractions (Interfaces chỉ định nghĩa, không impl)**
- **Mô tả:** Tạo tất cả interface trong `MyBot.Core`: `ICaptureBackend`, `IInputBackend`, `IEmulatorDriver`, `INotifier`, `ITab`, `IStateClassifier`, `ITemplateMatcher`, `IOcrEngine`, `ITimeProvider`.
- **Files:** `src/MyBot.Core/Abstractions/*.cs`
- **Effort:** 2h | **Deps:** T0.1
- **Test cases:**
  - `T0.2.1` — Reflection test: mỗi interface có ≥ 2 method, không có property setter.
  - `T0.2.2` — `IStateClassifier.Classify(Mat) → GameState` với `GameState` là enum.
- **Acceptance:** Dev có thể viết impl mới mà không sửa file interface.

### **T0.3 — DI Container + Composition Root + DPI Awareness**
- **Mô tả:** Setup `Microsoft.Extensions.DependencyInjection`. `MyBot.App/Program.cs` cấu hình logging, config, plugins, tabs. Gọi Win32 `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)`.
- **Files:** `src/MyBot.App/Program.cs`, `src/MyBot.App/Startup.cs`
- **Effort:** 2h | **Deps:** T0.2
- **Test cases:**
  - `T0.3.1` — Resolve `ITimeProvider` cho 2 instance khác nhau → cùng reference (singleton).
  - `T0.3.2` — Resolve `ITab` cho `LogTab` → mỗi lần là instance mới (transient).
  - `T0.3.3` — Win32 DPI context khởi tạo thành công không throw exception.
- **Acceptance:** Composition root chạy được, không crash lúc khởi động.

### **T0.4 — Logging Infrastructure (Serilog)**
- **Mô tả:** Serilog với 3 sink: Console (dev), File `logs/bot_YYYY-MM-DD.log` (roll theo ngày, giữ 14 ngày), RichTextBox sink cho UI. Interface `IBotLogger`.
- **Files:** `src/MyBot.Core/Logging/*.cs`
- **Effort:** 2h | **Deps:** T0.3
- **Test cases:**
  - `T0.4.1` — Log 100 message → file có đúng 100 dòng.
  - `T0.4.2` — Xoay ngày → file mới xuất hiện, file cũ giữ nguyên.
  - `T0.4.3` — Log level filter hoạt động chuẩn xác theo config.
- **Acceptance:** Log xuất hiện trong file + UI trong ≤ 100ms.

### **T0.5 — Configuration System (JSON + Schema Validation)**
- **Mô tả:** Load `config.json`, validate với JSON Schema (NJsonSchema). Hỗ trợ migration `config_version` (v1 → v2). `IConfigManager` với `Load()`, `Save()`, `Validate()`, `Migrate()`.
- **Files:** `src/MyBot.Core.Config/*.cs`, `docs/config-schema.json`
- **Effort:** 4h | **Deps:** T0.3
- **Test cases:**
  - `T0.5.1` — Load config hợp lệ → bind object chính xác.
  - `T0.5.2` — Config thiếu field bắt buộc → throw `ConfigValidationException` rõ tên field.
  - `T0.5.3` — Auto-migrate config version cũ lên bản mới tự chèn default values.
- **Acceptance:** User xóa `config.json` → tự sinh file default chuẩn.

### **T0.6 — GitHub Actions CI/CD Pipeline**
- **Mô tả:** Workflow 4 job: `lint` (dotnet format), `build` (windows-latest), `test` (ubuntu-latest dùng replay recordings), `package` (self-contained exe).
- **Files:** `.github/workflows/ci.yml`
- **Effort:** 2h | **Deps:** T0.1, T0.4, T0.5
- **Test cases:**
  - `T0.6.1` — Commit sai format code → job lint fail có log rõ ràng.
  - `T0.6.2` — Test suite chạy xanh trên môi trường Ubuntu không cần GPU/giả lập.
- **Acceptance:** PR đầu tiên có status badge xanh.

### **T0.7 — Documentation Starter Pack**
- **Mô tả:** `LICENSE` (GPL-3.0), `DISCLAIMER.md`, `CONTRIBUTING.md`, `docs/adr/0001-to-0005.md` (chép 5 ADR từ ARCHITECTURE_DECISIONS).
- **Files:** 8 markdown files
- **Effort:** 2h | **Deps:** Không
- **Test cases:**
  - `T0.7.1` — `grep -r "TBD\|TODO\|FIXME" docs/` ra 0 kết quả.
  - `T0.7.2` — Markdown link check không có link gãy (404).
- **Acceptance:** Docs đầy đủ để người ngoài đọc hiểu cách contribute trong 15 phút.

### **T0.8 — Record Session Tool (CLI)**
- **Mô tả:** Console tool `tools/record_session` ghi màn hình giả lập 30fps thành `.mkv` (FFV1 lossless) + `telemetry.jsonl`. Tạm thời dùng **ADB raw screencap socket** độc lập để ghi hình, không bị phụ thuộc vào `ICaptureBackend` của Phase 1.
- **Files:** `tools/record_session/Program.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T0.8.1` — Chạy 5s với fake capture (ảnh tĩnh lặp) → file `.mkv` có ≥ 140 frames.
  - `T0.8.2` — Codec verify đúng FFV1 bằng `ffprobe`.
  - `T0.8.3` — Nhấn Ctrl+C giữa chừng → file `.mkv` đóng chuẩn, không bị corrupt.
- **Acceptance:** Ghi hình mượt, phục vụ test replay độc lập.

### **T0.9 — FSM Core (10-States Enum & StateMachine Primitives)** *(Chuyển từ Phase 8 lên)*
- **Mô tả:** Định nghĩa enum `BotState` (10 trạng thái chuẩn) trong `MyBot.Core`. Xây dựng `BotStateMachine` với `Transition(newState)` thread-safe, kích hoạt event `StateChanged`.
- **Files:** `src/MyBot.Core/Enums/BotState.cs`, `src/MyBot.Core.FSM/BotStateMachine.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T0.9.1` — Chuỗi chuyển tiếp hợp lệ: `IDLE` → `STARTING` → `SEARCHING` → `PAUSING` → `PAUSED` → `RESUMING` → `SEARCHING` → `STOPPING` → `IDLE`.
  - `T0.9.2` — Chuyển trạng thái bất hợp lệ (VD: `IDLE` → `BATTLE`) → throw `InvalidTransitionException`.
  - `T0.9.3` — Gọi transition đa luồng đồng thời → thread-safe không gây race condition.
- **Acceptance:** Cung cấp `BotState` hoàn chỉnh cho toàn bộ UI và Logic kế thừa.

### **T0.10 — Transition Guard Matrix** *(Chuyển từ Phase 8 lên)*
- **Mô tả:** Hiện thực ma trận 2D boolean `validTransitions[from][to]` đối chiếu chuẩn xác bảng ma trận 10 state trong kiến trúc.
- **Files:** `src/MyBot.Core.FSM/TransitionGuardMatrix.cs`
- **Effort:** 1h | **Deps:** T0.9
- **Test cases:**
  - `T0.10.1` — 100% các ô trong ma trận khớp với spec architecture.
  - `T0.10.2` — Self-transition (tự chuyển sang chính mình) luôn trả về `false`.
  - `T0.10.3` — `ERROR` → `IDLE` trả về `true` (cho phép reset phục hồi).
- **Acceptance:** Khóa chặt các luồng chuyển state sai luật.

### **T0.11 — OCR Dataset Collector Tool** *(Bổ sung mới)*
- **Mô tả:** Tool trích xuất 500-1000 ảnh mẫu chữ số tài nguyên, cúp, thời gian từ thư mục ảnh/video golden recordings của MyBot gốc, gán nhãn tự động + thủ công vào `dataset/ocr_digits/`.
- **Files:** `tools/ocr_dataset_builder/Program.cs`
- **Effort:** 4h | **Deps:** T0.8
- **Test cases:**
  - `T0.11.1` — Xuất ra 500 ảnh nhãn kích thước chuẩn nhị phân (grayscale).
  - `T0.11.2` — Bảng `labels.csv` có đầy đủ nhãn số từ 0 đến 9 và các ký tự `,`, `.`, `/`, `M`, `k`.
- **Acceptance:** Có đủ tập dữ liệu kiểm thử độc lập cho OCR.

### **T0.12 — OCR Engine Model Prep (ONNX / Custom Tesseract)** *(Bổ sung mới)*
- **Mô tả:** Đóng gói ONNX model (`ocr_digits.onnx`) hoặc bộ train font Supercell nhị phân sẵn có từ MyBot gốc vào thư mục assets của project `MyBot.Core.CV`.
- **Files:** `src/MyBot.Core.CV/Assets/ocr_digits.onnx`
- **Effort:** 3h | **Deps:** T0.11
- **Test cases:**
  - `T0.12.1` — Model load thành công qua thư viện OnnxRuntime không lỗi input tensor.
  - `T0.12.2` — Chạy test trên 100 ảnh mẫu dataset đạt accuracy ≥ 99%.
- **Acceptance:** Asset model sẵn sàng cho Phase 3.

### **T0.13 — License Compliance Audit** *(Bổ sung mới)*
- **Mô tả:** Chạy scan license toàn bộ dependency NuGet transitive (`dotnet list package --include-transitive`), đảm bảo không có thư viện nào xung đột với giấy phép GPL-3.0.
- **Files:** `docs/THIRD_PARTY_LICENSES.md`
- **Effort:** 2h | **Deps:** T0.1
- **Test cases:**
  - `T0.13.1` — 100% các package NuGet (OpenCvSharp4, CommunityToolkit, Serilog, EF Core) thuộc nhóm giấy phép tương thích GPL-3.0 (MIT, Apache 2.0, BSD-3).
  - `T0.13.2` — Không chứa module non-free thương mại độc quyền.
- **Acceptance:** Dự án hợp lệ 100% về mặt pháp lý mã nguồn mở.

---

## 🎮 PHASE 1 — DRIVER LAYER & EMULATOR PLUGINS (Sprint 2-3, Week 2-3)

### **T1.0 — Window Normalizer & Resize Utility** *(Chuyển lên trước T1.1)*
- **Mô tả:** Utility chuẩn hoá kích thước cửa sổ giả lập về đúng **860 × 732 logical pixels** bằng Win32 `SetWindowPos`. Tự động tính toán bù trừ viền cửa sổ và DPI scale.
- **Files:** `src/MyBot.Core/Utils/WindowNormalizer.cs`
- **Effort:** 2h | **Deps:** T0.3
- **Test cases:**
  - `T1.0.1` — Cửa sổ bất kỳ kích thước → resize về đúng client area 860×732.
  - `T1.0.2` — Màn hình Windows DPI 150% → tính toán đúng client area logical không lệch toạ độ.
  - `T1.0.3` — Cửa sổ minimized → trả warning, không crash.
- **Acceptance:** Chuẩn hoá kích thước chuẩn trước khi capture.

### **T1.1 — ICaptureBackend Implementation: Win32 GDI (BitBlt)**
- **Mô tả:** Class `Win32GdiCaptureBackend`. Dùng `BitBlt` từ DC của cửa sổ emulator. Tối ưu: reuse `HBITMAP`, `HDC` qua `IDisposable`.
- **Files:** `src/MyBot.Core.Capture/Win32GdiCapture.cs`
- **Effort:** 4h | **Deps:** T1.0
- **Test cases:**
  - `T1.1.1` — Capture 100 frame liên tiếp → không tăng GDI handle count (`GetGuiResources`).
  - `T1.1.2` — Trả `Mat` kích thước chuẩn 860×732, `CV_8UC3`.
  - `T1.1.3` — Benchmark: 100 frame < 1.5 giây (≥ 66 fps).
- **Acceptance:** 0 GDI leak sau 10,000 lần capture liên tục.

### **T1.2 — ICaptureBackend: ADB Screencap**
- **Mô tả:** Class `AdbScreencapCaptureBackend`. Dùng `adb exec-out screencap -p` stream trực tiếp PNG bytes, decode inline không ghi file tạm.
- **Files:** `src/MyBot.Core.Capture/AdbScreencapCapture.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T1.2.1` — Decode mảng byte PNG từ ADB socket thành `Mat` OpenCV chuẩn xác.
  - `T1.2.2` — ADB timeout 5s → throw `CaptureTimeoutException` an toàn.
  - `T1.2.3` — Benchmark độ trễ frame ≤ 200ms.
- **Acceptance:** Đóng sạch ADB socket khi Dispose.

### **T1.3 — MockReplayCaptureBackend**
- **Mô tả:** Đọc frame từ `.mkv` (FFV1) + `telemetry.jsonl` phục vụ kiểm thử replay tự động. Hỗ trợ cờ `loop`.
- **Files:** `src/MyBot.Core.Capture/MockReplayCapture.cs`
- **Effort:** 2h | **Deps:** T0.8
- **Test cases:**
  - `T1.3.1` — Đọc 900 frame từ recording 30s → trả đủ 900 frame tuần tự.
  - `T1.3.2` — `loop=false` gặp EOF trả `null`; `loop=true` quay lại frame 0.
  - `T1.3.3` — `GetTelemetryAtFrame(500)` trả đúng entry JSON tương ứng frame 500.
- **Acceptance:** Replay test deterministic 100% giữa các lần chạy.

### **T1.4 — IInputBackend: Win32 PostMessage**
- **Mô tả:** Gửi `WM_LBUTTONDOWN`, `WM_MOUSEMOVE`, `WM_LBUTTONUP` tới hwnd giả lập. Hỗ trợ tap và swipe.
- **Files:** `src/MyBot.Core.Input/Win32PostMessageInput.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T1.4.1` — `tap(100, 200)` gửi đúng 2 message DOWN + UP với lParam đóng gói chuẩn.
  - `T1.4.2` — `swipe` phát sinh đủ chuỗi sự kiện `WM_MOUSEMOVE` theo thời gian quy định.
  - `T1.4.3` — Toạ độ click âm hoặc vượt quá biên → throw `ArgumentOutOfRangeException`.
- **Acceptance:** Click thành công trên background window không cần chiếm chuột người dùng.

### **T1.5 — IInputBackend: ADB Input**
- **Mô tả:** Thực thi tap/swipe qua ADB socket persistent kết nối trực tiếp emulator daemon.
- **Files:** `src/MyBot.Core.Input/AdbInput.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T1.5.1` — Gửi lệnh tap đúng cú pháp `input tap x y`.
  - `T1.5.2` — Giữ socket persistent, không sinh thêm tiến trình con `adb.exe`.
  - `T1.5.3` — Latency trung bình ≤ 180ms/tap.
- **Acceptance:** Tap mượt mà trên LDPlayer/BlueStacks qua ADB.

### **T1.6 — MockInputBackend (cho Replay Testing)**
- **Mô tả:** Lưu lại danh sách hành động vào bộ nhớ `RecordedActions`, hỗ trợ `AssertSubsequence()` và `Clear()`.
- **Files:** `src/MyBot.Core.Input/MockInput.cs`
- **Effort:** 2h | **Deps:** T0.2
- **Test cases:**
  - `T1.6.1` — Ghi nhận chính xác toạ độ, loại hành động và timestamp từ `ITimeProvider`.
  - `T1.6.2` — `Clear()` giải phóng sạch danh sách.
- **Acceptance:** Cung cấp hạ tầng cho Layer 2 test framework.

### **T1.7 — IEmulatorDriver Interface + Plugin Registry**
- **Mô tả:** Interface `IEmulatorDriver` + cơ chế tự quét assembly attribute `[EmulatorPlugin("name")]` lúc khởi động.
- **Files:** `src/MyBot.Core/Emulator/*.cs`, `src/MyBot.App/PluginLoader.cs`
- **Effort:** 3h | **Deps:** T0.3, T1.1-T1.6
- **Test cases:**
  - `T1.7.1` — Load assembly plugin → Registry tự động đăng ký key tương ứng.
  - `T1.7.2` — Plugin lỗi hoặc trùng tên → log warning/exception kiểm soát tốt.
- **Acceptance:** Mở rộng emulator mới chỉ cần thêm DLL plugin vào folder.

### **T1.8 — LDPlayer 9 Driver Plugin**
- **Mô tả:** Nhận diện tiến trình `dnplayer.exe`, đọc ADB port từ `ldconsole.exe list2`, attach vào window class `LDPlayerMainFrame`.
- **Files:** `plugins/MyBot.Plugins.Emulator.LDPlayer/*.cs`
- **Effort:** 4h | **Deps:** T1.7
- **Test cases:**
  - `T1.8.1` — Quét đúng danh sách instance LDPlayer và port ADB đang chạy.
  - `T1.8.2` — Attach và capture được hình ảnh game không bị màn hình đen.
- **Acceptance:** Điều khiển thực tế mượt trên LDPlayer 9.

### **T1.9 — BlueStacks 5 Driver Plugin**
- **Mô tả:** Đọc `bluestacks.conf` trích xuất ADB port. Attach trực tiếp vào child handle `subWin` để lấy hình ảnh chuẩn.
- **Files:** `plugins/MyBot.Plugins.Emulator.BlueStacks5/*.cs`
- **Effort:** 4h | **Deps:** T1.7
- **Test cases:**
  - `T1.9.1` — Parse file cấu hình lấy đúng port ADB.
  - `T1.9.2` — Attach vào `subWin` chụp BitBlt ra hình ảnh game chuẩn.
- **Acceptance:** Tương thích với BlueStacks 5 bản mới nhất.

### **T1.10 — MEmu + Nox Plugins**
- **Mô tả:** MEmu plugin (xử lý DPI Per-Monitor) và Nox plugin (cơ chế fallback DesktopDuplication khi BitBlt trả về đen).
- **Files:** `plugins/MyBot.Plugins.Emulator.MEmu/*.cs`, `plugins/MyBot.Plugins.Emulator.Nox/*.cs`
- **Effort:** 4h | **Deps:** T1.7
- **Test cases:**
  - `T1.10.1` — MEmu click trúng toạ độ ngay cả khi Windows đặt DPI 125%/150%.
  - `T1.10.2` — Nox tự động fallback sang DesktopDuplication khi BitBlt đen màn hình.
- **Acceptance:** Hỗ trợ đủ cả 4 giả lập phổ biến.

### **T1.11 — Runtime Detection & Fallback Chain**
- **Mô tả:** Chuỗi tự động dò tìm capture khả dụng: `BitBlt` → `PrintWindow` → `DesktopDuplication` → `ADB`. Tự động kiểm tra tính sống động của frame (Sanity Check diff > threshold).
- **Files:** `src/MyBot.Core.Capture/CaptureFallbackChain.cs`
- **Effort:** 3h | **Deps:** T1.1, T1.2
- **Test cases:**
  - `T1.11.1` — Giả lập DirectX bình thường → tự chọn BitBlt (nhanh nhất).
  - `T1.11.2` — Giả lập bị đen hình → tự nhảy fallback sang ADB.
  - `T1.11.3` — Tất cả fail → throw `NoCaptureBackendException` hướng dẫn user.
- **Acceptance:** Tự động kết nối thành công với mọi thiết lập đồ hoạ của user.

---

## 🛡️ PHASE 2 — ACTION CONTRACT & ANTI-BAN (Sprint 3, Week 4)

### **T2.1 — VisualAssertion Record Class**
- **Mô tả:** `record VisualAssertion(Rect Region, Func<Mat,Mat,bool> VerifyFn, int TimeoutMs, string Description)`.
- **Files:** `src/MyBot.Core.ActionContract/VisualAssertion.cs`
- **Effort:** 1h | **Deps:** T0.2
- **Test cases:**
  - `T2.1.1` — Record bất biến, readonly properties.
  - `T2.1.2` — `VerifyFn` đánh giá độ biến thiên pixel absdiff chính xác giữa 2 ảnh.
- **Acceptance:** Immutable contract cho tầng kiểm tra hình ảnh.

### **T2.2 — ActionContractExecutor: Live Mode (DAV)**
- **Mô tả:** Thực thi chu trình Detection $\rightarrow$ Action $\rightarrow$ Verification cho Live mode. Poll mỗi 33ms đến timeout, retry nếu thất bại.
- **Files:** `src/MyBot.Core.ActionContract/ActionContractExecutor.cs`
- **Effort:** 4h | **Deps:** T1.4, T1.6, T2.1
- **Test cases:**
  - `T2.2.1` — Màn hình thay đổi sau khi click → verify pass ≤ 100ms.
  - `T2.2.2` — Màn hình đứng yên → verify fail sau timeout, log warning.
  - `T2.2.3` — Thất bại lần 1, thành công lần 2 → retry pass thành công.
- **Acceptance:** Triệt tiêu hoàn toàn race condition double-click.

### **T2.3 — ActionContractExecutor: Replay Mode (Bypass)**
- **Mô tả:** Khi bot chạy trong chế độ `REPLAY`, `Execute()` chỉ phát lệnh vào `MockInput` và bỏ qua việc chụp/poll chờ đợi màn hình.
- **Files:** `src/MyBot.Core.ActionContract/ActionContractExecutor.cs`
- **Effort:** 1h | **Deps:** T2.2
- **Test cases:**
  - `T2.3.1` — Ở replay mode, lệnh pass tức thì không tốn thời gian sleep.
  - `T2.3.2` — Chạy 50 action liên tiếp hoàn thành trong < 100ms.
- **Acceptance:** Giúp replay test suite chạy nhanh gấp 10-20 lần.

### **T2.4 — Gaussian Delay Generator**
- **Mô tả:** Sinh thời gian trễ ngẫu nhiên theo phân phối chuẩn Gaussian $N(\mu, \sigma)$ sử dụng thuật toán Box-Muller, clamp trong khoảng `[min, max]`.
- **Files:** `src/MyBot.Core.AntiBan/HumanDelay.cs`
- **Effort:** 1h | **Deps:** T0.2
- **Test cases:**
  - `T2.4.1` — 10,000 mẫu với $\mu=80, \sigma=15$, clamp `[50, 130]` → mean thực tế $\in [78, 82]$, std $\in [13, 17]$.
  - `T2.4.2` — Không có mẫu nào vượt ra ngoài khoảng `[min, max]`.
- **Acceptance:** Phân phối thống kê chuẩn xác, có thể truyền seed test.

### **T2.5 — Bezier Curve Mouse Path**
- **Mô tả:** Sinh đường cong chuột Cubic Bezier giữa 2 điểm với điểm điều khiển ngẫu nhiên. Tỷ lệ 8% tạo hiện tượng overshoot (vượt đích 3-8px rồi rê ngược lại).
- **Files:** `src/MyBot.Core.AntiBan/BezierPath.cs`
- **Effort:** 3h | **Deps:** T2.4
- **Test cases:**
  - `T2.5.1` — Quỹ đạo có ít nhất 10 điểm, điểm đầu và cuối chuẩn xác.
  - `T2.5.2` — Quỹ đạo uốn cong tự nhiên, trung điểm không thẳng hàng.
  - `T2.5.3` — Kiểm thử 1000 đường cong xuất hiện ~8% lượt có overshoot.
- **Acceptance:** Chuyển động chuột tự nhiên, đánh lừa hệ thống giám sát hành vi.

### **T2.6 — Bounding Box Offset Click**
- **Mô tả:** Click phân tán trong vùng Rect theo phân phối 2D Gaussian $N(0.5, 0.12)$, giới hạn trong khoảng $[0.2, 0.8]$ của box, không click sát mép.
- **Files:** `src/MyBot.Core.AntiBan/BoundingBoxClick.cs`
- **Effort:** 1h | **Deps:** T2.4
- **Test cases:**
  - `T2.6.1` — Toạ độ click luôn nằm trọn trong vùng an toàn của hình chữ nhật.
  - `T2.6.2` — Toạ độ click có phương sai tự nhiên, không bao giờ cố định 1 điểm.
- **Acceptance:** Click tự nhiên như người bấm tay trên màn hình cảm ứng.

### **T2.7 — Coordinate History (Anti-Repeat)**
- **Mô tả:** Lưu 5 toạ độ click gần nhất. Nếu toạ độ mới cách điểm cũ < 4px $\rightarrow$ tự động tái tạo toạ độ khác (tối đa 10 lần).
- **Files:** `src/MyBot.Core.AntiBan/ClickHistory.cs`
- **Effort:** 1h | **Deps:** T2.6
- **Test cases:**
  - `T2.7.1` — 6 lần click liên tiếp vào cùng một icon đều cách nhau ≥ 4px.
  - `T2.7.2` — Cơ chế fallback an toàn khi kích thước icon quá nhỏ.
- **Acceptance:** Không để lại dấu vết pixel lặp lại trong log server game.

### **T2.8 — Micro-Break Scheduler**
- **Mô tả:** Cứ sau mỗi 15-25 trận đánh (ngẫu nhiên), bot chủ động nghỉ 5-15 phút.
- **Files:** `src/MyBot.Core.AntiBan/BreakScheduler.cs`
- **Effort:** 2h | **Deps:** T0.5
- **Test cases:**
  - `T2.8.1` — Tự động kích hoạt trạng thái nghỉ khi đạt số trận ngẫu nhiên.
  - `T2.8.2` — Sau khi nghỉ xong, bộ đếm số trận reset về 0.
- **Acceptance:** Mô phỏng thói quen nghỉ ngơi của người chơi thực tế.

### **T2.9 — Circadian Sleep Schedule**
- **Mô tả:** Cấu hình khung giờ hoạt động (VD: 06:00 đến 23:30). Ngoài giờ này bot chuyển sang trạng thái ngủ, hỗ trợ chế độ thức dậy ngắn để thu hoạch mỏ.
- **Files:** `src/MyBot.Core.AntiBan/CircadianSchedule.cs`
- **Effort:** 2h | **Deps:** T0.5
- **Test cases:**
  - `T2.9.1` — Thời gian 03:00 sáng $\rightarrow$ `ShouldSleep() == true`.
  - `T2.9.2` — Thức dậy đúng 1 lần trong đêm theo cấu hình rồi ngủ tiếp.
- **Acceptance:** Hoạt động an toàn 24/7 theo nhịp sinh học.

### **T2.10 — Per-Account Behavioral Seed**
- **Mô tả:** Mỗi tài khoản gán 1 behavioral seed trong config. Seed này nạp vào toàn bộ bộ sinh ngẫu nhiên (delay, bezier, click offset) để tạo "vân tay hành vi" riêng biệt cho từng acc.
- **Files:** `src/MyBot.Core.AntiBan/BehavioralSeed.cs`
- **Effort:** 1h | **Deps:** T2.4-T2.6
- **Test cases:**
  - `T2.10.1` — Account A và B có mẫu trễ và quỹ đạo chuột hoàn toàn khác nhau.
  - `T2.10.2` — Cùng Account A sau khi khởi động lại app vẫn duy trì pattern cũ.
- **Acceptance:** Tránh bị ban chùm khi chạy nhiều tài khoản trên cùng máy.

---

## 👁️ PHASE 3 — COMPUTER VISION & OCR (Sprint 4-5, Week 5-6)

### **T3.1 — Template Matcher (Single Scale)**
- **Mô tả:** `FindTemplate` và `FindAll` dùng `Cv2.MatchTemplate` + `Cv2.MinMaxLoc`, hỗ trợ threshold và lọc các match trùng lặp gần nhau.
- **Files:** `src/MyBot.Core.CV/TemplateMatcher.cs`
- **Effort:** 3h | **Deps:** T0.2
- **Test cases:**
  - `T3.1.1` — Tìm needle trong haystack chuẩn $\rightarrow$ trả score 1.0 đúng toạ độ.
  - `T3.1.2` — Không có needle $\rightarrow$ trả danh sách rỗng.
  - `T3.1.3` — `FindAll` tìm đủ số lượng đối tượng xuất hiện trên màn hình.
- **Acceptance:** Khớp chính xác các nút bấm và icon của CoC.

### **T3.2 — Multi-Scale Template Matcher**
- **Mô tả:** Bao bọc bộ so khớp trong vòng lặp tỷ lệ $[0.8, 1.0, 1.2]$ kèm pyramid tối ưu, xử lý các trường hợp UI bị co giãn nhẹ.
- **Files:** `src/MyBot.Core.CV/MultiScaleTemplateMatcher.cs`
- **Effort:** 3h | **Deps:** T3.1
- **Test cases:**
  - `T3.2.1` — Template co nhỏ 10% vẫn nhận diện ra với confidence ≥ 0.85.
  - `T3.2.2` — Hiệu năng quét đa tỷ lệ không chậm quá 3.5 lần so với single scale.
- **Acceptance:** Chống chịu tốt với các tỷ lệ co giãn màn hình.

### **T3.3 — Template Metadata Loader**
- **Mô tả:** Đọc `metadata.json` từng thư mục template để kiểm tra phiên bản game hỗ trợ và theme giao diện (mùa đông/bình thường).
- **Files:** `src/MyBot.Core.CV/TemplateMetadata.cs`
- **Effort:** 2h | **Deps:** T0.5
- **Test cases:**
  - `T3.3.1` — Parse đúng schema metadata của bộ template.
  - `T3.3.2` — Báo warning rõ ràng khi template thuộc phiên bản CoC cũ.
- **Acceptance:** Quản lý vòng đời template hình ảnh chặt chẽ.

### **T3.4 — Template Health Validator Tool**
- **Mô tả:** CLI tool chạy kiểm tra toàn bộ kho template trên tập golden recordings, cảnh báo các template có độ tin cậy thấp (< 0.7) để cập nhật.
- **Files:** `tools/validate_templates/Program.cs`
- **Effort:** 3h | **Deps:** T3.1, T1.3
- **Test cases:**
  - `T3.4.1` — Template hợp lệ báo confidence > 0.9.
  - `T3.4.2` — Xuất báo cáo CSV thống kê chi tiết tỷ lệ match của từng template.
- **Acceptance:** Maintainer chạy trước mỗi bản release để loại bỏ template hỏng.

### **T3.5 — OCR Engine: Custom Model Loading (ONNX)**
- **Mô tả:** Nạp `ocr_digits.onnx` qua `Microsoft.ML.OnnxRuntime`. Giao diện `IOcrEngine.Recognize(Mat region) → string`.
- **Files:** `src/MyBot.Core.CV/OcrEngine.cs`
- **Effort:** 4h | **Deps:** T0.2, T0.12
- **Test cases:**
  - `T3.5.1` — Nạp model thành công, `IsReady == true`.
  - `T3.5.2` — Nhận diện chuỗi ảnh số tài nguyên CoC đạt accuracy ≥ 99%.
  - `T3.5.3` — Benchmark: 100 lần gọi OCR mất < 3.5 giây (≤ 35ms/lần).
- **Acceptance:** Tốc độ cao, không cần cài đặt tesseract bên ngoài.

### **T3.6 — OCR Post-Processing (Regex & Text Normalization)**
- **Mô tả:** Chuẩn hoá text sau OCR: xử lý "1.2M" $\rightarrow$ 1,200,000; sửa lỗi quang học "O" $\rightarrow$ "0", "l" $\rightarrow$ "1", "S" $\rightarrow$ "5".
- **Files:** `src/MyBot.Core.CV/OcrPostProcessor.cs`
- **Effort:** 1h | **Deps:** T3.5
- **Test cases:**
  - `T3.6.1` — "1,234,567" $\rightarrow$ 1234567.
  - `T3.6.2` — "2.5M" $\rightarrow$ 2500000.
  - `T3.6.3` — "1O5" $\rightarrow$ 105.
- **Acceptance:** Số liệu tài nguyên chuẩn xác tuyệt đối.

### **T3.7 — State Classifier**
- **Mô tả:** Phân loại màn hình CoC: `VILLAGE`, `CLOUDS`, `BATTLE`, `POPUP`, `BUILDER_BASE`, `UNKNOWN` theo độ ưu tiên rõ ràng.
- **Files:** `src/MyBot.Game.CoC/StateClassifier.cs`
- **Effort:** 4h | **Deps:** T3.1, T3.3
- **Test cases:**
  - `T3.7.1` — Nhận diện chính xác màn hình chính làng (VILLAGE) confidence ≥ 0.9.
  - `T3.7.2` — Nhận diện màn hình tìm trận (CLOUDS) và trận đánh (BATTLE).
  - `T3.7.3` — Màn hình có Popup ưu tiên trả về `POPUP` trước.
  - `T3.7.4` — Đạt accuracy ≥ 95% trên bộ golden dataset.
- **Acceptance:** Bộ não nhận diện trạng thái game cực kỳ chuẩn xác.

### **T3.8 — Popup Detector & Resolution Matrix**
- **Mô tả:** Tự động phát hiện các popup phổ biến (Connection Lost, Maintenance, Offer, Personal Break) và toạ độ nút xử lý tương ứng.
- **Files:** `src/MyBot.Game.CoC/Popups/PopupDetector.cs`
- **Effort:** 3h | **Deps:** T3.7
- **Test cases:**
  - `T3.8.1` — Popup mất kết nối $\rightarrow$ đề xuất nút Reload.
  - `T3.8.2` — Popup bảo trì $\rightarrow$ đọc OCR số phút bảo trì, chuyển trạng thái ngủ.
- **Acceptance:** Tự động đóng/giải quyết 90% popup trong game.

### **T3.9 — Freeze Detector (Perceptual Hash)**
- **Mô tả:** Tính aHash 16×16 từng frame, so sánh khoảng cách Hamming. Nếu khoảng cách < 3 liên tục trong 15s $\rightarrow$ phát hiện game bị treo (Freeze).
- **Files:** `src/MyBot.Game.CoC/FreezeDetector.cs`
- **Effort:** 2h | **Deps:** T0.2
- **Test cases:**
  - `T3.9.1` — Màn hình đứng yên hoàn toàn 15s $\rightarrow$ `IsFrozen() == true`.
  - `T3.9.2` — Màn hình có ngọn đuốc đung đưa nhẹ $\rightarrow$ `IsFrozen() == false` (không báo giả).
- **Acceptance:** Phát hiện đứng game chuẩn xác không gây false-positive.

### **T3.10 — Debug Visual Overlay Renderer**
- **Mô tả:** Vẽ bounding box nhận diện, dropline đỏ, tâm click lên Mat để hiển thị trên cửa sổ Debug.
- **Files:** `src/MyBot.Core.CV/DebugOverlay.cs`
- **Effort:** 2h | **Deps:** T3.1, T3.7
- **Test cases:**
  - `T3.10.1` — Vẽ đúng màu sắc và vị trí các đối tượng đã detect.
  - `T3.10.2` — Không làm rò rỉ bộ nhớ Mat khi render liên tục.
- **Acceptance:** Cung cấp hình ảnh trực quan cho dev debug.

---

## 🧮 PHASE 4 — GAME MATH ENGINE (Sprint 5, Week 7)

### **T4.1 — Isometric Conversion (u,v) ↔ (x,y)**
- **Mô tả:** Lưới Isometric 44×44 tiles. Chuyển đổi qua lại giữa toạ độ lưới và toạ độ màn hình theo công thức chuẩn của MyBot.
- **Files:** `src/MyBot.Game.CoC.Math/IsometricGrid.cs`
- **Effort:** 2h | **Deps:** Không
- **Test cases:**
  - `T4.1.1` — Grid(0,0) $\rightarrow$ Screen(center, top).
  - `T4.1.2` — Round-trip Grid $\rightarrow$ Screen $\rightarrow$ Grid sai lệch $\leq 1$ pixel.
- **Acceptance:** Công thức toán học chuẩn xác 100%.

### **T4.2 — Diamond Containment Check**
- **Mô tả:** Kiểm tra điểm có nằm trong hình thoi bản đồ: $\frac{|x - X_c|}{W_{half}} + \frac{|y - Y_c|}{H_{half}} \leq 1.0$.
- **Files:** `src/MyBot.Game.CoC.Math/Diamond.cs`
- **Effort:** 1h | **Deps:** T4.1
- **Test cases:**
  - `T4.2.1` — Điểm tâm và 4 đỉnh hình thoi trả về true.
  - `T4.2.2` — Điểm góc ngoài hình chữ nhật trả về false.
- **Acceptance:** Phục vụ xác định giới hạn thả quân.

### **T4.3 — Red Area Scanner (Native + Managed Fallback)**
- **Mô tả:** Quét pixel biên đỏ cấm thả quân (R>150, G<60, B<60, |G-B|<40) chia làm 4 góc phần tư bản đồ. Hỗ trợ fallback OpenCV `InRange`.
- **Files:** `src/MyBot.Game.CoC.Math/RedAreaScanner.cs`
- **Effort:** 4h | **Deps:** T4.2
- **Test cases:**
  - `T4.3.1` — Quét ra 4 danh sách điểm viền đỏ (TL, TR, BL, BR) trên ảnh mẫu trận đấu.
  - `T4.3.2` — Benchmark: thời gian quét < 100ms (native) và < 350ms (managed).
- **Acceptance:** Nhận diện chính xác ranh giới đỏ thả lính.

### **T4.4 — Red Area Cleaner (Outlier Removal)**
- **Mô tả:** Loại bỏ các điểm nhiễu có khoảng cách Euclidean tới điểm liền kề > 25px để làm mượt đường viền.
- **Files:** `src/MyBot.Game.CoC.Math/RedAreaCleaner.cs`
- **Effort:** 2h | **Deps:** T4.3
- **Test cases:**
  - `T4.4.1` — Loại bỏ sạch các điểm đơn độc do hiệu ứng đạn/lửa đỏ gây ra.
  - `T4.4.2` — Danh sách rỗng hoặc danh sách chuẩn không bị xoá nhầm điểm.
- **Acceptance:** Đường viền thả quân không bị gai nhọn.

### **T4.5 — Dropline Interpolation (Spline/Linear)**
- **Mô tả:** Nội suy chuỗi điểm viền thành đường spline mượt mà, phân bố đều N điểm thả quân.
- **Files:** `src/MyBot.Game.CoC.Math/DroplineInterpolator.cs`
- **Effort:** 2h | **Deps:** T4.4
- **Test cases:**
  - `T4.5.1` — Đầu vào 10 điểm, nội suy ra 100 điểm đều nhau trên quỹ đạo.
  - `T4.5.2` — Điểm đầu và cuối trùng khớp với ranh giới hợp lệ.
- **Acceptance:** Tạo đường ray thả quân mượt mà cho kịch bản CSV.

### **T4.6 — Dead Base Clustering (Euclidean)**
- **Mô tả:** Gom cụm các toạ độ template match mỏ tài nguyên trong phạm vi $\leq 20px$ để đếm chính xác số lượng mỏ độc lập.
- **Files:** `src/MyBot.Game.CoC.Math/EuclideanClusterer.cs`
- **Effort:** 1h | **Deps:** T0.2
- **Test cases:**
  - `T4.6.1` — 3 điểm match sát nhau (cách 5px) gom thành đúng 1 cluster.
  - `T4.6.2` — Các mỏ cách xa nhau giữ nguyên danh sách riêng.
- **Acceptance:** Không đếm trùng mỏ tài nguyên.

### **T4.7 — Storage Status Detector**
- **Mô tả:** Kiểm tra kho chứa tài nguyên (đặc biệt bám sát hàm `hasElixirStorage()` gốc từ AutoIt) để phân biệt nhà hoang và nhà sống.
- **Files:** `src/MyBot.Game.CoC.Math/StorageDetector.cs`
- **Effort:** 2h | **Deps:** T3.1
- **Test cases:**
  - `T4.7.1` — Nhà kho đầy nước $\rightarrow$ `HasFullStorage() == true`.
  - `T4.7.2` — Nhà kho cạn nước $\rightarrow$ `HasFullStorage() == false`.
- **Acceptance:** Đúng 95% trên 100 ảnh mẫu golden test.

### **T4.8 — Base Classifier**
- **Mô tả:** Kết hợp T4.6 + T4.7 + cấp TownHall để phân loại: `DeadBase`, `LiveBase`, `THOutside`, `ProgressBase`.
- **Files:** `src/MyBot.Game.CoC.Math/BaseClassifier.cs`
- **Effort:** 3h | **Deps:** T4.6, T4.7
- **Test cases:**
  - `T4.8.1` — Mỏ đầy + Kho cạn $\rightarrow$ phân loại chuẩn xác `DeadBase`.
  - `T4.8.2` — Đạt accuracy ≥ 92% trên toàn bộ tập ảnh kiểm thử.
- **Acceptance:** Ra quyết định tấn công hay bỏ qua cực kỳ chuẩn xác.

---

## 🏘️ PHASE 5 — VILLAGE MANAGEMENT, CLAN & BUILDER BASE (Sprint 6, Week 8)

### **T5.1 — Collector Harvester**
- **Mô tả:** Quét icon bong bóng vàng/elixir/dark elixir đầy trên mỏ và tự động click thu hoạch tuần tự.
- **Effort:** 3h | **Deps:** T3.1, T2.2
- **Test cases:** `T5.1.1` Có 4 mỏ đầy $\rightarrow$ click 4 lần thu hoạch. `T5.1.2` Không có mỏ đầy $\rightarrow$ không click.
- **Acceptance:** Thu hoạch tài nguyên trong ≤ 10s.

### **T5.2 — Loot Cart Collector**
- **Mô tả:** Nhận diện và thu gom Xe tài nguyên (Loot Cart) xuất hiện sau khi làng bị đánh.
- **Effort:** 1h | **Deps:** T5.1
- **Test cases:** `T5.2.1` Có xe $\rightarrow$ click thu. `T5.2.2` Không có xe $\rightarrow$ bỏ qua an toàn.
- **Acceptance:** Thu dọn xe tài nguyên tự động.

### **T5.3 — Obstacle Cleaner**
- **Mô tả:** Dọn dẹp 9 loại chướng ngại vật (cây, đá, bụi cây) khi còn thợ rảnh và đủ tài nguyên.
- **Effort:** 3h | **Deps:** T3.1, T0.5
- **Test cases:** `T5.3.1` Có cây, thợ rảnh $\rightarrow$ click dọn. `T5.3.2` Hết thợ rảnh $\rightarrow$ bỏ qua.
- **Acceptance:** Dọn rác kiếm ngọc tối ưu.

### **T5.4 — Trap Rearm**
- **Mô tả:** Tự động kích hoạt lại bẫy và nạp đạn cho X-Bow, Tháp Inferno khi bị cạn.
- **Effort:** 2h | **Deps:** T3.1
- **Test cases:** `T5.4.1` Bẫy cạn $\rightarrow$ nạp đạn. `T5.4.2` Đã đầy $\rightarrow$ không click.
- **Acceptance:** Đảm bảo hệ thống phòng thủ luôn sẵn sàng.

### **T5.5 — Greedy Wall Upgrade**
- **Mô tả:** Tự động nâng cấp tường thành theo thứ tự level tăng dần khi lượng vàng/elixir vượt ngưỡng dự trữ (Min Reserve).
- **Effort:** 3h | **Deps:** T3.1, T0.5
- **Test cases:** `T5.5.1` Tài nguyên dư $\rightarrow$ nâng cấp tường. `T5.5.2` Dưới ngưỡng dự trữ $\rightarrow$ không nâng cấp.
- **Acceptance:** Xả tài nguyên thừa vào tường hiệu quả.

### **T5.6 — Quick Train Army**
- **Mô tả:** Mở nhà lính, chọn preset Quick Train (1/2/3). Đọc OCR thời gian luyện lính còn lại để căn giờ tấn công.
- **Effort:** 3h | **Deps:** T2.2, T3.5
- **Test cases:** `T5.6.1` Đọc chính xác thời gian luyện quân "12m 30s". `T5.6.2` Quân đầy $\rightarrow$ sẵn sàng tấn công.
- **Acceptance:** Duy trì quân đội luôn sẵn sàng 24/7.

### **T5.7 — Clan Donation**
- **Mô tả:** Quét khung chat clan, nhận diện yêu cầu xin quân ("drag", "loon", "valk"), đối chiếu danh sách Whitelist/Blacklist để cho quân chuẩn xác.
- **Effort:** 4h | **Deps:** T3.5, T0.5
- **Test cases:** `T5.7.1` Yêu cầu "any dragon" $\rightarrow$ donate đúng rồng. `T5.7.2` "no goblin" $\rightarrow$ không cho goblin.
- **Acceptance:** Tự động xin và cho quân chuẩn mực.

### **T5.8 — Clan Request**
- **Mô tả:** Gửi yêu cầu xin quân Clan Castle định kỳ theo thời gian hồi (cooldown).
- **Effort:** 2h | **Deps:** T2.2
- **Test cases:** `T5.8.1` Nút Request sẵn sàng $\rightarrow$ gửi request. `T5.8.2` Đang cooldown $\rightarrow$ bỏ qua.
- **Acceptance:** Luôn đầy lính clan thủ/công.

### **T5.9 — Laboratory & Pet House Manager**
- **Mô tả:** Quản lý nâng cấp quân trong Lab và nâng cấp Pet trong Pet House theo danh sách ưu tiên.
- **Effort:** 3h | **Deps:** T3.7, T0.5
- **Test cases:** `T5.9.1` Lab rảnh $\rightarrow$ nâng cấp theo danh sách ưu tiên. `T5.9.2` Đang nghiên cứu $\rightarrow$ bỏ qua.
- **Acceptance:** Tối ưu hóa thời gian nghiên cứu.

### **T5.10 — Blacksmith Manager (Thợ Rèn)**
- **Mô tả:** Tự động kiểm tra quặng và nâng cấp Hero Equipment theo cấu hình.
- **Effort:** 2h | **Deps:** T5.9
- **Test cases:** `T5.10.1` Đủ quặng $\rightarrow$ nâng cấp trang bị ưu tiên.
- **Acceptance:** Quản lý tính năng mới thợ rèn CoC.

### **T5.11 — Builder Base 2-Phase Attack**
- **Mô tả:** Tự động sang Làng Đêm, tìm trận, triển khai quân Phase 1, tự động chuyển Phase 2 và spam chiêu Battle Machine.
- **Effort:** 4h | **Deps:** T6.5
- **Test cases:** `T5.11.1` Hết phase 1 $\rightarrow$ sang phase 2. `T5.11.2` Bật chiêu Battle Machine định kỳ.
- **Acceptance:** Tự động farm làng đêm hoàn chỉnh.

### **T5.12 — Builder Base Collector & Clock Tower** *(Bổ sung mới)*
- **Mô tả:** Thu hoạch Mỏ Ngọc (Gem Mine), Mỏ Vàng/Elixir Làng Đêm và tự kích hoạt Tăng tốc Tháp Đồng Hồ (Clock Tower Boost) khi sẵn sàng.
- **Effort:** 3h | **Deps:** T5.1
- **Test cases:** `T5.12.1` Tháp đồng hồ sẵn sàng $\rightarrow$ kích hoạt boost. `T5.12.2` Thu hoạch sạch mỏ ngọc làng đêm.
- **Acceptance:** Tối ưu tài nguyên Làng Đêm.

### **T5.13 — Builder Base Building Upgrade** *(Bổ sung mới)*
- **Mô tả:** Tự động nâng cấp công trình Làng Đêm theo danh sách ưu tiên cấu hình.
- **Effort:** 2h | **Deps:** T5.12
- **Test cases:** `T5.13.1` Có thợ rảnh làng đêm + đủ tài nguyên $\rightarrow$ nâng cấp công trình ưu tiên.
- **Acceptance:** Nâng cấp Làng Đêm tự động.

### **T5.14 — Builder Base Wall Upgrade** *(Bổ sung mới)*
- **Mô tả:** Tự động nâng cấp tường Làng Đêm bằng Builder Gold / Elixir khi vượt ngưỡng dự trữ.
- **Effort:** 2h | **Deps:** T5.5
- **Test cases:** `T5.14.1` Nâng cấp tường Làng Đêm khi tài nguyên dư thừa.
- **Acceptance:** Tránh tràn kho Làng Đêm.

### **T5.15 — Clan Games Task Scanner** *(Bổ sung mới)*
- **Mô tả:** Tự động mở xe Caravan Clan Games, dùng OCR quét danh sách 5-10 thử thách hiện có, lọc ra các nhiệm vụ bot làm được (phá huỷ X công trình, cướp Y tài nguyên).
- **Files:** `src/MyBot.Game.CoC/Village/ClanGamesScanner.cs`
- **Effort:** 4h | **Deps:** T3.5, T3.7
- **Test cases:**
  - `T5.15.1` — Nhận diện đúng danh sách thử thách và số điểm thưởng tương ứng.
  - `T5.15.2` — Phân loại chính xác nhiệm vụ auto được và nhiệm vụ cần bỏ qua.
- **Acceptance:** Tự động chọn nhiệm vụ Clan Games tối ưu.

### **T5.16 — Clan Games Auto-Complete & Claim** *(Bổ sung mới)*
- **Mô tả:** Nhận thử thách đã chọn, theo dõi tiến độ sau các trận đánh và tự động nhận thưởng khi hoàn thành nhiệm vụ.
- **Files:** `src/MyBot.Game.CoC/Village/ClanGamesExecutor.cs`
- **Effort:** 3h | **Deps:** T5.15
- **Test cases:**
  - `T5.16.1` — Chọn nhiệm vụ thành công $\rightarrow$ lưu trạng thái theo dõi.
  - `T5.16.2` — Hoàn thành thử thách $\rightarrow$ ấn Claim lấy điểm clan.
- **Acceptance:** Tự động cày full điểm Clan Games cho tài khoản.

### **T5.17 — Clan Capital / Raid Weekend Auto-Attack** *(Bổ sung mới)*
- **Mô tả:** Tự động phát hiện sự kiện Cuối tuần Đột kích (Raid Weekend), đánh đủ 5-6 lượt theo chiến thuật spam quân ven biên ranh giới để tối đa hoá Huy chương Đột kích (Capital Gold & Raid Medals).
- **Files:** `src/MyBot.Game.CoC/Village/ClanCapitalRaid.cs`
- **Effort:** 4h | **Deps:** T6.5
- **Test cases:**
  - `T5.17.1` — Phát hiện có lượt đánh Capital $\rightarrow$ vào trận xả hết quân theo kịch bản.
  - `T5.17.2` — Đánh đủ số lượt quy định $\rightarrow$ thoát ra về làng chính.
- **Acceptance:** Không bỏ lỡ huy chương đột kích mỗi cuối tuần.

---

## ⚔️ PHASE 6 — COMBAT ENGINE, PETS & EQUIPMENT (Sprint 7-8, Week 9-10)

### **T6.1 — CSV Lexer (Hỗ trợ ký tự phân cách Pipe `|`)**
- **Mô tả:** Đọc file script tấn công `.csv` với ký tự phân cách bắt buộc là `|`, loại bỏ comment `//`, `;`, xử lý khoảng trắng và chuẩn hoá UTF-8 BOM.
- **Files:** `src/MyBot.Game.CoC.Combat/CsvLexer.cs`
- **Effort:** 2h | **Deps:** T0.2
- **Test cases:**
  - `T6.1.1` — Phân tách chính xác dòng `DROP|A|1-10|10|Barb|50|100|200` thành 8 token.
  - `T6.1.2` — Xử lý đúng dòng chứa khoảng trống rỗng `DROP||1`.
  - `T6.1.3` — Tương thích 100% các file CSV kịch bản của MyBot gốc.
- **Acceptance:** Lexer chuẩn xác cho kịch bản tấn công.

### **T6.2 — CSV Command Parser**
- **Mô tả:** Chuyển đổi token thành đối tượng command: `DropCommand`, `MakeCommand`, `WaitCommand`, `HeroCommand`, `SpellCommand`, `SideCommand`, `SideBCommand`.
- **Files:** `src/MyBot.Game.CoC.Combat/CsvParser.cs`
- **Effort:** 4h | **Deps:** T6.1
- **Test cases:**
  - `T6.2.1` — Parse đúng dải chỉ số index: đơn lẻ (`1`), dải (`1-10`), danh sách (`1,3,5`).
  - `T6.2.2` — Lệnh sai cú pháp $\rightarrow$ throw `UnknownCommandException` báo rõ số dòng.
- **Acceptance:** Parse thành công 100% kịch bản Barch, GiBarch, Goblin chuẩn.

### **T6.3 — Vector Resolver**
- **Mô tả:** Chuyển đổi định danh `VectorID` thành danh sách toạ độ $(x, y)$ dựa vào dropline đỏ và lệnh `MAKE` trước đó.
- **Files:** `src/MyBot.Game.CoC.Combat/VectorResolver.cs`
- **Effort:** 3h | **Deps:** T6.2, T4.5
- **Test cases:**
  - `T6.3.1` — `VECTOR_A` giải mã ra đủ số điểm theo yêu cầu của lệnh MAKE.
  - `T6.3.2` — `VECTOR_A-B` xen kẽ điểm thả quân chuẩn xác.
- **Acceptance:** Tính toán toạ độ thả quân không bị trùng lặp.

### **T6.4 — Troop Slot Resolution**
- **Mô tả:** Quét thanh chọn quân ở đáy màn hình để tìm slot chứa loại lính cần thả (`Barb`, `Archer`, `AnyS` - Siege Engine).
- **Files:** `src/MyBot.Game.CoC.Combat/TroopSlotResolver.cs`
- **Effort:** 3h | **Deps:** T3.1
- **Test cases:**
  - `T6.4.1` — Tìm đúng slot chứa Barbarian.
  - `T6.4.2` — Lính hết hoặc không có $\rightarrow$ throw `TroopNotFoundException` kiểm soát được.
- **Acceptance:** Chọn đúng loại lính cần thả trong trận.

### **T6.5 — Drop Executor**
- **Mô tả:** Thực thi thả quân: click chọn slot lính $\rightarrow$ click vào các toạ độ trên vector thả kèm độ trễ `DelayDrop`, `DelayPoints` và jitter ngẫu nhiên.
- **Files:** `src/MyBot.Game.CoC.Combat/DropExecutor.cs`
- **Effort:** 4h | **Deps:** T2.2, T6.3, T6.4
- **Test cases:**
  - `T6.5.1` — Thả 10 lính trên vector 10 điểm $\rightarrow$ ghi nhận đủ 10 tap slot và 10 tap trận địa.
  - `T6.5.2` — Độ trễ giữa các lượt thả tuân thủ cấu hình kịch bản.
- **Acceptance:** Thả quân nhịp nhàng, tự nhiên như người đánh.

### **T6.6 — Hero Ability Trigger (Default)**
- **Mô tả:** Giám sát thanh máu của 4 Hero. Khi HP xuống dưới ngưỡng (King 30%, Queen 25%, Champion 35%) $\rightarrow$ bấm kích hoạt kỹ năng đặc biệt.
- **Files:** `src/MyBot.Game.CoC.Combat/HeroAbilityTrigger.cs`
- **Effort:** 3h | **Deps:** T3.1
- **Test cases:**
  - `T6.6.1` — Máu Queen < 25% $\rightarrow$ click kích hoạt tàng hình.
  - `T6.6.2` — Hero đã chết hoặc kỹ năng đang hồi $\rightarrow$ không click lại.
- **Acceptance:** Kích hoạt chiêu tướng đúng thời điểm cứu mạng.

### **T6.7 — Hero Equipment Ability Trigger (Runtime)** *(Bổ sung mới)*
- **Mô tả:** Xử lý cơ chế kích hoạt trang bị Hero (Equipment) trong trận: tự động bấm kích hoạt theo ngưỡng riêng của trang bị (VD: Găng tay khổng lồ Giant Gauntlet, Bình tàng hình Invisibility Vial) hoặc kích hoạt ngay khi hero vào lõi căn cứ.
- **Files:** `src/MyBot.Game.CoC.Combat/EquipmentTrigger.cs`
- **Effort:** 3h | **Deps:** T6.6
- **Test cases:**
  - `T6.7.1` — Trang bị Giant Gauntlet kích hoạt đúng điều kiện cấu hình.
  - `T6.7.2` — Không xung đột với kỹ năng mặc định của tướng.
- **Acceptance:** Hỗ trợ đầy đủ meta trang bị Hero CoC 2024.

### **T6.8 — Pet Deployment Executor (Runtime)** *(Bổ sung mới)*
- **Mô tả:** Xử lý pet đi kèm Hero trong trận: theo dõi trạng thái sống sót của Pet (L.A.S.S.I, Electro Owl, Unicorn, Phoenix), kích hoạt hồi sinh Phoenix khi Hero ngã xuống.
- **Files:** `src/MyBot.Game.CoC.Combat/PetCombatExecutor.cs`
- **Effort:** 3h | **Deps:** T6.5
- **Test cases:**
  - `T6.8.1` — Thả Hero đi kèm Pet tương ứng được cấu hình.
  - `T6.8.2` — Hero chết kích hoạt trạng thái bất tử tạm thời của Phoenix.
- **Acceptance:** Quản lý trọn vẹn Pet trong chiến đấu.

### **T6.9 — Siege Machine Controller**
- **Mô tả:** Quản lý Xe công thành (Battle Blimp, Log Launcher, Flame Flinger). Tự động kích hoạt mở Blimp sớm khi bay tới sát Nhà Chính (TH) và thả spell hỗ trợ.
- **Files:** `src/MyBot.Game.CoC.Combat/SiegeController.cs`
- **Effort:** 3h | **Deps:** T3.1, T4.2
- **Test cases:**
  - `T6.9.1` — Blimp cách TH < 50px $\rightarrow$ click pop xe xả quân clan.
- **Acceptance:** Đánh Siege Blimp chuẩn xác.

### **T6.10 — Spell Executor**
- **Mô tả:** Thả bình phép (Rage, Heal, Freeze, Jump, Lightning) vào đúng toạ độ mục tiêu theo thuật toán thông minh.
- **Files:** `src/MyBot.Game.CoC.Combat/SpellExecutor.cs`
- **Effort:** 4h | **Deps:** T6.5
- **Test cases:**
  - `T6.10.1` — Freeze thả trúng tháp Inferno hoặc Scattershot.
  - `T6.10.2` — Rage thả bao phủ khu vực quân đang tiến công.
- **Acceptance:** Thả phép chính xác tối ưu sát thương.

### **T6.11 — Matchmaking Loop & Loot Filter**
- **Mô tả:** Vòng lặp tìm trận: Bấm Attack $\rightarrow$ Chờ mây $\rightarrow$ OCR tài nguyên $\rightarrow$ So khớp điều kiện lọc (Min Gold/Elixir/DE/Cúp) $\rightarrow$ Quyết định bấm Next hay Attack.
- **Files:** `src/MyBot.Game.CoC.Combat/MatchmakingLoop.cs`, `LootFilter.cs`
- **Effort:** 4h | **Deps:** T3.7, T3.5, T4.8
- **Test cases:**
  - `T6.11.1` — Tài nguyên đạt yêu cầu $\rightarrow$ dừng tìm kiếm, chuyển trạng thái BATTLE.
  - `T6.11.2` — Tài nguyên không đủ $\rightarrow$ bấm Next, tăng biến đếm số lượt tìm.
  - `T6.11.3` — Kẹt mây quá 60s $\rightarrow$ huỷ tìm kiếm về làng.
- **Acceptance:** Tìm được nhà ngon trong thời gian ngắn nhất.

### **T6.12 — Smart Early Surrender & Battle Monitor**
- **Mô tả:** Giám sát toàn bộ trận đánh (thời gian, % phá huỷ, tài nguyên đã cướp). Chủ động đầu hàng sớm khi: tài nguyên còn lại < 15k, hoặc quân đã chết hết, hoặc đã đạt 50% 1 sao và cướp > 95% loot.
- **Files:** `src/MyBot.Game.CoC.Combat/SurrenderCriteria.cs`, `BattleMonitor.cs`
- **Effort:** 3h | **Deps:** T6.6, T3.5
- **Test cases:**
  - `T6.12.1` — Đã cướp hết tài nguyên $\rightarrow$ bấm kết thúc trận ngay lập tức.
  - `T6.12.2` — Tiết kiệm ≥ 30% thời gian chờ đợi mỗi trận đánh.
- **Acceptance:** Tối đa hoá tốc độ cày tài nguyên mỗi giờ.

---

## 🎨 PHASE 7 — UI LAYER (WPF MVVM) (Sprint 8-10, Week 11-13)

### **T7.1 — Main Window Shell**
- **Mô tả:** `MainWindow.xaml` kích thước cố định **475 × 650**, không cho resize chuột, Custom Chrome hiện đại, Content Area 466×425, Bottom Panel 115px.
- **Effort:** 3h | **Deps:** T0.3
- **Test cases:** `T7.1.1` Mở app đúng 475×650px. `T7.1.2` Kéo title bar di chuyển cửa sổ mượt mà.
- **Acceptance:** Khung giao diện chuẩn mực, không flicker.

### **T7.2 — Custom Title Bar**
- **Mô tả:** 5 nút bấm điều khiển: Dock `◄`/`►`, Mini Mode `▄`/`█`, Minimize `_`, Close `×`.
- **Effort:** 2h | **Deps:** T7.1
- **Test cases:** `T7.2.1` Click `▄` chuyển chế độ thu nhỏ Mini Mode. `T7.2.2` Click `×` hiển thị xác nhận nếu bot đang chạy.
- **Acceptance:** Title bar tiện dụng, thẩm mỹ cao.

### **T7.3 — Banner Logo & URL**
- **Mô tả:** Ảnh logo thương hiệu 475×67 và thanh banner URL 475×13 mở trình duyệt khi click.
- **Effort:** 1h | **Deps:** T7.1
- **Test cases:** `T7.3.1` Logo hiển thị sắc nét. `T7.3.2` Click URL mở đúng trình duyệt web mặc định.
- **Acceptance:** Nhận diện thương hiệu chuẩn MyBot.

### **T7.4 — Bottom Panel: Control Buttons**
- **Mô tả:** Hệ thống nút: Start (xanh), Stop (đỏ), Pause/Resume (cam), Search Mode, Photo, Hide, Dock, Background Mode checkbox. Bind vào RelayCommands.
- **Effort:** 3h | **Deps:** T7.1, T0.9
- **Test cases:** `T7.4.1` Click Start khi IDLE $\rightarrow$ kích hoạt Command. `T7.4.2` Phím ESC $\rightarrow$ Stop khẩn cấp.
- **Acceptance:** Phím bấm mượt, bắt phím tắt toàn cục.

### **T7.5 — Bottom Panel: Hero Shields (6 Anh Hùng)**
- **Mô tả:** 6 icon khiên tướng (King, Queen, Grand Warden, Royal Champion, Minion Prince, Battle Machine). Hiển thị 4 màu trạng thái: Xám (Chưa có), Xanh lá (Sẵn sàng), Xanh dương (Đang hồi máu kèm tooltip đếm ngược), Đỏ (Đang nâng cấp).
- **Effort:** 2h | **Deps:** T7.4
- **Test cases:** `T7.5.1` Tướng hồi máu hiển thị màu xanh dương kèm tooltip thời gian. `T7.5.2` Tướng sẵn sàng đổi màu xanh lá.
- **Acceptance:** Trực quan hoá trạng thái Hero real-time.

### **T7.6 — Bottom Panel: Lab & Pet Timers**
- **Mô tả:** 2 dòng hiển thị icon + timer đếm ngược của Phòng Thí Nghiệm (Lab) và Nhà Pet (Pet House).
- **Effort:** 1h | **Deps:** T7.4
- **Test cases:** `T7.6.1` Đang nghiên cứu $\rightarrow$ đếm ngược thời gian. `T7.6.2` Rảnh $\rightarrow$ hiển thị chữ "Finished".
- **Acceptance:** Nắm bắt tiến độ nâng cấp ngay tại màn hình chính.

### **T7.7 — Bottom Panel: Resource Ticker**
- **Mô tả:** Ticker tài nguyên 2 trang (Trang 1: Vàng, Elixir, Dark Elixir, Cúp, Thợ, Ngọc, Runtime, Tốc độ cày/h; Trang 2: Tài nguyên Làng Đêm & Capital). Nút ◄/► để chuyển trang.
- **Effort:** 3h | **Deps:** T7.4, T0.5
- **Test cases:** `T7.7.1` Định dạng số có dấu phẩy ngăn cách (1,234,567). `T7.7.2` Chuyển trang mượt mà.
- **Acceptance:** Cập nhật thông số tài nguyên mỗi 5s.

### **T7.8 — Bottom Panel: Status Bar**
- **Mô tả:** Hiển thị trạng thái bot hiện tại: "Status: Idle", "Status: Searching (Raid #14)", "Status: Paused".
- **Effort:** 1h | **Deps:** T7.4, T0.9
- **Test cases:** `T7.8.1` Cập nhật tức thì theo event `StateChanged` của State Machine.
- **Acceptance:** Luôn thông báo rõ bot đang làm gì.

### **T7.9 — Log Tab**
- **Mô tả:** RichTextBox hiển thị log màu theo severity (Success=Xanh lá, Error=Đỏ, Info=Xanh dương, Warning=Cam, Debug=Tím). Có nút AutoScroll, Clear, Copy.
- **Effort:** 2h | **Deps:** T0.4, T7.1
- **Test cases:** `T7.9.1` Tải 1000 dòng log không bị giật lag giao diện. `T7.9.2` Nút Copy sao chép đúng nội dung vào clipboard.
- **Acceptance:** Nhật ký hoạt động trực quan, mượt mà.

### **T7.10 — Village Tab: Misc Subtab**
- **Mô tả:** Cấu hình: Dọn rác, giữ thợ, thu hoạch mỏ, nhặt xe tài nguyên, nạp đạn bẫy, cắn bình thuốc.
- **Effort:** 2h | **Deps:** T7.9
- **Test cases:** `T7.10.1` Checkbox lưu giá trị bền vững vào `config.json`.
- **Acceptance:** Lưu cấu hình chuẩn xác.

### **T7.11 — Village Tab: Clan Castle Subtab**
- **Mô tả:** Cấu hình xin quân và cho quân (Grid 40+ icon lính, textbox Whitelist/Blacklist).
- **Effort:** 3h | **Deps:** T7.10
- **Test cases:** `T7.11.1` Chọn loại lính cho phép donate $\rightarrow$ lưu chuẩn.
- **Acceptance:** Quản lý quy tắc clan thuận tiện.

### **T7.12 — Village Tab: Upgrade Subtab**
- **Mô tả:** Cấu hình nâng cấp tường (loại tài nguyên, mức dự trữ tối thiểu, khoảng level) và ưu tiên nâng công trình.
- **Effort:** 3h | **Deps:** T7.10
- **Test cases:** `T7.12.1` Validation khoảng level tường hợp lệ (min $\leq$ max).
- **Acceptance:** Tránh lỗi cấu hình sai.

### **T7.13 — Village Tab: Notify Subtab**
- **Mô tả:** Nhập Telegram Token/Chat ID và Discord Webhook URL. Có nút Test gửi thông báo thử nghiệm.
- **Effort:** 2h | **Deps:** T7.10
- **Test cases:** `T7.13.1` Bấm Test gửi tin nhắn thành công qua webhook.
- **Acceptance:** Kết nối thông báo ra bên ngoài tin cậy.

### **T7.14 — Attack Tab: Train Army Subtab (với Equipment/Pets/Siege)**
- **Mô tả:** Chọn preset quân, chọn Hero bắt buộc, **Grid cấu hình Hero Equipment (icon 24×24 + huy hiệu level + checkbox)**, gán Pet và độ ưu tiên Siege.
- **Effort:** 4h | **Deps:** T7.1
- **Test cases:** `T7.14.1` Mỗi Hero chỉ được chọn tối đa 2 trang bị Equipment. `T7.14.2` Trang bị đang dùng hiển thị viền vàng nổi bật.
- **Acceptance:** Bố cục gọn gàng, đầy đủ tính năng CoC mới.

### **T7.15 — Attack Tab: Search & Attack (Deadbase)**
- **Mô tả:** 3 tab con Search/Attack/Collectors: cấu hình Min Gold, Elixir, Dark Elixir, Cúp, cấp TH, số mỏ tối thiểu.
- **Effort:** 3h | **Deps:** T7.14
- **Test cases:** `T7.15.1` Nhập ngưỡng tài nguyên lưu chuẩn vào bộ lọc tìm kiếm.
- **Acceptance:** Thiết lập tìm nhà hoang linh hoạt.

### **T7.16 — Attack Tab: Search & Attack (Activebase)**
- **Mô tả:** Bộ lọc né công trình phòng thủ mạnh: Eagle Artillery, Inferno, X-Bow, Scattershot, Monolith.
- **Effort:** 2h | **Deps:** T7.15
- **Test cases:** `T7.16.1` Cấu hình cấp phòng thủ tối đa cho phép đánh.
- **Acceptance:** Đảm bảo an toàn khi đánh nhà sống.

### **T7.17 — Attack Tab: Options Subtab**
- **Mô tả:** Chọn thuật toán Red Area, Smart Zap sét, tiêu chí dừng trận (hết loot, 1 sao, 2 sao, 50%, chết hết quân).
- **Effort:** 2h | **Deps:** T7.15
- **Test cases:** `T7.17.1` Lưu tiêu chí dừng trận chính xác.
- **Acceptance:** Tối ưu hiệu quả tấn công.

### **T7.18 — Attack Tab: Strategies Subtab + Drop Order Modal**
- **Mô tả:** Chọn file `.csv` kịch bản, nút "Edit Drop Order" mở modal 440×375 kéo thả thứ tự thả của 80+ loại quân/tướng/phép.
- **Effort:** 4h | **Deps:** T7.17
- **Test cases:** `T7.18.1` Kéo thả đổi vị trí lính $\rightarrow$ lưu lại thứ tự mới vào file strategy.
- **Acceptance:** Tuỳ biến kịch bản tấn công trực quan.

### **T7.19 — Bot Tab: Options Subtab**
- **Mô tả:** Đổi ngôn ngữ (load từ `languages/*.ini`), số ngày tự xoá log cũ, tự khởi động cùng Windows, tự căn chỉnh cửa sổ.
- **Effort:** 2h | **Deps:** T7.1
- **Test cases:** `T7.19.1` Đổi sang "Tiếng Việt" $\rightarrow$ toàn bộ text UI cập nhật ngay lập tức không cần khởi động lại.
- **Acceptance:** Đa ngôn ngữ mượt mà.

### **T7.20 — Bot Tab: Android Subtab**
- **Mô tả:** Chọn giả lập (LDPlayer/BlueStacks 5/MEmu/Nox), đường dẫn ADB, IP, Port, phương thức capture hình ảnh.
- **Effort:** 2h | **Deps:** T7.19
- **Test cases:** `T7.20.1` Chọn giả lập lưu đúng tham số vào config.
- **Acceptance:** Dễ dàng chuyển đổi giữa các giả lập.

### **T7.21 — Bot Tab: Debug Subtab**
- **Mô tả:** Bật log debug, lưu ảnh nhà zombie, các nút test nhanh: Test Capture, Test OCR, Test Red Area.
- **Effort:** 2h | **Deps:** T7.19
- **Test cases:** `T7.21.1` Click Test Capture chụp màn hình lưu file PNG và hiển thị thông báo.
- **Acceptance:** Hỗ trợ người dùng tự kiểm tra lỗi nhanh.

### **T7.22 — Bot Tab: Profiles Subtab**
- **Mô tả:** Quản lý Profile (Thêm, Đổi tên, Xoá), cấu hình chuyển đổi tài khoản (Supercell ID / SharedPrefs).
- **Effort:** 3h | **Deps:** T7.19
- **Test cases:** `T7.22.1` Thêm profile mới $\rightarrow$ sinh bộ cấu hình riêng độc lập.
- **Acceptance:** Cày nhiều tài khoản với các chiến thuật khác nhau.

### **T7.23 — Stats Tab**
- **Mô tả:** DataGrid lịch sử các trận đánh và biểu đồ tổng kết (tài nguyên thu được theo giờ, tỷ lệ thắng). Nút xuất báo cáo CSV.
- **Effort:** 3h | **Deps:** T7.1, T0.5
- **Test cases:** `T7.23.1` Tải danh sách trận đánh hiển thị chuẩn xác. `T7.23.2` Xuất file CSV mở tốt trên Excel.
- **Acceptance:** Thống kê kết quả cày cuốc chi tiết.

### **T7.24 — About Tab**
- **Mô tả:** Danh sách tác giả, phiên bản build, giấy phép GPL-3.0, link diễn đàn, nút Donate.
- **Effort:** 1h | **Deps:** T7.1
- **Test cases:** `T7.24.1` Hiển thị chuẩn phiên bản assembly của app.
- **Acceptance:** Thông tin minh bạch, đúng quy chuẩn OSS.

### **T7.25 — Splash Screen Window**
- **Mô tả:** Cửa sổ Splash 350×120 hiển thị 2s lúc mở app với progress bar 5 bước kiểm tra môi trường.
- **Effort:** 1h | **Deps:** T7.1
- **Test cases:** `T7.25.1` Tải xong tài nguyên tự động chuyển sang MainWindow mượt mà.
- **Acceptance:** Khởi động chuyên nghiệp.

### **T7.26 — System Tray Menu**
- **Mô tả:** Thu nhỏ app xuống khay hệ thống, menu chuột phải: Show, Start, Stop, Pause, Resume, Exit.
- **Effort:** 2h | **Deps:** T7.1
- **Test cases:** `T7.26.1` Click đúp icon khay $\rightarrow$ phục hồi cửa sổ chính.
- **Acceptance:** Chạy ẩn gọn gàng dưới nền.

### **T7.27 — I18N Engine**
- **Mô tả:** Đọc file `languages/*.ini`, hàm dịch `GetText(section, key, default)`. Refresh giao diện dynamic.
- **Effort:** 3h | **Deps:** T7.19
- **Test cases:** `T7.27.1` Chuyển đổi ngôn ngữ không sót key nào.
- **Acceptance:** Quốc tế hoá hoàn chỉnh.

### **T7.28 — UI State Matrix Wiring (Liên kết 10 State)** *(Không còn bị block nhờ T0.9/T0.10)*
- **Mô tả:** Khóa/mở các nút điều khiển bottom panel và 6 Tab theo đúng trạng thái `BotState` của State Machine thông qua DataTrigger XAML.
- **Effort:** 3h | **Deps:** T7.4, T0.9, T0.10
- **Test cases:**
  - `T7.28.1` — Trạng thái `IDLE` $\rightarrow$ nút Start sáng, nút Stop mờ.
  - `T7.28.2` — Trạng thái `SEARCHING` $\rightarrow$ khóa 6 Tab cấu hình, bật nút Pause.
  - `T7.28.3` — Trạng thái `ERROR` $\rightarrow$ mở khóa cho phép sửa lại cấu hình.
- **Acceptance:** Giao diện phản hồi chuẩn xác theo vòng đời bot.

### **T7.29 — Safe-Point Pause UI Feedback**
- **Mô tả:** Khi bấm Pause, hiển thị banner/dialog "Đang chờ điểm an toàn (tối đa 15s)". Nếu đang trong trận, hiển thị hộp thoại hỏi "Đầu hàng ngay hay chờ đánh xong?".
- **Effort:** 2h | **Deps:** T7.28, T6.12
- **Test cases:** `T7.29.1` Bấm Pause ở ngoài làng $\rightarrow$ dừng an toàn không mất quân.
- **Acceptance:** Trải nghiệm người dùng an tâm, minh bạch.

### **T7.30 — Dynamic Tab Registry + Lazy Loading**
- **Mô tả:** `TabRegistry` đăng ký động các tab. Chỉ nạp giao diện tab vào bộ nhớ khi người dùng click chọn lần đầu (Lazy Load).
- **Effort:** 3h | **Deps:** T7.9-T7.24
- **Test cases:** `T7.30.1` Khởi động app chỉ mất ≤ 1s và chiếm ít RAM ban đầu.
- **Acceptance:** Giao diện phản hồi nhanh, mở rộng tab mới dễ dàng.

### **T7.31 — Multi-Instance Dashboard Tab** *(Bổ sung mới)*
- **Mô tả:** Thêm tab "Instances" cho phép theo dõi trạng thái song song của 1 đến 8 tài khoản/giả lập bot đang chạy (Trạng thái, Tài nguyên cướp hôm nay, Số trận đánh, Lỗi gần nhất).
- **Files:** `src/MyBot.UI.Tabs.Instances/*.xaml`
- **Effort:** 4h | **Deps:** T7.30, T0.9
- **Test cases:**
  - `T7.31.1` — Hiển thị DataGrid đa instance cập nhật mỗi 3s.
  - `T7.31.2` — Bấm chọn instance chuyển nhanh cửa sổ quản lý tương ứng.
- **Acceptance:** Phục vụ người dùng cày nhiều acc chuyên nghiệp.

### **T7.32 — Config Backup, Export & Restore Tool** *(Bổ sung mới)*
- **Mô tả:** Chức năng sao lưu và phục hồi cấu hình ra file `.zip` (bao gồm `config.json`, profile, kịch bản CSV tuỳ chỉnh), có xác thực schema khi import.
- **Files:** `src/MyBot.Core.Config/ConfigBackupManager.cs`
- **Effort:** 2h | **Deps:** T0.5
- **Test cases:**
  - `T7.32.1` — Export ra file zip đầy đủ dữ liệu cấu hình.
  - `T7.32.2` — Import file zip bị lỗi cấu trúc $\rightarrow$ báo lỗi rõ ràng, không ghi đè hỏng cấu hình cũ.
- **Acceptance:** Người dùng dễ dàng chia sẻ profile cho nhau.

---

## 🔄 PHASE 8 — STATE WATCHDOG, RECOVERY & PERSISTENCE (Sprint 10, Week 14)

*(Lưu ý: T8.1 và T8.2 đã chuyển lên T0.9 và T0.10)*

### **T8.1 — State Watchdog Timer**
- **Mô tả:** Giám sát thời gian chạy của từng state theo bảng timeout quy định (VD: ATTACHING tối đa 60s, CLOUDS tối đa 60s, BATTLE tối đa 210s). Quá hạn tự động phát sinh sự kiện `WatchdogTimeout`.
- **Files:** `src/MyBot.Core.FSM/StateWatchdog.cs`
- **Effort:** 2h | **Deps:** T0.9
- **Test cases:**
  - `T8.1.1` — Kẹt ở một state quá hạn $\rightarrow$ Watchdog kích hoạt sự kiện cứu hộ.
  - `T8.1.2` — Chuyển state bình thường trước hạn $\rightarrow$ timer tự hủy, không leak tài nguyên.
- **Acceptance:** Ngăn chặn triệt để tình trạng bot bị treo vĩnh viễn.

### **T8.2 — Panic Recovery Orchestrator**
- **Mô tả:** Chuỗi phục hồi tự động khi phát hiện lỗi hoặc đứng game: Chụp ảnh hiện trường $\rightarrow$ Tắt tiến trình giả lập $\rightarrow$ Đợi 8s $\rightarrow$ Bật lại giả lập $\rightarrow$ Chuẩn hoá kích thước $\rightarrow$ Mở CoC $\rightarrow$ Chờ màn hình chính $\rightarrow$ Tiếp tục chạy.
- **Files:** `src/MyBot.Game.CoC/Recovery/PanicRecoveryOrchestrator.cs`
- **Effort:** 4h | **Deps:** T8.1, T1.8-T1.10, T3.9
- **Test cases:**
  - `T8.2.1` — Kích hoạt cứu hộ phục hồi lại game thành công trong $\leq 45s$.
  - `T8.2.2` — Giả lập bị crash không bật lại được quá 3 lần $\rightarrow$ chuyển trạng thái `ERROR` an toàn.
- **Acceptance:** Tự động phục hồi thành công 95% các sự cố mà không cần người can thiệp.

### **T8.3 — Graceful Shutdown Manager**
- **Mô tả:** Xử lý sự kiện tắt ứng dụng (Ctrl+C, Đóng cửa sổ): Ngừng input $\rightarrow$ Lưu checkpoint $\rightarrow$ Flush SQLite WAL $\rightarrow$ Giải phóng DC/Bitmap GDI và ADB Socket $\rightarrow$ Đóng sạch tiến trình.
- **Files:** `src/MyBot.App/GracefulShutdown.cs`
- **Effort:** 2h | **Deps:** T0.4
- **Test cases:**
  - `T8.3.1` — Tắt đột ngột không để lại tiến trình zombie `adb.exe` hay rò rỉ tài nguyên.
  - `T8.3.2` — Database SQLite lưu trọn vẹn dữ liệu, không bị corrupt file.
- **Acceptance:** Tắt ứng dụng an toàn trong ≤ 2 giây.

### **T8.4 — Supercell ID Account Switch**
- **Mô tả:** Quy trình chuyển tài khoản: Cài đặt $\rightarrow$ Đã kết nối Supercell ID $\rightarrow$ Đổi tài khoản $\rightarrow$ Chọn acc mục tiêu $\rightarrow$ Dùng OCR kiểm tra cấp TownHall để xác thực đã vào đúng acc.
- **Files:** `src/MyBot.Game.CoC/Village/SupercellIdSwitcher.cs`
- **Effort:** 4h | **Deps:** T2.2, T3.5
- **Test cases:**
  - `T8.4.1` — Chuyển đổi giữa 2 tài khoản thành công, OCR kiểm tra đúng cấp TH.
  - `T8.4.2` — Mạng lag khi chuyển tài khoản $\rightarrow$ tự động thử lại tối đa 3 lần.
- **Acceptance:** Đổi tài khoản Supercell ID mượt mà, tin cậy 99%.

### **T8.5 — Multi-Account Rotation Scheduler**
- **Mô tả:** Điều phối xoay vòng danh sách tài khoản: Đánh xong 1 trận $\rightarrow$ chuyển acc tiếp theo. Nếu một acc dính khiên bảo vệ $\rightarrow$ tạm thời cho nghỉ và chuyển acc khác.
- **Files:** `src/MyBot.Game.CoC/Village/AccountRotationScheduler.cs`
- **Effort:** 3h | **Deps:** T8.4
- **Test cases:**
  - `T8.5.1` — Xoay vòng tuần tự danh sách 3 tài khoản không bị trùng lặp.
  - `T8.5.2` — Bỏ qua tài khoản đang có khiên bảo vệ.
- **Acceptance:** Tối đa hoá thời gian farm cho nhiều tài khoản.

### **T8.6 — SQLite Repository Layer (EF Core)**
- **Mô tả:** Lưu trữ cơ sở dữ liệu: Lịch sử trận đánh, thông số tài nguyên, nhật ký lỗi. Hỗ trợ EF Core Migration.
- **Files:** `src/MyBot.Core.Data/*.cs`
- **Effort:** 3h | **Deps:** T0.5
- **Test cases:**
  - `T8.6.1` — Ghi 500 trận đánh đồng thời không bị lock database (nhờ chế độ WAL).
  - `T8.6.2` — Truy vấn thống kê 10,000 dòng dữ liệu phản hồi trong < 50ms.
- **Acceptance:** Cơ sở dữ liệu nhanh nhẹn, bền vững.

### **T8.7 — Health Check HTTP Endpoint**
- **Mô tả:** Mini Kestrel server lắng nghe cổng local 8080. Cung cấp API `GET /health` (trả về trạng thái, uptime, số trận đánh) và `GET /metrics` định dạng Prometheus.
- **Files:** `src/MyBot.App/Monitoring/HealthCheckServer.cs`
- **Effort:** 3h | **Deps:** T0.9
- **Test cases:**
  - `T8.7.1` — Gọi `GET /health` trả về JSON trạng thái chuẩn xác 200 OK.
  - `T8.7.2` — Trùng cổng $\rightarrow$ log cảnh báo, không làm crash app bot.
- **Acceptance:** Phục vụ giám sát bot từ xa trên VPS hoặc qua bot Telegram.

### **T8.8 — External Notification Service (Discord / Telegram)**
- **Mô tả:** Hiện thực `INotifier` gửi tin nhắn kèm ảnh chụp màn hình qua Discord Webhook và Telegram Bot. Có cơ chế tự động thử lại khi dính Rate Limit 429.
- **Files:** `src/MyBot.Core.Notification/*.cs`
- **Effort:** 3h | **Deps:** T0.2, T7.13
- **Test cases:**
  - `T8.8.1` — Gửi thông báo có kèm ảnh screenshot 1MB tới Discord/Telegram thành công trong ≤ 2s.
  - `T8.8.2` — Xử lý êm đẹp lỗi mất mạng, không gây crash luồng chính.
- **Acceptance:** Báo tin tức thời tới điện thoại người dùng khi có sự cố hoặc nhặt đồ ngon.

---

## 🧪 PHASE 9 — 3-LAYER TEST FRAMEWORK & CI GATES (Sprint 11, Week 15)

### **T9.1 — Golden Recording Standard Spec**
- **Mô tả:** Định chuẩn cấu trúc thư mục lưu trữ bài test: `recordings/<name>/` gồm `frames.mkv` (FFV1), `telemetry.jsonl`, `metadata.json`.
- **Effort:** 2h | **Deps:** T0.8, T1.3
- **Test cases:** `T9.1.1` Kiểm tra định dạng schema của tập recording mẫu hợp lệ.
- **Acceptance:** Tiêu chuẩn hoá dữ liệu test replay.

### **T9.2 — Layer 1: State Sequence Accuracy Test**
- **Mô tả:** Test xUnit phát lại video recording, so sánh state của bot với nhãn ground-truth trong telemetry, yêu cầu độ chính xác $\geq 95\%$.
- **Files:** `tests/MyBot.Game.CoC.Tests/StateSequenceTests.cs`
- **Effort:** 3h | **Deps:** T9.1, T3.7
- **Test cases:** `T9.2.1` Nhận diện đúng $\geq 95\%$ khung hình $\rightarrow$ Test Pass.
- **Acceptance:** Phát hiện ngay nếu ai sửa code làm hỏng State Classifier.

### **T9.3 — Layer 2: Action Subsequence Match Test**
- **Mô tả:** So sánh chuỗi hành động bot phát ra (trong `MockInput`) với chuỗi hành động mẫu: dung sai toạ độ $\leq 15px$, thời gian $\leq 500ms$. Không được thiếu action quan trọng.
- **Files:** `tests/MyBot.Game.CoC.Tests/ActionSubsequenceTests.cs`
- **Effort:** 4h | **Deps:** T9.1, T6.5, T1.6
- **Test cases:** `T9.3.1` Barch drop đúng tuần tự $\rightarrow$ Pass. `T9.3.2` Bỏ sót lệnh thả quân $\rightarrow$ Fail.
- **Acceptance:** Kiểm thử logic tấn công hoàn toàn tự động trên CI.

### **T9.4 — Layer 3: Causal Match Test**
- **Mô tả:** Kiểm tra quan hệ nhân quả: Hành động tap chỉ được phép phát ra khi game state nằm trong tập trạng thái cho phép (VD: nút Next chỉ được bấm khi đang ở `SEARCHING`).
- **Files:** `tests/MyBot.Game.CoC.Tests/CausalMatchTests.cs`
- **Effort:** 2h | **Deps:** T9.1, T0.9
- **Test cases:** `T9.4.1` Không có vi phạm nhân quả $\rightarrow$ Pass.
- **Acceptance:** Loại bỏ lỗi logic "hành động đúng nhưng bấm sai lúc".

### **T9.5 — Test Report Generator**
- **Mô tả:** Xuất báo cáo JSON `test_reports/<name>_report.json` chi tiết kết quả 3 tầng, điểm fail đầu tiên kèm ảnh chụp tại thời điểm đó.
- **Files:** `tests/MyBot.Integration.Tests/TestReportGenerator.cs`
- **Effort:** 2h | **Deps:** T9.2-T9.4
- **Test cases:** `T9.5.1` Xuất file JSON hợp lệ, dễ đọc.
- **Acceptance:** Nhìn báo cáo là biết ngay bug nằm ở đâu.

### **T9.6 — Thu thập 10 Golden Recordings Chuẩn**
- **Mô tả:** Ghi hình và gắn nhãn 10 trận đánh thực tế đa dạng: Nhà hoang TH10, TH12, TH14; Nhà sống; TH ngoài rìa; Combo Barch, Goblin.
- **Effort:** 8h | **Deps:** T0.8, T9.1
- **Test cases:** `T9.6.1` Đủ 10 bài test, mỗi bài dài $\geq 900$ frames (30s+), telemetry đầy đủ nhãn.
- **Acceptance:** Bộ dữ liệu kiểm thử vàng cho dự án.

### **T9.7 — CI 3-Layer Automation Gate** *(Bổ sung mới)*
- **Mô tả:** Tích hợp bộ test 3 tầng vào GitHub Actions, tự động chạy khi tạo Pull Request, tự động block merge nếu tỷ lệ chính xác giảm.
- **Files:** `.github/workflows/test-suite.yml`
- **Effort:** 2h | **Deps:** T9.2-T9.6
- **Test cases:** `T9.7.1` Chạy toàn bộ 10 bài test trên máy ảo Ubuntu trong < 90 giây.
- **Acceptance:** Đảm bảo không ai merge code làm thụt lùi tính năng (Regression).

### **T9.8 — Code Coverage Enforcement Gate** *(Bổ sung mới)*
- **Mô tả:** Đo lường độ bao phủ code bằng Coverlet, yêu cầu Code Coverage cho tầng Core Logic $\geq 70\%$.
- **Effort:** 1h | **Deps:** T9.7
- **Test cases:** `T9.8.1` PR mới có coverage < 70% $\rightarrow$ CI báo đỏ cảnh báo.
- **Acceptance:** Duy trì chất lượng code lâu dài.

### **T9.9 — Flaky Test Detector** *(Bổ sung mới)*
- **Mô tả:** Tự động chạy lặp lại các bài test 3 lần để phát hiện và cảnh báo các bài test thiếu tính tất định (Flaky Tests).
- **Effort:** 2h | **Deps:** T9.7
- **Test cases:** `T9.9.1` Phát hiện các bài test dựa vào biến thời gian thực chưa inject `ITimeProvider`.
- **Acceptance:** Đảm bảo test suite luôn tin cậy 100%.

---

## 📦 PHASE 10 — INTEGRATION, STRESS TEST & RELEASE (Sprint 12, Week 16)

### **T10.1 — 72 Giờ Stress Test Liên Tục**
- **Mô tả:** Chạy bot liên tục 72h trong môi trường test, ghi nhận thông số RAM, CPU, GDI Handles, Socket mỗi 5 phút.
- **Effort:** 3 ngày (tự động hoá) | **Deps:** Toàn bộ Phase 1-9
- **Test cases:**
  - `T10.1.1` — RAM duy trì $\leq 350MB$ sau 72h.
  - `T10.1.2` — CPU trung bình $\leq 25\%$.
  - `T10.1.3` — GDI handle count phẳng lì, không tăng dù chỉ 1 handle.
  - `T10.1.4` — Không xảy ra crash ứng dụng trong suốt 72h.
- **Acceptance:** Đạt toàn bộ chỉ số KPI độ bền 24/7.

### **T10.2 — Memory Leak Profiling & Fix**
- **Mô tả:** Dùng JetBrains dotMemory hoặc PerfView kiểm tra snapshot bộ nhớ, giải phóng triệt để các đối tượng `Mat` OpenCV và unbind Event Handler.
- **Effort:** 4h | **Deps:** T10.1
- **Test cases:** `T10.2.1` Xác nhận 0 Memory Leak sau 10,000 chu kỳ phân tích hình ảnh.
- **Acceptance:** Bộ nhớ sạch sẽ, tối ưu tuyệt đối.

### **T10.3 — Single-File Standalone Packaging**
- **Mô tả:** Đóng gói ứng dụng thành 1 file thực thi duy nhất: `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true`, dung lượng $< 100MB$.
- **Effort:** 2h | **Deps:** T10.1, T10.2
- **Test cases:**
  - `T10.3.1` — File chạy ngay trên máy tính Windows mới toanh không cần cài đặt trước .NET Runtime.
  - `T10.3.2` — Dung lượng file `.exe` gọn gàng $< 100MB$.
- **Acceptance:** Người dùng tải về là nhấp đúp chạy ngay.

### **T10.4 — Quickstart & Documentation Hoàn Thiện**
- **Mô tả:** Biên soạn README với hướng dẫn cài đặt trong 30 phút, bộ câu hỏi FAQ, xử lý sự cố thường gặp, KNOWN_LIMITATIONS.
- **Effort:** 4h | **Deps:** Toàn bộ
- **Test cases:** `T10.4.1` Người mới đọc tài liệu cài đặt và khởi động bot thành công trong $\leq 30$ phút.
- **Acceptance:** Tài liệu thân thiện, chuyên nghiệp.

### **T10.5 — Public GitHub Release (v1.0.0)**
- **Mô tả:** Gắn thẻ Release `v1.0.0`, đính kèm binary `.exe`, file `.zip` mã nguồn, bảng mã băm SHA256, mở kênh GitHub Discussions.
- **Effort:** 2h | **Deps:** T10.1-T10.4
- **Test cases:** `T10.5.1` Tải binary từ GitHub Release về chạy thử nghiệm thành công 100%.
- **Acceptance:** Dự án mã nguồn mở chính thức ra mắt cộng đồng.

---

## 📊 BẢNG TỔNG KẾT TOÀN DIỆN (SAU KHI CẬP NHẬT)

| Phase | Nội Dung | Số Task | Effort Dự Kiến | Sprint Phân Bổ |
| :--- | :--- | :---: | :---: | :---: |
| **0** | Foundation, FSM Primitives, Datasets & Licenses | **13** | 30h | Sprint 1 |
| **1** | Window Normalizer, Capture, Input & Drivers | **12** | 36h | Sprint 2-3 |
| **2** | Action Contract (DAV) & Anti-Ban Subsystem | **10** | 20h | Sprint 3-4 |
| **3** | Computer Vision, ONNX OCR & State Classifier | **10** | 27h | Sprint 4-5 |
| **4** | Game Math, Isometric, Red Area & Deadbase | **8** | 15h | Sprint 5 |
| **5** | Village, Clan Games, Clan Capital & Builder Base | **17** | 47h | Sprint 6-7 |
| **6** | Combat Engine, Pipe CSV, Pets & Hero Equipment | **12** | 39h | Sprint 7-8 |
| **7** | WPF MVVM UI Shell, 7 Tabs, Multi-Instance | **32** | 73h | Sprint 9-11 |
| **8** | State Watchdog, Recovery, Multi-Acc & SQLite | **8** | 25h | Sprint 12 |
| **9** | 3-Layer Test Suite, Golden Replay & CI Gates | **9** | 26h | Sprint 13-14 |
| **10** | 72h Stress Test, Profiling & Public Release | **5** | 11h | Sprint 15-16 |
| **TỔNG** | **TOÀN DỰ ÁN MYBOT C# WPF** | **145 Tasks** | **~349h $\rightarrow$ ~420h** | **16 Sprints (~4 tháng)** |

---

## 🎯 DANH SÁCH 5 VIỆC BẮT BUỘC ĐÃ ĐƯỢC GIẢI QUYẾT TRIỆT ĐỂ:

1. ✅ **Đảo chiều Dependency:** T0.9 (`BotState` enum, FSM Core) & T0.10 (Transition Guard Matrix) được định nghĩa ngay ở Phase 0 $\rightarrow$ Loại bỏ 100% nguy cơ block Sprint 8 khi làm UI T7.28.
2. ✅ **Giải quyết bế tắc Model OCR:** T0.11 (Bộ Dataset) & T0.12 (ONNX Model Prep) được đưa vào Phase 0 $\rightarrow$ Phase 3 có sẵn asset để test ngay lập tức.
3. ✅ **Xoá Circular Dependency của Tool Record:** T0.8 tách sang dùng ADB raw socket stream PNG độc lập, không còn phụ thuộc vào `ICaptureBackend` của Phase 1.
4. ✅ **Window Normalizer lên trước Capture:** T1.0 nằm ở đầu Phase 1 $\rightarrow$ Đảm bảo cửa sổ luôn đúng kích thước 860×732 logical pixels trước khi capture BitBlt.
5. ✅ **Bổ sung đủ 100% tính năng CoC theo Spec:** Clan Games (T5.15, T5.16), Clan Capital (T5.17), Builder Base Full (T5.12, T5.13, T5.14), Pet & Equipment Runtime (T6.7, T6.8), Multi-Instance UI (T7.31), Config Backup/Export (T7.32), GPL-3.0 License Audit (T0.13), và Test CI Gates (T9.7, T9.8, T9.9).
