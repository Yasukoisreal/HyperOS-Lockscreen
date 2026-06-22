# 🛠️ Hướng Dẫn Tạo Dự Án Lollipop Lockscreen trong Visual Studio 2015

## Yêu Cầu Hệ Thống

> [!IMPORTANT]
> Trước khi bắt đầu, bạn cần đảm bảo đã cài đặt đầy đủ:

| Yêu cầu | Chi tiết |
|---|---|
| **OS** | Windows 8.1 / Windows 10 / Windows 11 |
| **Visual Studio** | Visual Studio 2015 (Community/Professional/Enterprise) |
| **SDK** | Windows Phone 8.1 SDK (đi kèm VS 2015 hoặc cài riêng) |
| **Emulator** | Windows Phone 8.1 Emulator (cần Hyper-V) |

> [!WARNING]
> **Windows Phone 8.1 SDK không còn được hỗ trợ trên VS 2017+**. Visual Studio 2015 là phiên bản cuối cùng hỗ trợ đầy đủ loại dự án này.

---

## Bước 1: Kiểm Tra SDK Đã Cài

Mở Visual Studio 2015 → **Help → About** → kiểm tra có dòng:
```
Windows Phone SDK 8.1
```

Nếu chưa có:
- Chạy **VS 2015 Installer → Modify → chọn "Windows Phone 8.1 Development Tools"**
- Hoặc tải Windows Phone 8.1 SDK tại [SDK Archive](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive/)

---

## Bước 2: Tạo Project Mới

```
File → New → Project
```

Chọn đúng template:

```
Templates
└── Visual C#
    └── Windows Phone Apps
        └── Blank App (Windows Phone Silverlight)    ← CHỌN CÁI NÀY
```

> [!CAUTION]
> Phải chọn **"Windows Phone Silverlight"**, KHÔNG phải "Windows Phone 8.1 (WinRT)".
> Ứng dụng gốc dùng **Silverlight runtime**, không phải WinRT.

| Cài đặt | Giá trị |
|---|---|
| **Name** | `LollipopLockscreen` |
| **Location** | Tùy chọn |
| **Solution name** | `LollipopLockscreen` |
| **Target Windows Phone OS Version** | **Windows Phone 8.1** |

Nhấn **OK**.

---

## Bước 3: Cấu Trúc Thư Mục Dự Án

Sau khi tạo project, tạo cấu trúc thư mục sau (chuột phải → Add → New Folder):

```
LollipopLockscreen/
├── Assets/
│   ├── AppsIcons/          ← Icon shortcuts
│   ├── Battery/            ← Icon pin
│   ├── Fonts/              ← AndroidClock.ttf
│   ├── Tiles/              ← Live tile icons
│   └── Weather/            ← 16 icon thời tiết
├── Extensions/
│   └── LockAppExtension.xml
├── Resources/
│   └── AppResources.resx   ← (có sẵn)
├── Pages/
│   ├── MainPage.xaml        ← Màn hình khóa chính
│   ├── SettingsPage.xaml    ← Trang cài đặt
│   ├── AdvancedSettings.xaml
│   └── About.xaml
├── Controls/
│   └── PatternLockMetroControl.xaml
├── App.xaml
├── LockScreenPage.xaml      ← Entry point
└── WMAppManifest.xml
```

---

## Bước 4: Cài NuGet Packages

**Tools → NuGet Package Manager → Package Manager Console:**

```powershell
# MVVM Light
Install-Package MvvmLightLibs -Version 5.1.1

# JSON parser (cho weather API)
Install-Package Newtonsoft.Json -Version 6.0.8

# Windows Phone Toolkit (ToggleSwitch, Panorama)
Install-Package WPtoolkit -Version 4.2013.08.16

# Lumia Imaging SDK (blur effect)
Install-Package LumiaImagingSDK -Version 2.0.184

# Async/Await support
Install-Package Microsoft.Bcl.Async -Version 1.0.168

# HTTP client (weather API)
Install-Package Microsoft.Net.Http -Version 2.2.29
```

---

## Bước 5: Cấu Hình WMAppManifest.xml

Mở **Properties → WMAppManifest.xml**:

### Tab Capabilities — Tích chọn:

```
☑ ID_CAP_NETWORKING
☑ ID_CAP_MEDIALIB_AUDIO
☑ ID_CAP_MEDIALIB_PLAYBACK
☑ ID_CAP_SENSORS
☑ ID_CAP_WEBBROWSERCOMPONENT
☑ ID_CAP_SHELL_DEVICE_LOCK_UI_API   ← ⚡ QUAN TRỌNG NHẤT
☑ ID_CAP_IDENTITY_DEVICE
☑ ID_CAP_IDENTITY_USER
☑ ID_CAP_LOCATION
☑ ID_CAP_MAP
```

### Tab Application UI:
- **Navigation Page**: `LockScreenPage.xaml`

### Thêm Extensions (edit XML):

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

### Tạo Extensions/LockAppExtension.xml:

```xml
<?xml version="1.0"?>
<x:Extension xmlns:x="urn:LockApp">
  <AppID>App</AppID>
</x:Extension>
```

> Set **Build Action = Content**, **Copy to Output = Copy if newer**.

---

## Bước 6: LockScreenPage.xaml (Entry Point)

```xml
<phone:PhoneApplicationPage
    x:Class="LollipopLockscreen.LockScreenPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:phone="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone"
    xmlns:shell="clr-namespace:Microsoft.Phone.Shell;assembly=Microsoft.Phone"
    SupportedOrientations="Portrait"
    shell:SystemTray.IsVisible="False">
    <Grid Background="Black"/>
</phone:PhoneApplicationPage>
```

**Code-behind:**

```csharp
protected override void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    NavigationService.Navigate(new Uri("/Pages/MainPage.xaml", UriKind.Relative));
}
```

---

## Bước 7: MainPage.xaml — Màn Hình Khóa Chính

**Pages → Add → New Item → Windows Phone Portrait Page → `MainPage.xaml`**

### Cấu trúc XAML cốt lõi:

```xml
<phone:PhoneApplicationPage
    x:Class="LollipopLockscreen.Pages.MainPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:phone="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone"
    xmlns:shell="clr-namespace:Microsoft.Phone.Shell;assembly=Microsoft.Phone"
    xmlns:toolkit="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone.Controls.Toolkit"
    SupportedOrientations="Portrait" Orientation="Portrait"
    shell:SystemTray.BackgroundColor="Black"
    shell:SystemTray.Opacity="0.5"
    BackKeyPress="PhoneApplicationPage_BackKeyPress"
    Loaded="PhoneApplicationPage_Loaded">

    <phone:PhoneApplicationPage.Resources>
        <!-- Battery flash -->
        <Storyboard x:Name="FlashBattery" AutoReverse="True" RepeatBehavior="Forever">
            <DoubleAnimationUsingKeyFrames
                Storyboard.TargetProperty="(UIElement.Opacity)"
                Storyboard.TargetName="BatteryLowIcon">
                <EasingDoubleKeyFrame KeyTime="0" Value="1"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.40" Value="0"/>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>

        <!-- 3D zoom animations -->
        <Storyboard x:Key="TimeAnim">
            <DoubleAnimationUsingKeyFrames
                Storyboard.TargetProperty="(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)"
                Storyboard.TargetName="HourText">
                <DiscreteDoubleKeyFrame KeyTime="0" Value="500"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.500" Value="0">
                    <EasingDoubleKeyFrame.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </EasingDoubleKeyFrame.EasingFunction>
                </EasingDoubleKeyFrame>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>

        <Storyboard x:Key="DateAnim">
            <DoubleAnimationUsingKeyFrames
                Storyboard.TargetProperty="(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)"
                Storyboard.TargetName="DatePanel">
                <DiscreteDoubleKeyFrame KeyTime="0" Value="650"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.500" Value="0">
                    <EasingDoubleKeyFrame.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </EasingDoubleKeyFrame.EasingFunction>
                </EasingDoubleKeyFrame>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>

        <Storyboard x:Key="PassAnim">
            <DoubleAnimationUsingKeyFrames
                Storyboard.TargetProperty="(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)"
                Storyboard.TargetName="PassGrid">
                <DiscreteDoubleKeyFrame KeyTime="0" Value="400"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.500" Value="0">
                    <EasingDoubleKeyFrame.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </EasingDoubleKeyFrame.EasingFunction>
                </EasingDoubleKeyFrame>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>

        <Storyboard x:Key="PassAnimR">
            <DoubleAnimationUsingKeyFrames
                Storyboard.TargetProperty="(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)"
                Storyboard.TargetName="PassGrid">
                <DiscreteDoubleKeyFrame KeyTime="0" Value="0"/>
                <EasingDoubleKeyFrame KeyTime="0:0:0.500" Value="400">
                    <EasingDoubleKeyFrame.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </EasingDoubleKeyFrame.EasingFunction>
                </EasingDoubleKeyFrame>
            </DoubleAnimationUsingKeyFrames>
        </Storyboard>
    </phone:PhoneApplicationPage.Resources>

    <Grid x:Name="LayoutRoot" Background="Transparent">
        <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name="VisualStateGroup">
                <VisualStateGroup.Transitions>
                    <VisualTransition GeneratedDuration="0:0:0.2">
                        <VisualTransition.GeneratedEasingFunction>
                            <SineEase EasingMode="EaseInOut"/>
                        </VisualTransition.GeneratedEasingFunction>
                    </VisualTransition>
                </VisualStateGroup.Transitions>
                <VisualState x:Name="PassEnter">
                    <Storyboard>
                        <DoubleAnimation Duration="0" To="1"
                            Storyboard.TargetProperty="(UIElement.Opacity)"
                            Storyboard.TargetName="LockScreenPanel"/>
                        <DoubleAnimation Duration="0" To="0"
                            Storyboard.TargetProperty="(UIElement.Opacity)"
                            Storyboard.TargetName="OverlayInformationPanel"/>
                        <DoubleAnimation Duration="0" To="-253"
                            Storyboard.TargetProperty="(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)"
                            Storyboard.TargetName="OverlayInformationPanel"/>
                    </Storyboard>
                </VisualState>
                <VisualState x:Name="PassClose"/>
            </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>

        <!-- LOCK SCREEN PANEL -->
        <Grid x:Name="LockScreenPanel" RenderTransformOrigin="0.5,0.5">
            <Grid.Projection><PlaneProjection/></Grid.Projection>
            <Grid.RenderTransform><CompositeTransform/></Grid.RenderTransform>

            <!-- Background -->
            <Border x:Name="BackgroundImage" CacheMode="BitmapCache">
                <Border.Background>
                    <ImageBrush ImageSource="/Assets/BlurBackground.jpg"/>
                </Border.Background>
            </Border>

            <!-- ⚡ OVERLAY PANEL — vuốt lên để unlock -->
            <Grid x:Name="OverlayInformationPanel"
                  ManipulationStarted="OverlayInformationPanel_ManipulationStarted"
                  ManipulationDelta="OverlayInformationPanel_ManipulationDelta"
                  ManipulationCompleted="OverlayInformationPanel_ManipulationCompleted">
                <Grid.Projection><PlaneProjection/></Grid.Projection>
                <Grid.RenderTransform><CompositeTransform/></Grid.RenderTransform>

                <StackPanel>
                    <!-- Đồng hồ -->
                    <TextBlock x:Name="HourText" Text="12:45"
                        FontFamily="Assets/Fonts/AndroidClock.ttf#AndroidClock"
                        FontSize="140" Foreground="White"
                        TextAlignment="Center" Margin="13,65,0,0">
                        <TextBlock.Projection><PlaneProjection/></TextBlock.Projection>
                    </TextBlock>

                    <!-- Thứ -->
                    <TextBlock x:Name="DayPanel" Text="Monday"
                        FontFamily="Yu Gothic" FontSize="32"
                        TextAlignment="Center" Margin="64,20,65,0">
                        <TextBlock.Projection><PlaneProjection/></TextBlock.Projection>
                    </TextBlock>

                    <!-- Ngày tháng -->
                    <TextBlock x:Name="DatePanel" Text="January 1"
                        FontFamily="Segoe WP SemiLight" FontSize="32"
                        TextAlignment="Center" Margin="44,-12,45,0">
                        <TextBlock.Projection><PlaneProjection/></TextBlock.Projection>
                    </TextBlock>

                    <!-- Panorama: Music ↔ Weather -->
                    <phone:Panorama x:Name="panoramaControl" Height="387">
                        <phone:PanoramaItem Width="480">
                            <StackPanel x:Name="PlayPanel">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <Button Click="PlayPrev" Height="96" Width="96"/>
                                    <Button Click="PlayPause" Height="96" Width="96"/>
                                    <Button Click="PlayNext" Height="96" Width="96"/>
                                </StackPanel>
                                <TextBlock x:Name="SongName" Text="Song"
                                    TextAlignment="Center" FontSize="24"/>
                                <TextBlock x:Name="Artist" Text="Artist"
                                    TextAlignment="Center" FontSize="24"/>
                            </StackPanel>
                        </phone:PanoramaItem>
                        <phone:PanoramaItem Width="480">
                            <Grid>
                                <Image x:Name="WeatherIcon"
                                    Source="/Assets/Weather/cloudy.png" Width="96"/>
                                <TextBlock x:Name="TemperatureText" Text="20" FontSize="80"/>
                                <TextBlock x:Name="CityNameText" Text="City" FontSize="35"/>
                            </Grid>
                        </phone:PanoramaItem>
                    </phone:Panorama>
                </StackPanel>

                <!-- Nút mở khóa -->
                <Image x:Name="UnlockButton" Source="/LockIcon.png"
                    Width="100" Height="100"
                    HorizontalAlignment="Center" VerticalAlignment="Bottom"
                    Tap="UnlockButton_Tap" Margin="0,0,0,4">
                    <Image.Projection><PlaneProjection/></Image.Projection>
                    <Image.RenderTransform><CompositeTransform/></Image.RenderTransform>
                </Image>
            </Grid>
        </Grid>

        <!-- BÀN PHÍM PIN -->
        <Grid x:Name="PassGrid" Visibility="Collapsed">
            <Grid.Projection><PlaneProjection/></Grid.Projection>
            <!-- Thêm 10 nút tròn (0-9) + 4 indicators -->
        </Grid>

        <!-- PATTERN LOCK -->
        <Grid x:Name="PatternGrid" Visibility="Collapsed">
            <Grid.Projection><PlaneProjection/></Grid.Projection>
        </Grid>
    </Grid>
</phone:PhoneApplicationPage>
```

---

## Bước 8: MainPage.xaml.cs — Code-Behind

```csharp
using System;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Phone.Controls;

namespace LollipopLockscreen.Pages
{
    public partial class MainPage : PhoneApplicationPage
    {
        private DispatcherTimer timer;
        private bool bIsPasswordEnabled, bIsPatternOn, bIsAnimOn = true;
        private double yToUnlock = 200;

        public MainPage()
        {
            InitializeComponent();
        }

        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            UpdateTime();

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, a) => UpdateTime();
            timer.Start();

            if (bIsAnimOn)
            {
                ((Storyboard)Resources["TimeAnim"]).Begin();
                ((Storyboard)Resources["DateAnim"]).Begin();
            }
        }

        private void UpdateTime()
        {
            HourText.Text = DateTime.Now.ToString("HH:mm");
            DayPanel.Text = DateTime.Now.ToString("dddd");
            DatePanel.Text = DateTime.Now.ToString("MMMM d, yyyy");
        }

        // ===== SWIPE =====
        private void OverlayInformationPanel_ManipulationStarted(
            object sender, ManipulationStartedEventArgs e) { }

        private void OverlayInformationPanel_ManipulationDelta(
            object sender, ManipulationDeltaEventArgs e)
        {
            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            double newY = t.TranslateY + e.DeltaManipulation.Translation.Y;
            if (newY <= 0) t.TranslateY = newY;
        }

        private void OverlayInformationPanel_ManipulationCompleted(
            object sender, ManipulationCompletedEventArgs e)
        {
            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            if (Math.Abs(t.TranslateY) > yToUnlock)
            {
                VisualStateManager.GoToState(this, "PassEnter", true);
                ShowUnlockMethod();
            }
            else t.TranslateY = 0;
        }

        // ===== UNLOCK =====
        private async void UnlockButton_Tap(object sender, GestureEventArgs e)
        {
            ShowUnlockMethod();
        }

        private void ShowUnlockMethod()
        {
            if (bIsPasswordEnabled)
            {
                PassGrid.Visibility = Visibility.Visible;
                ((Storyboard)Resources["PassAnim"]).Begin();
            }
            else if (bIsPatternOn)
            {
                PatternGrid.Visibility = Visibility.Visible;
            }
        }

        // ===== MUSIC =====
        private void PlayPrev(object sender, RoutedEventArgs e)
            => Microsoft.Xna.Framework.Media.MediaPlayer.MovePrevious();
        private void PlayPause(object sender, RoutedEventArgs e)
        {
            var mp = Microsoft.Xna.Framework.Media.MediaPlayer.State;
            if (mp == Microsoft.Xna.Framework.Media.MediaState.Playing)
                Microsoft.Xna.Framework.Media.MediaPlayer.Pause();
            else
                Microsoft.Xna.Framework.Media.MediaPlayer.Resume();
        }
        private void PlayNext(object sender, RoutedEventArgs e)
            => Microsoft.Xna.Framework.Media.MediaPlayer.MoveNext();

        // ===== SETTINGS =====
        private void LoadSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            if (s.Contains("bIsPasswordEnabled"))
                bIsPasswordEnabled = (bool)s["bIsPasswordEnabled"];
            if (s.Contains("bIsPatternOn"))
                bIsPatternOn = (bool)s["bIsPatternOn"];
            if (s.Contains("bIsAnimOn"))
                bIsAnimOn = (bool)s["bIsAnimOn"];
        }

        private void PhoneApplicationPage_BackKeyPress(
            object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            NavigationService.Navigate(
                new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
        }
    }
}
```

---

## Bước 9: Đăng Ký Lock Screen API

Trong **SettingsPage.xaml.cs**:

```csharp
using Windows.Phone.System.LockScreenExtensibility;

// Đăng ký
private async void RegisterLockscreen()
{
    var result = await LockScreenManager.RequestAccessAsync();
    if (result == LockScreenRequestResult.Granted)
    {
        // Thành công — app giờ là lock screen provider
    }
}

// Kiểm tra
bool isRegistered = LockScreenManager.IsProvidedByCurrentApplication;
```

---

## Bước 10: Copy Assets & Build

1. Copy assets từ gói gốc → project (xem bước 12 bên dưới)
2. **Include In Project** + **Build Action = Content**
3. **Build → Build Solution** (Ctrl+Shift+B)
4. **F5** → chạy trên Emulator hoặc Device

---

## Bước 11: Copy Assets Từ Gói Gốc

```
Gói gốc                              → Project mới
Assets/Fonts/AndroidClock.ttf         → Assets/Fonts/
Assets/Weather/*.png (16 files)       → Assets/Weather/
Assets/Battery/*.png                  → Assets/Battery/
Assets/AppsIcons/*.png                → Assets/AppsIcons/
LockIcon.png                         → (root)
Assets/BlurBackground.jpg            → Assets/
```

---

## ⚠️ Lưu Ý Quan Trọng

> [!WARNING]
> 1. **Windows Phone 8.1 đã ngừng hỗ trợ** — không thể publish lên Store
> 2. **Emulator** cần Hyper-V (hoạt động trên cả Windows 11)
> 3. **Lock Screen API** chỉ hoạt động trên thiết bị thật, không trên emulator
> 4. NuGet packages dùng đúng version tương thích WP8.1 Silverlight

> [!TIP]
> Nếu bạn muốn tạo lock screen tương tác cho **nền tảng hiện đại** (Android/Web/Flutter), tôi có thể giúp tạo bản demo tương đương. Hãy cho biết!
