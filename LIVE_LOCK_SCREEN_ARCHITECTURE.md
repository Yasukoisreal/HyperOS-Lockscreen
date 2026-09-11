# 📘 Windows Phone 8.1 Live Lock Screen: Technical Architecture & Developer Guide

> **Purpose:** A comprehensive, platform-level technical reference and implementation guide for developers building custom **Live Lock Screen** applications on **Windows Phone 8.1 Silverlight**.

> [!NOTE]
> **Developer Disclaimer:** This documentation is compiled from hands-on experiments, reverse engineering, and community research on Windows Phone 8.1 Silverlight. Because Microsoft never published complete official documentation for the internal Live Lock Screen extensibility framework, some details, quirks, or edge cases may not be 100% complete or universally accurate across all device firmware revisions. Feedback, bug reports, and contributions via pull requests or issues are always warmly welcomed!

---

## 1. Platform Fundamentals & Technical Architecture

### 1.1 Background & History
Prior to Windows Phone 8.1, the lock screen on Windows Phone 8.0 was strictly static. Third-party developers were limited to:
1. Setting a static wallpaper bitmap using `LockScreen.SetImageFileUri(...)`.
2. Displaying small badge counters or text in up to 5 predefined system notification slots.

Announced at **Microsoft Build 2014**, Windows Phone 8.1 introduced the **Live Lock Screen Extensibility Framework**. This platform capability enables registered applications to take over the active lock screen visual surface, rendering rich XAML typography, dynamic animations, and responsive multi-touch gestures when the user turns on the device.

### 1.2 OS-Level Architectural Reality
A critical architectural question for developers: **Does a Live Lock Screen replace the secure Windows Phone lock screen kernel?**

> **THE ANSWER IS: NO.**
>
> A Live Lock Screen app is a **specialized Silverlight application process** hosted within a protected compositor surface managed by the Windows Phone OS Shell. The operating system kernel retains full authority over hardware power states, device security policies, emergency calls, and system PIN verification.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        DEVICE DISPLAY SURFACE                          │
├────────────────────────────────────────────────────────────────────────┤
│  [Layer 1 - Highest Priority] Native OS PIN Keypad / Emergency Call    │
│  - Rendered exclusively by the OS Shell; untouchable by apps           │
├────────────────────────────────────────────────────────────────────────┤
│  [Layer 2] Live Lock Screen Extensibility App (Silverlight Runtime)    │
│  - Custom XAML UI, time/date typography, animations, gestures, widgets │
├────────────────────────────────────────────────────────────────────────┤
│  [Layer 3] OS Lock Screen Host & Watchdog Service                      │
│  - Lifecycle management, 512MB RAM quotas, and 500ms render timeouts   │
├────────────────────────────────────────────────────────────────────────┤
│  [Layer 4 - Base] Start Screen / Suspended Background Applications    │
└────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Why Silverlight 8.1 is Mandatory (WinRT is Incompatible)
Windows Phone 8.1 supports two distinct application runtimes:
- **WinRT 8.1 XAML (`Windows.UI.Xaml.*`):** Only supports static wallpaper and badge notifications via `Windows.ApplicationModel.LockScreen`. WinRT does **not** expose the compositor hooks required for interactive lock screen rendering.
- **Silverlight 8.1 (`System.Windows.*`, `Microsoft.Phone.*`):** The OS extensibility contract (`LockAppExtension`) interfaces directly with **`AgHost.exe`** (the Silverlight Application Host). **Consequently, all Live Lock Screen applications must be developed using Windows Phone 8.1 Silverlight.**

---

## 2. End-to-End Execution Lifecycle (Sequence Diagram)

The following sequence illustrates what happens from the moment the user presses the hardware power button to unlocking the home screen:

```
User                    OS Shell (Power Mgr)         App Host (AgHost)       SystemProtection
 │                                │                          │                      │
 │── [1] Press Power Button ─────>│                          │                      │
 │                                │── [2] Wake Process ─────>│                      │
 │                                │   (Warm Resume)          │                      │
 │                                │                          │── [3] Check Status ─>│
 │                                │                          │   ScreenLocked       │
 │                                │                          │<── [4] Returns true ─│
 │                                │                          │                      │
 │                                │                          │── [5] Navigate to ──>│
 │                                │                          │   LockView.xaml      │
 │                                │                          │                      │
 │                                │<── [6] Render Frame 0 ───│ (Must complete <500ms│
 │<── [7] Display Turns On ───────│    (Compositor Surface)  │  to avoid Fallback)  │
 │    (Sees Custom Clock UI)      │                          │                      │
 │                                │                          │                      │
 │── [8] Swipe Up Gesture ──────────────────────────────────>│                      │
 │   (ManipulationDelta)          │                          │                      │
 │                                │                          │                      │
 │── [9] Threshold Met ─────────────────────────────────────>│                      │
 │                                │                          │── [10] Play Exit ────│
 │                                │                          │    Animation         │
 │                                │                          │                      │
 │                                │                          │── [11] Invoke ──────>│
 │                                │<── [12] Hand Over Control ──────────────────────│
 │                                │    RequestScreenUnlock                          │
 │                                │                          │                      │
 │                       [Device has OS PIN?]                │                      │
 │                           ┌────┴────┐                     │                      │
 │                         YES         NO                    │                      │
 │                          │           │                    │                      │
 │<── [13a] Display ────────│           │                    │                      │
 │    Native OS PIN Keypad  │           │                    │                      │
 │                          │           │                    │                      │
 │<─────────────────────────┴───────────┴─── [13b] Unlock directly to Start Screen ──│
```

---

## 3. System Extensibility & Configuration

To register an application as an interactive Live Lock Screen provider and hook into the OS compositor, two configuration files are required.

### 3.1 `Properties/WMAppManifest.xml`

Declare the lock UI capability and the lock screen extension contracts:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Deployment xmlns="http://schemas.microsoft.com/windowsphone/2014/deployment" AppPlatformVersion="8.1">
  <App xmlns="" ProductID="{YOUR-PRODUCT-GUID-HERE}" 
       Title="Custom Lock Screen" 
       RuntimeType="Silverlight" 
       Version="1.0.0.0" 
       Genre="apps.normal" 
       Author="DeveloperName" 
       Description="Custom Live Lock Screen for Windows Phone 8.1" 
       Publisher="PublisherName" 
       PublisherID="{YOUR-PUBLISHER-GUID}">

    <IconPath IsRelative="true" IsResource="false">Assets\ApplicationIcon.png</IconPath>

    <Capabilities>
      <!-- CRITICAL: Allows interaction with SystemProtection lock screen APIs -->
      <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
      <Capability Name="ID_CAP_NETWORKING" />
    </Capabilities>

    <!-- Default Entry Point: Must point to the Router Gateway -->
    <!-- ActivationPolicy="Resume" is mandatory: enables instant warm-resume on screen wake-up -->
    <Tasks>
      <DefaultTask Name="_default" NavigationPage="LockRouter.xaml" ActivationPolicy="Resume" />
    </Tasks>

    <!-- Standard PrimaryToken (required by WMAppManifest schema before <Extensions>) -->
    <Tokens>
      <PrimaryToken TokenID="AppToken" TaskName="_default">
        <TemplateFlip>
          <SmallImageURI IsRelative="true" IsResource="false">Assets\Tiles\FlipCycleTileSmall.png</SmallImageURI>
          <Count>0</Count>
          <BackgroundImageURI IsRelative="true" IsResource="false">Assets\Tiles\FlipCycleTileMedium.png</BackgroundImageURI>
          <Title>CustomLockScreen</Title>
        </TemplateFlip>
      </PrimaryToken>
    </Tokens>

    <!-- Register Extensibility Extension Contracts with the OS (MUST be placed after </Tokens>) -->
    <Extensions>
      <!-- Contract 1: Designates the app as a Live Lock Screen Application -->
      <Extension ExtensionName="LockScreen_Application"
                 ConsumerID="{CD4601F6-351B-43C7-9087-6B12BD98ED63}"
                 TaskID="_default"
                 ExtraFile="Extensions\\LockAppExtension.xml" />

      <!-- Contract 2: Allows background stream feeding for static lock screen backgrounds -->
      <Extension ExtensionName="LockScreen_Background"
                 ConsumerID="{111DFF24-AA15-4A96-8006-2BFF8122084F}"
                 TaskID="_default" />
    </Extensions>

  </App>
</Deployment>
```

#### Consumer ID & Parameter Reference:
| Property / GUID | Description |
|---|---|
| `{CD4601F6-351B-43C7-9087-6B12BD98ED63}` | **LockScreen_Application**: Internal Windows Phone 8.1 Shell consumer ID. Designates the application as a Live Lock Screen host process for `AgHost.exe` and enables programmatic registration via `ExtensibilityApp.RegisterLockScreenApplication()`. |
| `{111DFF24-AA15-4A96-8006-2BFF8122084F}` | **LockScreen_Background**: Allows the application to appear in the phone's **Settings > lock screen > Background** dropdown to supply static wallpaper images. |
| `ID_CAP_SHELL_DEVICE_LOCK_UI_API` | **Lock UI Capability**: Mandatory. Grants Silverlight execution privileges to call `Windows.Phone.System.SystemProtection` APIs (`ScreenLocked`, `RequestScreenUnlock`). |
| `ActivationPolicy="Resume"` | **Activation Policy**: Critical for performance. Instructs the OS to warm-resume the suspended process in memory instead of cold-booting, preventing the "Resuming..." delay when turning on the screen. |

### 3.2 Descriptor File: `Extensions\LockAppExtension.xml`

Create a folder named `Extensions` at the project root, and add an XML file named `LockAppExtension.xml`:

```xml
<?xml version="1.0"?>
<x:Extension xmlns:x="urn:LockApp">
  <AppID>App</AppID>
</x:Extension>
```

> **Visual Studio File Properties:**
> - **Build Action:** `Content`
> - **Copy to Output Directory:** `Copy if newer`

### 3.3 Programmatic Registration: `ExtensibilityApp`

> [!IMPORTANT]
> **Key Architectural Distinction:**
> Windows Phone 8.1 system settings (**Settings > lock screen > Background**) only allows selecting *static image providers* (e.g., Bing or Photo). There is **no menu option in the phone settings** to activate a Live Lock Screen!
> 
> Instead, Live Lock Screen applications **must be activated programmatically** from inside the application using `ExtensibilityApp`:

```csharp
using Windows.Phone.System.LockScreenExtensibility;

// Check if currently registered as the active live lock screen
bool isRegistered = ExtensibilityApp.IsLockScreenApplicationRegistered();

// Programmatically register as the active live lock screen
if (!isRegistered)
{
    ExtensibilityApp.RegisterLockScreenApplication();
}

// Programmatically unregister (reverts to default OS lock screen)
ExtensibilityApp.UnregisterLockScreenApplication();
```

---

## 4. The "Dual-Role" Routing Architecture

Every Live Lock Screen application serves two entirely different execution contexts:
1. **Unlocked Context (Configuration Mode):** Launched by the user tapping the app icon or Live Tile from the Start screen or app list. It should display settings, theme customization, or widget setup.
2. **Locked Context (Lock Screen Mode):** Awakened by the OS when the screen turns on. It must immediately display the interactive lock screen interface.

### Implementing the Router: `LockRouter.xaml.cs`
Never set `DefaultTask NavigationPage` directly to either your settings page or your lock page. Use an intermediate lightweight router:

```csharp
using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.Phone.System;

namespace CustomLockScreen
{
    public partial class LockRouter : PhoneApplicationPage
    {
        public LockRouter()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                // Inspect hardware lock state
                if (SystemProtection.ScreenLocked)
                {
                    // Device is locked -> Route directly to the Live Lock Screen view
                    NavigationService.Navigate(new Uri("/LockView.xaml", UriKind.Relative));
                }
                else
                {
                    // Device is unlocked -> Route to the configuration/settings view
                    NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
                }
            }
            catch
            {
                // Fallback safe routing
                NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
            }
        }
    }
}
```

> **Core API:** `Windows.Phone.System.SystemProtection.ScreenLocked` evaluates to `true` if and only if the device is currently locked by the operating system.

---

## 5. Touch Gestures, Physics & Security Unlock Flow

When authoring the active lock screen page (`LockView.xaml`), three critical interaction requirements must be met:

### 5.1 Intercepting the Hardware Back Key
If the hardware back button is not intercepted, pressing Back will immediately exit the page or suspend the app, exposing the user's Start screen without unlocking.

```csharp
protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
{
    base.OnBackKeyPress(e);
    // MANDATORY: Block the hardware back button while on the lock screen
    e.Cancel = true;
}
```

### 5.2 Touch Manipulation & Snap-Back Physics
Smooth lock screens track the user's touch displacement in real-time. If the drag is released before reaching the unlock threshold, the UI must smoothly snap back:

```csharp
private double dragDeltaY = 0;
private const double UNLOCK_THRESHOLD = -150.0; // Dragging up beyond -150px triggers unlock

private void LayoutRoot_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
{
    dragDeltaY += e.DeltaManipulation.Translation.Y;
    
    // Clamp: Only allow upward dragging
    if (dragDeltaY > 0) dragDeltaY = 0;

    // Follow finger
    ContentTransform.TranslateY = dragDeltaY;

    // Optional: Fade content proportionally as dragged up
    double opacity = 1.0 - Math.Min(1.0, Math.Abs(dragDeltaY) / 450.0);
    ContentPanel.Opacity = opacity;
}

private void LayoutRoot_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
{
    // Unlock condition: Exceeded distance threshold OR flicked with high upward velocity
    if (dragDeltaY < UNLOCK_THRESHOLD || e.FinalVelocities.LinearVelocity.Y < -800)
    {
        InitiateUnlockSequence();
    }
    else
    {
        // Insufficient drag -> Snap back with cubic ease
        PlaySnapBackAnimation();
    }
}

private void PlaySnapBackAnimation()
{
    var sb = new Storyboard();
    
    var animY = new DoubleAnimation
    {
        To = 0,
        Duration = TimeSpan.FromMilliseconds(250),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    Storyboard.SetTarget(animY, ContentTransform);
    Storyboard.SetTargetProperty(animY, new PropertyPath("TranslateY"));
    
    var animOp = new DoubleAnimation
    {
        To = 1.0,
        Duration = TimeSpan.FromMilliseconds(250)
    };
    Storyboard.SetTarget(animOp, ContentPanel);
    Storyboard.SetTargetProperty(animOp, new PropertyPath("Opacity"));

    sb.Children.Add(animY);
    sb.Children.Add(animOp);
    
    dragDeltaY = 0;
    sb.Begin();
}
```

### 5.3 Requesting the Unlock: `SystemProtection.RequestScreenUnlock()`

Once the unlock gesture or animation concludes, pass control back to the operating system:

```csharp
private void InitiateUnlockSequence()
{
    // Trigger smooth slide-out animation before requesting unlock
    PlayExitAnimation(() =>
    {
        try
        {
            if (SystemProtection.ScreenLocked)
            {
                // HAND CONTROL OVER TO THE OPERATING SYSTEM
                SystemProtection.RequestScreenUnlock();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Unlock error: " + ex.Message);
        }
    });
}
```

### 5.4 Operating System Security Guarantee
- **Device has NO native password:** The OS immediately drops the lock screen compositor layer and restores the user to their previous app or Start screen.
- **Device HAS a native password (PIN configured in Phone Settings):** The operating system **instantly presents the Native Windows Phone PIN Keypad overlay on top**. The user must enter their valid device PIN to gain access.
- **Conclusion:** A Live Lock Screen application can never compromise device security, bypass PIN protection, or introduce lock screen vulnerabilities.

---

## 6. Performance Engineering & The "Resuming..." Problem (512MB RAM)

### 6.1 The Technical Cause of "Resuming..." Delays
Microsoft's official *Live Lock Screen BETA* received widespread criticism in 2014 because users frequently encountered a gray *"Resuming..."* screen for 1 to 2 seconds upon pressing the power button.

**Root Technical Factors:**
1. **512MB RAM Constraints:** Budget devices (e.g., Lumia 520, 525, 530, 630) formed the vast majority of Windows Phone 8.1 hardware. The system memory limit for lock screen host processes is strictly capped (typically under 40–60MB).
2. **Uncompressed Bitmap Memory Allocation:** When a JPEG or PNG wallpaper is loaded into memory, it decodes to uncompressed 32-bit ARGB:
   $$\text{Memory} = \text{Width} \times \text{Height} \times 4 \text{ bytes}$$
   A single $1080 \times 1920$ image requires **$\approx 8.3 \text{ MB}$** of pure RAM. Decoding multiple high-resolution assets simultaneously causes immediate GC pressure and paging latency.
3. **OS Watchdog Timeout (500ms):** The OS watchdog monitors wake times. If the lock screen application fails to render its initial frame within approximately **500ms to 1000ms**, the watchdog forcefully aborts the app and falls back to the default static wallpaper lock screen.

### 6.2 Essential Optimization Rules

#### A. Aggressive Memory Reclamation in `OnNavigatedFrom`
Whenever the screen turns off or the phone is unlocked, immediately sever image references and collect garbage:

```csharp
protected override void OnNavigatedFrom(NavigationEventArgs e)
{
    base.OnNavigatedFrom(e);

    // Detach image sources to immediately free unmanaged bitmap buffers
    if (WallpaperBrush != null) WallpaperBrush.ImageSource = null;

    // Stop recurring animations and DispatcherTimers
    if (clockTimer != null && clockTimer.IsEnabled) clockTimer.Stop();

    // Proactively invoke Garbage Collection
    GC.Collect();
}
```

#### B. Pixel Snapping (Eliminating Sub-Pixel Interpolation)
Silverlight layout calculations with floating-point coordinates force the compositor to perform sub-pixel anti-aliasing interpolation, causing noticeable font blurriness and GPU rasterization penalties. Always round dynamically calculated coordinates to integers:

```csharp
// Always round layout coordinates to (int)
int pixelAlignedX = (int)Math.Round(calculatedX);
int pixelAlignedY = (int)Math.Round(calculatedY);
ClockElement.Margin = new Thickness(pixelAlignedX, pixelAlignedY, 0, 0);
```

#### C. Streamlined Visual Tree
- Avoid deeply nested layout containers (`Grid` within `Border` within `StackPanel`).
- Prefer direct property assignment in code-behind over complex XAML DataBindings on the lock screen surface. Direct assignments (`TimeText.Text = DateTime.Now.ToString("HH:mm")`) bypass binding expression trees and execute orders of magnitude faster.

---

## 7. Step-by-Step Implementation Guide

Follow this guide to build a new Live Lock Screen application from scratch:

### Step 1: Create the Project
1. Open **Visual Studio 2015** (or Visual Studio 2013).
2. Go to `File > New > Project`.
3. Select `Visual C# > Windows Phone Apps > Blank App (Windows Phone Silverlight)`.
4. Target **Windows Phone 8.1**.

### Step 2: Configure `WMAppManifest.xml`
1. Open `Properties\WMAppManifest.xml` using the XML Code Editor.
2. Under `<Capabilities>`, add:
   ```xml
   <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
   ```
3. Set the default navigation task to `LockRouter.xaml`:
   ```xml
   <DefaultTask Name="_default" NavigationPage="LockRouter.xaml" ActivationPolicy="Resume" />
   ```
4. Immediately after the closing `</Tokens>` tag, insert:
   ```xml
   <Extensions>
     <Extension ExtensionName="LockScreen_Application"
                ConsumerID="{CD4601F6-351B-43C7-9087-6B12BD98ED63}"
                TaskID="_default"
                ExtraFile="Extensions\\LockAppExtension.xml" />
     <Extension ExtensionName="LockScreen_Background"
                ConsumerID="{111DFF24-AA15-4A96-8006-2BFF8122084F}"
                TaskID="_default" />
   </Extensions>
   ```

### Step 3: Add `LockAppExtension.xml`
1. Add a new project folder named `Extensions`.
2. Add a new XML file inside named `LockAppExtension.xml`.
3. Set its content to:
   ```xml
   <?xml version="1.0"?>
   <x:Extension xmlns:x="urn:LockApp">
     <AppID>App</AppID>
   </x:Extension>
   ```
4. In the **Properties** panel for `LockAppExtension.xml`:
   - Set **Build Action** to `Content`.
   - Set **Copy to Output Directory** to `Copy if newer`.

### Step 4: Implement `LockRouter.xaml`
1. Add a new `Windows Phone Portrait Page` named `LockRouter.xaml`.
2. Minimal `LockRouter.xaml` XAML:
   ```xml
   <phone:PhoneApplicationPage
       x:Class="MyLockScreen.LockRouter"
       xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:phone="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone"
       xmlns:shell="clr-namespace:Microsoft.Phone.Shell;assembly=Microsoft.Phone"
       SupportedOrientations="Portrait" Orientation="Portrait"
       shell:SystemTray.IsVisible="False">
       <Grid x:Name="LayoutRoot" Background="Black" />
   </phone:PhoneApplicationPage>
   ```
3. In `LockRouter.xaml.cs`:
   ```csharp
   using System;
   using System.Windows.Navigation;
   using Microsoft.Phone.Controls;
   using Windows.Phone.System;

   namespace MyLockScreen
   {
       public partial class LockRouter : PhoneApplicationPage
       {
           public LockRouter()
           {
               InitializeComponent();
           }

           protected override void OnNavigatedTo(NavigationEventArgs e)
           {
               base.OnNavigatedTo(e);
               if (SystemProtection.ScreenLocked)
                   NavigationService.Navigate(new Uri("/LockView.xaml", UriKind.Relative));
               else
                   NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
           }
       }
   }
   ```

### Step 5: Implement `LockView.xaml`
1. Add a new `Windows Phone Portrait Page` named `LockView.xaml`.
2. Complete XAML layout (`LockView.xaml`):
   ```xml
   <phone:PhoneApplicationPage
       x:Class="MyLockScreen.LockView"
       xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:phone="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone"
       xmlns:shell="clr-namespace:Microsoft.Phone.Shell;assembly=Microsoft.Phone"
       SupportedOrientations="Portrait" Orientation="Portrait"
       shell:SystemTray.IsVisible="False">

       <Grid x:Name="LayoutRoot" Background="Black"
             ManipulationDelta="LayoutRoot_ManipulationDelta"
             ManipulationCompleted="LayoutRoot_ManipulationCompleted">
           
           <StackPanel x:Name="ContentPanel" VerticalAlignment="Center" HorizontalAlignment="Center">
               <StackPanel.RenderTransform>
                   <CompositeTransform x:Name="ContentTransform" />
               </StackPanel.RenderTransform>
               
               <TextBlock x:Name="TimeText" Text="12:00" FontSize="88" 
                          HorizontalAlignment="Center" Foreground="White" />
               <TextBlock x:Name="DateText" Text="Monday, January 1" FontSize="22" 
                          HorizontalAlignment="Center" Foreground="#AAFFFFFF" />
               <TextBlock Text="▲ Swipe up to unlock" FontSize="16" Margin="0,60,0,0"
                          HorizontalAlignment="Center" Foreground="#66FFFFFF" />
           </StackPanel>
       </Grid>
   </phone:PhoneApplicationPage>
   ```
3. Complete code-behind (`LockView.xaml.cs`):
   ```csharp
   using System;
   using System.Windows;
   using System.Windows.Input;
   using System.Windows.Media.Animation;
   using System.Windows.Navigation;
   using System.Windows.Threading;
   using Microsoft.Phone.Controls;
   using Windows.Phone.System;

   namespace MyLockScreen
   {
       public partial class LockView : PhoneApplicationPage
       {
           private DispatcherTimer clockTimer;
           private double dragDeltaY = 0;
           private const double UNLOCK_THRESHOLD = -150.0;

           public LockView()
           {
               InitializeComponent();
               UpdateTime();

               clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
               clockTimer.Tick += (s, e) => UpdateTime();
           }

           protected override void OnNavigatedTo(NavigationEventArgs e)
           {
               base.OnNavigatedTo(e);

               // Remove LockRouter from back stack so hardware Back won't navigate back to it
               while (NavigationService.CanGoBack)
                   NavigationService.RemoveBackEntry();

               clockTimer.Start();
           }

           protected override void OnNavigatedFrom(NavigationEventArgs e)
           {
               base.OnNavigatedFrom(e);
               clockTimer.Stop();
           }

           protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
           {
               base.OnBackKeyPress(e);
               // CRITICAL: Block hardware back button to maintain screen lock
               e.Cancel = true;
           }

           private void UpdateTime()
           {
               var now = DateTime.Now;
               TimeText.Text = now.ToString("HH:mm");
               DateText.Text = now.ToString("dddd, MMMM d");
           }

           private void LayoutRoot_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
           {
               dragDeltaY += e.DeltaManipulation.Translation.Y;
               if (dragDeltaY > 0) dragDeltaY = 0; // Only allow dragging upwards

               ContentTransform.TranslateY = dragDeltaY;
               ContentPanel.Opacity = 1.0 - Math.Min(1.0, Math.Abs(dragDeltaY) / 450.0);
           }

           private void LayoutRoot_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
           {
               if (dragDeltaY < UNLOCK_THRESHOLD || e.FinalVelocities.LinearVelocity.Y < -800)
               {
                   if (SystemProtection.ScreenLocked)
                       SystemProtection.RequestScreenUnlock();
               }
               else
               {
                   // Snap back with cubic ease
                   var sb = new Storyboard();
                   var animY = new DoubleAnimation
                   {
                       To = 0,
                       Duration = TimeSpan.FromMilliseconds(200),
                       EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                   };
                   Storyboard.SetTarget(animY, ContentTransform);
                   Storyboard.SetTargetProperty(animY, new PropertyPath("TranslateY"));
                   sb.Children.Add(animY);

                   var animOp = new DoubleAnimation
                   {
                       To = 1.0,
                       Duration = TimeSpan.FromMilliseconds(200)
                   };
                   Storyboard.SetTarget(animOp, ContentPanel);
                   Storyboard.SetTargetProperty(animOp, new PropertyPath("Opacity"));
                   sb.Children.Add(animOp);

                   dragDeltaY = 0;
                   sb.Begin();
               }
           }
       }
   }
   ```

### Step 6: Activate the Live Lock Screen

> [!NOTE]
> The phone's built-in **Settings > lock screen > Background** menu only selects static wallpaper feeds (like Bing). It **cannot** enable a Live Lock Screen application.
> 
> Therefore, Live Lock Screens **must be activated programmatically** from within your application (for example, via an Enable switch in your settings page):

```csharp
using Windows.Phone.System.LockScreenExtensibility;

// Inside your Settings or Setup page:
private void EnableLiveLockScreen()
{
    try
    {
        if (!ExtensibilityApp.IsLockScreenApplicationRegistered())
        {
            // Prompts the OS to activate this application as the Live Lock Screen
            ExtensibilityApp.RegisterLockScreenApplication();
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Registration error: " + ex.Message);
    }
}

private void DisableLiveLockScreen()
{
    try
    {
        if (ExtensibilityApp.IsLockScreenApplicationRegistered())
        {
            // Reverts back to the standard OS static lock screen
            ExtensibilityApp.UnregisterLockScreenApplication();
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine("Unregistration error: " + ex.Message);
    }
}
```

---

## 8. API Compatibility Matrix (Cheat Sheet)

| Capability / API | ❌ Banned (WinRT / UWP) | ✅ Required (WP8.1 Silverlight) |
|---|---|---|
| **UI Framework** | `Windows.UI.Xaml.*` | `System.Windows.*` |
| **Lock Screen Registration** | `Windows.ApplicationModel.LockScreen.*` | `Windows.Phone.System.LockScreenExtensibility.ExtensibilityApp` |
| **File Storage** | `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| **Settings Storage** | `Windows.Storage.ApplicationData` | `System.IO.IsolatedStorage.IsolatedStorageSettings` |
| **Photo Chooser** | `Windows.Storage.Pickers.FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| **Hardware Back Key** | `HardwareButtons.BackPressed` | `PhoneApplicationPage.BackKeyPress` event |
| **Page Navigation** | `Frame.Navigate(...)` | `NavigationService.Navigate(...)` |
| **UI Thread Dispatch** | `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke(...)` |
| **Lock Screen Unlock** | `Application.Current.Exit()` | `SystemProtection.RequestScreenUnlock()` |

---

## 9. Summary

Windows Phone 8.1's **Live Lock Screen** extensibility architecture delivers an optimal balance of customization and security:
1. **Absolute Security:** Custom visuals run safely in user-space; kernel security and device PIN verification remain intact.
2. **Full Interactivity:** Rich Silverlight XAML animations, touch physics, and real-time widgets.
3. **Instant Performance:** Adhering to aggressive memory cleanup (`OnNavigatedFrom`), integer pixel snapping, and lightweight visual trees guarantees responsive, sub-500ms frame rendering even on resource-constrained 512MB RAM devices.
