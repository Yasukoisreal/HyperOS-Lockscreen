# HyperOS Live Lock Screen for Windows Phone 8.1

[![Platform](https://img.shields.io/badge/platform-Windows%20Phone%208.1%20Silverlight-0078D7.svg)](https://learn.microsoft.com/en-us/previous-versions/windows/apps/)
[![Framework](https://img.shields.io/badge/.NET%20Framework-Silverlight%20v8.1-512BD4.svg)](https://microsoft.com)
[![Build](https://img.shields.io/badge/build-MSBuild%2014.0-brightgreen.svg)]()
[![RAM Target](https://img.shields.io/badge/target%20RAM-512MB%2B-orange.svg)]()
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A modern, highly customizable **Live Lock Screen** application for **Windows Phone 8.1 Silverlight**, inspired by the visual design and fluid typography of **Xiaomi HyperOS**.

---

## 🌟 Highlights & Features

### 1. 📱 Native Live Lock Screen Integration
* Integrates directly into Windows Phone 8.1 via the official `LockAppExtension.xml` extensibility framework.
* Instant visual wake-up with hardware back-button security lock protection.
* Fluid swipe-to-unlock gesture physics with cubic-eased exit animations.

### 2. 🎨 Preset Carousel ("My Sets")
* Category-based preset browser featuring **Classic**, **Magazine**, and **Rhombus** styles.
* Real-time hardware-rendered thumbnail carousel powered by `ClockRenderer`.
* Instant preview and one-tap lock screen application.

### 3. ⏰ Versatile Clock Layouts & Typography
* **Classic / Horizontal**: Standard modern layout with inline carrier, date, and weather info.
* **Vertical / Stacked Digits**: Bold dual-tier hour/minute layout for large typography.
* **Analog Dials**: Multiple analog watch dials (Modern, Bauhaus, Minimalist) with animated hands.
* **Rhombus**: Angled diamond typography with dynamic date and weather badge placement.
* **Giant**: Oversized high-impact display numbers.
* **Stacked**: Left-aligned multi-line day, large time, and date stamp.
* **Upward**: Minimalist editorial style featuring date on top, zero-padded time in the center, and uppercase short day at the bottom (supports left, center, and right alignments).
* **Rich Font Library**: Includes MiSans (Regular, Demibold, Light, Bold), Bebas Neue, Playfair Display, DM Serif Display, Instrument Serif, Montserrat, Poppins, Raleway, Abril Fatface, Bodoni Moda, and Segoe WP.

### 4. ✨ 2.5D Wallpaper Depth Effect
* Layered visual hierarchy that places subject cutouts in front of clock digits.
* **Auto Subject Extraction**: Powered by the Remove.bg API with single-tap processing.
* **Custom Foreground Chooser**: Easily load any transparent PNG foreground.
* Depth layer control (bring Hour, Minute, Colon, Date, or Weather in front/behind).

### 5. 🖼️ Wallpaper Customization & Visual Effects
* Built-in wallpaper gallery with categorized presets.
* **AI Wallpaper Generation (Text-to-Image)**: Direct integration with Pollinations AI (generating optimized 1024×1024 backgrounds).
* **Lumia Imaging SDK**:
  * Real-time Gaussian blur backgrounds.
  * Optical glass and refraction distortion filters.
  * Brightness and contrast enhancements.

### 6. 🌦️ Widgets & System Info
* **Live Weather**: Integrated with the Open-Meteo API; automatically geocodes city names or retrieves current GPS coordinates.
* **Day Countdown**: Track milestones, exams, anniversaries, or vacations directly on your lock screen.
* **Custom Signature & Owner Info**: Display personal contact details or inspirational quotes.
* **Smart Battery Status**: Real-time battery indicator with low-battery flashing and charging pulse animations.
* **Media Controls**: Integrated playback control overlay for background music.

### 7. 🔒 Device Security
* **4-Digit PIN Lock**: Numerical keypad unlock screen.
* **3×3 Pattern Lock**: Android-style gesture pattern unlock.
* **Security Recovery**: Built-in security question for pattern recovery.

---

## 🚀 Performance & Low-Memory Optimization (512MB RAM)

Designed specifically for low-end Windows Phone 8.1 devices (such as Lumia 520, 525, 530, 630):
* **Aggressive Memory Reclamation**: Automatically flushes all `ImageBrush.ImageSource` allocations and triggers garbage collection on page navigation (`OnNavigatedFrom`).
* **Sub-Pixel Rounding**: Dynamically rounded layout coordinates to integer pixels prevent anti-aliasing blur and GPU rasterization strain.
* **Cached Brushes**: Centralized brush palettes and visual element caching to avoid allocation spikes during carousel swipes.

---

## 📁 Repository Structure

```
HyperOS/
├── Assets/                 # Icons, battery glyphs, wallpapers, and embedded TTF fonts
├── Controls/               # Custom XAML controls (Analog clocks, Pattern Lock grid)
├── Extensions/
│   └── LockAppExtension.xml # OS Live Lock Screen registration descriptor
├── Helpers/
│   ├── ClockRenderer.cs    # Unified clock and preset thumbnail rendering pipeline
│   └── FilterHelper.cs     # Lumia Imaging SDK filter effects (blur, glass, refraction)
├── Pages/
│   ├── About.xaml          # App information, version, and credits
│   ├── EditorPage.xaml     # Full-featured lock screen customization editor
│   ├── LockScreen.xaml     # Active live lock screen rendering surface
│   ├── LockScreenPage.xaml # OS entry point router (ScreenLocked detection)
│   ├── MySetsPage.xaml     # Preset carousel browser
│   └── SettingsPage.xaml   # System, security, and API configurations
├── Properties/
│   └── WMAppManifest.xml   # Windows Phone manifest with capabilities & extensions
├── .agents/
│   └── AGENTS.md           # Engineering guidelines, API whitelist, and memory constraints
└── HyperOS.sln             # Visual Studio solution file
```

---

## 🛠️ Building & Deployment

### Prerequisites
* **Operating System**: Windows 8.1, 10, or 11
* **IDE**: Visual Studio 2013 with Update 4/5 or Visual Studio 2015
* **SDK**: Windows Phone 8.1 Silverlight SDK
* **Build Tools**: MSBuild 14.0 or Visual Studio command prompt

### Command Line Build
To build the debug x86 XAP package:
```powershell
& "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe" "HyperOS.sln" /t:Build /p:Configuration=Debug /p:Platform="x86"
```

To build for ARM physical devices:
```powershell
& "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe" "HyperOS.sln" /t:Build /p:Configuration=Release /p:Platform="ARM"
```

The resulting package will be generated at:
```
HyperOS\Bin\ARM\Release\HyperOS_Release_ARM.xap
```

### Deployment to Device
1. Enable **Developer Unlock** on your Windows Phone 8.1 device via *Windows Phone Developer Registration*.
2. Deploy the `.xap` using **Windows Phone Application Deployment** tool or **Windows Phone Power Tools**.
3. Go to **Settings > Lock Screen** on your Windows Phone and select **HyperOS** as your lock screen provider.

---

## 📜 Credits & Acknowledgments

* **Inspiration**: Xiaomi HyperOS Lock Screen design language.
* **Base Concept**: Inspired by Lollipop Lockscreen by Flydream.
* **APIs**:
  * [Open-Meteo](https://open-meteo.com/) — Free Weather & Geocoding API.
  * [Pollinations AI](https://pollinations.ai/) — Open source text-to-image synthesis.
  * [Remove.bg](https://www.remove.bg/) — Subject cutouts for depth wallpapers.
* **Libraries**: Lumia Imaging SDK, Windows Phone Toolkit, Microsoft BCL.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
