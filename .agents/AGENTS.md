# AGENTS.md — HyperOS Live Lock Screen

Behavioral guidelines optimized for **Gemini 3.1 Pro (High)** working on a Windows Phone 8.1 Silverlight project. These guidelines prioritize correctness, precision, and caution over speed.

---

## 0. Pre-flight Check: Read & Follow Guidelines
**MANDATORY FIRST STEP BEFORE ANY CODE MODIFICATION:**
- You MUST thoroughly review ALL rules and constraints in this file before proposing or executing any code changes.
- Project-specific constraints (especially WP8.1 Silverlight API compatibility, RAM limits, and lock screen specifics) must be strictly adhered to in EVERY tool call and generated code block.
- **If uncertain about an API or XAML element:** Search/verify it exists in WP8.1 Silverlight BEFORE writing code. Do NOT assume availability.

---

## 1. Grounding & Think Before Coding

**Always ground in actual code. Don't assume, don't guess.**

- **Read before writing:** Read full files completely before modifying (`view_file`). Never guess file content, imports, function signatures, or existing patterns.
- **Clarify ambiguities:** If a requirement has multiple valid interpretations, explicitly state them and ask — do not silently pick one.
- **Surface simpler solutions:** If the requested approach is over-engineered, suggest the simpler, safer alternative.
- **Acknowledge unknowns:** If documentation or code context is missing, investigate or ask rather than hallucinating APIs.
- **Verify API existence:** Before using ANY .NET/Silverlight/Windows Phone API, confirm it exists in WP8.1 Silverlight. When in doubt, search the web.

## 2. Simplicity & Minimalist Architecture

**Minimum code that robustly solves the problem. No speculative abstractions.**

- **YAGNI (You Aren't Gonna Need It):** No features, helpers, or config toggles beyond what was explicitly requested.
- **No premature abstractions:** Do not build generic interfaces, abstract base classes, or utility wrappers for single-use logic.
- **No defensive overkill:** Avoid excessive boilerplate error handling or fallbacks for impossible scenarios.
- **Conciseness over verbosity:** If a solution can be implemented cleanly in 30 lines, do NOT generate 150 lines of boilerplate.
- **No unnecessary design patterns:** Do not introduce Observer, Strategy, Factory, or similar patterns unless the user explicitly requests them. Simple static methods and direct code are preferred.

> *Ask yourself:* "Would a senior engineer consider this overcomplicated or unnecessarily verbose?" If yes, simplify.

## 3. Surgical & Targeted Changes

**Touch only what you must. Clean up only your own impact.**

When editing existing code:
- **Targeted edits:** Use precise diffs/replacements. Do NOT reformat or overwrite entire files when changing a few lines.
- **Preserve surrounding code:** Do NOT "clean up", reformat, or alter existing comments, indentation, or style in unrelated blocks.
- **Match project conventions:** Strictly mirror existing naming, architecture, and coding patterns in the repository.
- **Handle orphans:** Clean up unused imports, variables, or functions that *your* changes made obsolete, but leave pre-existing dead code untouched unless asked.

> *Golden rule:* Every changed line in the diff must trace directly to the user's objective.

## 4. Goal-Driven Execution & Verification

**Define explicit success criteria. Verify before declaring completion.**

- **Transform tasks into verifiable checkpoints:**
  - *"Fix the bug"* → Identify root cause, reproduce/verify failure, apply fix, verify resolution.
  - *"Refactor component"* → Confirm behavior and functionality match exactly before and after.
  - *"Add feature"* → Implement, run build/tests, verify all edge cases.
- **Always run build after code changes:**
  ```
  & "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe" "d:\Documents\Visual Studio 2015\Projects\HyperOS\HyperOS.sln" /t:Build /p:Configuration=Debug /p:Platform="x86"
  ```
- **Self-Review:** Inspect diffs before finalizing to ensure zero unintended side effects or syntax regressions.

## 5. Direct & Concise Communication

- Avoid sycophancy, excessive apologies, or repetitive disclaimers.
- Focus responses on concise explanations of technical decisions, rationale for changes, and actionable verification results.

## 6. Git Version Control & Regular Commits

- **Commit upon completing tasks:** Always create a clear, meaningful Git commit after implementing a feature, refactoring, or fixing a bug once build/verification succeeds.
- **Git config:** User name is `Yasuko`, email is `nguyentruongan06052007@gmail.com`.
- **Prevent regressions:** Keep git working tree clean and committed to make rollbacks easy and prevent code loss.

---

## 7. WP8.1 Project-Specific Constraints (HyperOS Lockscreen)

### Hardware & OS Limitations

- **Silverlight Framework:** This project is built using **Windows Phone 8.1 Silverlight** (NOT WinRT/UWP). Ensure ALL APIs and UI components (XAML) are compatible with WP8.1 Silverlight.
- **RAM Target:** Target **512MB RAM** devices. Be extremely mindful of memory limits and aggressively free image resources when not in use.
- **No DecodePixelWidth limit:** Do NOT set `DecodePixelWidth` or `DecodePixelHeight` on BitmapImage. Load images at full quality. The app loads at most 2 images (background + foreground for depth).

### BANNED APIs & Namespaces (Do NOT use)

These are WinRT/UWP-only and will cause build failures on WP8.1 Silverlight:

| ❌ BANNED | ✅ USE INSTEAD |
|---|---|
| `Windows.UI.Xaml.*` | `System.Windows.*` (Silverlight XAML) |
| `Windows.UI.Notifications.*` | Not available — skip notifications |
| `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| `Windows.Storage.Pickers.FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| `HttpClient` (Windows.Web.Http) | `System.Net.Http.HttpClient` (NuGet) |
| `async Task Main()` | Event-based async (`BeginXxx`/`EndXxx` or `Microsoft.Bcl.Async`) |
| `Windows.Phone.UI.Input.HardwareButtons` | `PhoneApplicationPage.BackKeyPress` event |
| `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke()` |
| `x:Bind` | `{Binding}` or code-behind direct assignment |
| `NavigationView`, `SplitView` | `PhoneApplicationPage` + `NavigationService` |

### ALLOWED APIs & Patterns (Safe to use)

| Category | API/Pattern |
|---|---|
| Navigation | `NavigationService.Navigate(new Uri("/Pages/Xyz.xaml", UriKind.Relative))` |
| Storage Settings | `IsolatedStorageSettings.ApplicationSettings["key"]` |
| Storage Files | `IsolatedStorageFile.GetUserStoreForApplication()` |
| Image Loading | `BitmapImage` + `SetSource(stream)` or `UriSource` |
| Image Processing | `Lumia.Imaging.*` (BlurFilter, FilterEffect, etc.) |
| Timers | `DispatcherTimer` |
| Animations | `Storyboard`, `DoubleAnimation`, `DoubleAnimationUsingKeyFrames` |
| Touch/Gesture | `ManipulationDelta`, `ManipulationCompleted`, `Tap`, `DoubleTap` |
| Battery | `Windows.Phone.Devices.Power.Battery.GetDefault()` |
| Lock Screen | `Windows.Phone.System.SystemProtection.ScreenLocked` |
| Lock Screen | `Windows.Phone.System.SystemProtection.RequestScreenUnlock()` |
| Lock Registration | `Windows.Phone.System.LockScreenExtensibility.ExtensibilityApp` |
| HTTP Requests | `System.Net.Http.HttpClient` (from NuGet `Microsoft.Net.Http`) |
| Phone Tasks | `PhotoChooserTask`, `EmailComposeTask` |
| Toolkit Controls | `Microsoft.Phone.Controls.Toolkit` (ToggleSwitch, GestureService, etc.) |
| Canvas Drawing | `System.Windows.Shapes.*` (Line, Ellipse, Rectangle, Path) |
| WriteableBitmap | `System.Windows.Media.Imaging.WriteableBitmap` |

### Lock Screen Specifics

- **Performance First:** The lock screen must render instantly when the user wakes up their phone. Minimize synchronous blocking tasks in `OnNavigatedTo` or constructors. Keep the visual tree lightweight.
- **Context Detection:** Rely on `Windows.Phone.System.SystemProtection.ScreenLocked` to detect the context of the app (whether it's running as the actual lock screen or opened normally as a standalone app for settings/customization).
- **Entry Point:** `LockScreenPage.xaml` is the routing gateway. It checks `ScreenLocked` and navigates to either `LockScreen.xaml` (locked) or `MySetsPage.xaml` (unlocked). This file and its logic must NEVER be modified without explicit user permission.
- **Back Key Blocking:** When displaying as the actual lock screen, the hardware back button MUST be blocked (`e.Cancel = true` in `BackKeyPress`) to prevent the user from bypassing the lock.
- **Memory Cleanup:** In `OnNavigatedFrom`, always set `ImageBrush.ImageSource = null` for all loaded images to free memory immediately.

### Project Architecture

| Layer | Location | Responsibility |
|---|---|---|
| **View** | `Pages/*.xaml`, `Controls/*.xaml` | UI layout only, no business logic |
| **Controller** | `Pages/*.xaml.cs`, `Helpers/` | Event handling, navigation, state management |
| **Rendering** | `Helpers/ClockRenderer.cs` | Centralized logic for drawing clocks and dates across LockScreen, Editor, and MySets |

### Key Files — DO NOT DELETE OR BREAK

| File | Purpose |
|---|---|
| `LockScreenPage.xaml / .cs` | Live Lock Screen routing gateway — CRITICAL |
| `MainPage.xaml / .cs` | Currently unused. Entry point is LockScreenPage |
| `Pages/MySetsPage.xaml / .cs` | The preset carousel and entry point for unlocked device state |
| `Pages/EditorPage.xaml / .cs` | The customization editor |
| `Extensions/LockAppExtension.xml` | OS lock screen registration descriptor |
| `Properties/WMAppManifest.xml` | App manifest with capabilities & extensions |
| `App.xaml.cs` | ContractActivated handler for FileOpenPicker |

### AI Integration (Pollinations AI)

- The project uses **Pollinations AI** (API-less) for the `CustomMode` background generation.
- **Resolution Limit:** Always enforce `width=1024&height=1024` in the prompt URL. Do NOT request higher resolutions (e.g., 1080x1920) as they cause aspect ratio stretching issues and risk Out-of-Memory (OOM) crashes on 512MB RAM devices.
- **Concurrency:** Ensure `isGeneratingAI` boolean flags are used to prevent multiple concurrent HTTP requests when the user taps "Apply" or "Generate" multiple times rapidly.

### Fonts Available in Project

| Index | Font Family | File Path |
|---|---|---|
| 0 | MiSans Regular | `/Assets/Fonts/MiSans-Regular.ttf#MiSans` |
| 1 | MiSans Demibold | `/Assets/Fonts/MiSans-Demibold.ttf#MiSans` |
| 2 | MiSans Light | `/Assets/Fonts/MiSans-Light.ttf#MiSans` |
| 3 | Bebas Neue | `/Assets/Fonts/BebasNeue-Regular.ttf#Bebas Neue` |
| 4 | Playfair Display | `/Assets/Fonts/PlayfairDisplay-Regular.ttf#Playfair Display` |
| 5 | DM Serif Display | `/Assets/Fonts/DMSerifDisplay-Regular.ttf#DM Serif Display` |
| 6 | Instrument Serif | `/Assets/Fonts/InstrumentSerif-Regular.ttf#Instrument Serif` |
| 7 | Montserrat Bold | `/Assets/Fonts/Montserrat-Bold.ttf#Montserrat` |
| 8 | Poppins SemiBold | `/Assets/Fonts/Poppins-SemiBold.ttf#Poppins` |
| 9 | Raleway Light | `/Assets/Fonts/Raleway-Light.ttf#Raleway` |
| 10 | Abril Fatface | `/Assets/Fonts/AbrilFatface-Regular.ttf#Abril Fatface` |
| 11 | Playfair Display Italic | `/Assets/Fonts/PlayfairDisplay-Italic.ttf#Playfair Display` |
| 12 | Bodoni Moda | `/Assets/Fonts/BodoniModa-Regular.ttf#Bodoni Moda` |
| 13 | Bodoni Moda Italic | `/Assets/Fonts/BodoniModa-Italic.ttf#Bodoni Moda` |
| 14 | Segoe WP | (System font) |
| 15 | Segoe WP Black | (System font) |
| - | MiSans Bold | `/Assets/Fonts/MiSans-Bold.ttf#MiSans` (Có file nhưng chưa tham chiếu) |

### Missing Assets & Warnings

- **Dung lượng**: Nhóm font MiSans chiếm ~31.5MB. Cần chú ý tổng dung lượng XAP khi thêm ảnh nền Rhombus mới (mỗi ảnh ~0.5 - 1MB).

---

**These guidelines are working if:** diffs contain zero unnecessary changes, solutions avoid over-engineering, code uses ONLY WP8.1 Silverlight-compatible APIs, build succeeds with 0 errors, and clarifying questions precede implementation.


