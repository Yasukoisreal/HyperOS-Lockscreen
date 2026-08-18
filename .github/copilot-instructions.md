# GitHub Copilot Instructions — HyperOS Live Lock Screen

You are assisting with a **Windows Phone 8.1 Silverlight** project. Prioritize correctness, XAML compatibility, and memory efficiency over modern patterns.

## 1. WP8.1 Project-Specific Constraints

### Hardware & OS Limitations
- **Framework:** **Windows Phone 8.1 Silverlight** (NOT WinRT/UWP). Ensure ALL APIs and UI components (XAML) are WP8.1 Silverlight compatible.
- **RAM Target:** 512MB RAM devices. Aggressively free image resources when not in use.
- **No DecodePixelWidth limit:** Do NOT set `DecodePixelWidth` or `DecodePixelHeight` on `BitmapImage` (except for small thumbnails like `MySetsPage` cards).

### BANNED APIs & Namespaces (WinRT/UWP only)
| ❌ DO NOT USE | ✅ USE INSTEAD (Silverlight) |
|---|---|
| `Windows.UI.Xaml.*` | `System.Windows.*` |
| `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| `Windows.Storage.Pickers.FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| `HttpClient` (Windows.Web.Http) | `System.Net.Http.HttpClient` (NuGet) |
| `async Task Main()` | Event-based async (`BeginXxx`/`EndXxx`) or `Microsoft.Bcl.Async` |
| `Windows.Phone.UI.Input.HardwareButtons` | `PhoneApplicationPage.BackKeyPress` event |
| `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke()` |
| `x:Bind` | `{Binding}` or code-behind assignment |
| `NavigationView`, `SplitView` | `PhoneApplicationPage` + `NavigationService` |

### ALLOWED APIs & Patterns
- **Navigation:** `NavigationService.Navigate(new Uri("/Pages/Xyz.xaml", UriKind.Relative))`
- **Storage:** `IsolatedStorageSettings.ApplicationSettings["key"]`
- **Images:** `BitmapImage` + `SetSource(stream)` or `UriSource`
- **Timers:** `DispatcherTimer`
- **Animations:** `Storyboard`, `DoubleAnimation`, `DoubleAnimationUsingKeyFrames`
- **Gestures:** `ManipulationDelta`, `Tap`
- **Lock Screen:** `Windows.Phone.System.SystemProtection.ScreenLocked`
- **UI Toolkit:** `Microsoft.Phone.Controls.Toolkit`

## 2. Lock Screen Specifics
- **Performance First:** The lock screen must render instantly. Minimize synchronous blocking tasks in `OnNavigatedTo` or constructors. Keep the visual tree lightweight.
- **Context Detection:** Rely on `Windows.Phone.System.SystemProtection.ScreenLocked` to detect the context of the app (whether it is running as the actual lock screen or opened normally as a standalone app).
- **Back Key Blocking:** When displaying as the actual lock screen, the hardware back button MUST be blocked (`e.Cancel = true` in `BackKeyPress`) to prevent the user from bypassing the lock.
- **Memory Cleanup:** In `OnNavigatedFrom`, always set `ImageBrush.ImageSource = null` for all loaded images to free memory immediately.

## 3. Project Architecture & Key Files

| File/Layer | Responsibility |
|---|---|
| **View (`Pages/*.xaml`)** | UI layout only. No logic. |
| **Controller (`Pages/*.xaml.cs`)** | Event handling, navigation, state management. |
| **Rendering (`Helpers/ClockRenderer.cs`)** | Centralized logic for drawing clocks and dates across all pages. |
| `LockScreenPage.xaml` | Live Lock Screen routing gateway (Entry point). **DO NOT BREAK.** |
| `MySetsPage.xaml` | Preset carousel and entry point for unlocked device state. |
| `EditorPage.xaml` | Customization editor. |

## 4. Implementation Rules
1. **Simplicity over abstraction:** Do not build generic interfaces or utility wrappers for single-use logic.
2. **UI Consistency:** Mirror existing naming, architecture, and coding patterns in XAML and C#.

## 5. AI Integration (Pollinations AI)
- The project uses **Pollinations AI** for background generation via `CustomMode`.
- **Resolution Limit:** Always enforce `width=1024&height=1024` in the prompt URL to prevent OOM crashes on 512MB RAM.
- **Concurrency:** Ensure `isGeneratingAI` boolean flags are checked to prevent multiple concurrent HTTP requests.

## 6. Fonts Available in Project
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

## 7. Autonomous Execution & Copilot Edits
If you are running autonomously (via Copilot Edits or Agent mode with terminal access):
1. **Read before writing:** Use terminal commands or editor context to read full files completely before modifying.
2. **Surgical Changes:** Use precise diffs/replacements. Do NOT reformat or alter existing comments, indentation, or style in unrelated blocks.
3. **Verify before completing:** Always run build after code changes to catch WP8.1 Silverlight syntax/API errors:
   `& "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe" "HyperOS.sln" /t:Build /p:Configuration=Debug /p:Platform="x86"`
4. **Git Version Control:** Once the build succeeds and the task is fully completed, ALWAYS automatically run `git add .` and create a clear, meaningful commit (`git commit -m "..."`).
