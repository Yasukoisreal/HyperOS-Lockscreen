# 📘 Windows Phone 8.1 Live Lock Screen: Tài Liệu Kỹ Thuật & Kiến Trúc Nền Tảng Cho Lập Trình Viên

> **Mục đích tài liệu:** Cung cấp đầy đủ kiến trúc hệ thống, cơ chế hoạt động tầng hệ điều hành (OS Internals), và hướng dẫn thực hành từng bước giúp lập trình viên có thể tự thiết kế và xây dựng bất kỳ ứng dụng **Live Lock Screen** nào trên nền tảng **Windows Phone 8.1 Silverlight**.

---

## 1. Bản Chất Kỹ Thuật Của Live Lock Screen

### 1.1 Khái niệm & Bối cảnh
Trước bản cập nhật Windows Phone 8.1, màn hình khóa trên Windows Phone 8.0 chỉ là một giao diện tĩnh: các ứng dụng bên thứ ba chỉ có thể làm hai việc cơ bản:
1. Đặt ảnh nền tĩnh thông qua `LockScreen.SetImageFileUri(...)`.
2. Hiển thị thông báo dạng số hoặc văn bản ngắn trên 5 slot badge biểu tượng của hệ thống.

Từ **Windows Phone 8.1**, Microsoft giới thiệu cơ chế mở rộng **Live Lock Screen (Extensibility Framework)**, cho phép một ứng dụng có thể thay thế toàn bộ giao diện màn hình khóa bằng một trang giao diện XAML tương tác đầy đủ, hỗ trợ hoạt ảnh chuyển động (animations), cử chỉ chạm vuốt (gestures) và nội dung tùy biến thời gian thực.

### 1.2 Bản chất tầng hệ điều hành (OS Architecture)
Live Lock Screen **không thay thế kernel bảo mật** của hệ điều hành. Cơ chế hoạt động thực tế như sau:

* Ứng dụng Live Lock Screen là một **tiến trình Silverlight đặc biệt** chạy trong sandbox của OS Shell.
* Khi người dùng bật sáng màn hình, hệ điều hành sẽ ánh xạ giao diện XAML của ứng dụng lên bề mặt kết xuất của màn hình khóa (**Lock Screen Compositor Surface**).
* Khi người dùng tương tác mở khóa, ứng dụng chỉ làm nhiệm vụ gửi tín hiệu yêu cầu mở khóa (`RequestScreenUnlock`) về lại cho hệ điều hành.

```
┌────────────────────────────────────────────────────────────────────────┐
│                      MÀN HÌNH THIẾT BỊ (DISPLAY)                       │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 1 - Ưu tiên cao nhất] Giao diện Native PIN / Cuộc gọi khẩn cấp   │
│  - Hoàn toàn do hệ điều hành vẽ, ứng dụng không thể can thiệp          │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 2] Live Lock Screen Extensibility App (Silverlight Runtime)      │
│  - Giao diện tùy chỉnh (Đồng hồ, hoạt cảnh, widget, cử chỉ chạm vuốt)  │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 3] OS Lock Screen Host & Watchdog Service                        │
│  - Quản lý trạng thái nguồn, kiểm soát hạn mức RAM và timeout vẽ       │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 4 - Đáy] Start Screen / Ứng dụng đang chạy trước đó              │
└────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Tại sao bắt buộc phải là Silverlight 8.1 (Không dùng WinRT)?
Trên Windows Phone 8.1 tồn tại hai nền tảng runtime:
- **WinRT 8.1 XAML (`Windows.UI.Xaml.*`):** Chỉ hỗ trợ API `Windows.ApplicationModel.LockScreen` để đổi ảnh tĩnh và badge. WinRT **không có** cơ chế cắm ứng dụng tương tác vào màn hình khóa.
- **Silverlight 8.1 (`System.Windows.*`, `Microsoft.Phone.*`):** Hệ điều hành cung cấp điểm nối mở rộng (`LockAppExtension`) gắn trực tiếp vào tiến trình **`AgHost.exe`** (Silverlight Application Host). Vì vậy, **bắt buộc phải sử dụng dự án Windows Phone Silverlight 8.1**.

---

## 2. Chu Trình Hoạt Động Hoàn Chỉnh (Lifecycle Sequence)

Sơ đồ trình tự mô tả vòng đời của một ứng dụng Live Lock Screen từ khi bật nguồn đến khi mở khóa:

```
Người Dùng               OS Shell (Power Mgr)         Ứng Dụng (AgHost)       SystemProtection
    │                              │                          │                      │
    │── [1] Bấm Nút Nguồn ────────>│                          │                      │
    │                              │── [2] Kích hoạt app ────>│                      │
    │                              │   (Resume/Wake-up)       │                      │
    │                              │                          │── [3] Kiểm tra ─────>│
    │                              │                          │   ScreenLocked       │
    │                              │                          │<── [4] Trả về true ──│
    │                              │                          │                      │
    │                              │                          │── [5] Navigate tới ─>│
    │                              │                          │   trang Lock UI      │
    │                              │                          │                      │
    │                              │<── [6] Vẽ Frame 0 ───────│ (Phải < 500ms để     │
    │<── [7] Màn hình phát sáng ───│    (Hoàn tất render)     │  tránh Fallback)     │
    │    (Thấy giao diện khóa)     │                          │                      │
    │                              │                          │                      │
    │── [8] Vuốt ngón tay mở khóa ───────────────────────────>│                      │
    │   (Xử lý Manipulation)       │                          │                      │
    │                              │                          │                      │
    │── [9] Đạt ngưỡng mở khóa ──────────────────────────────>│                      │
    │                              │                          │── [10] Chạy Exit ────│
    │                              │                          │    Animation         │
    │                              │                          │                      │
    │                              │                          │── [11] Yêu cầu ─────>│
    │                              │<── [12] Bàn giao quyền ─────────────────────────│
    │                              │    RequestScreenUnlock                          │
    │                              │                          │                      │
    │                     [Máy CÓ cài PIN OS?]                │                      │
    │                         ┌────┴────┐                     │                      │
    │                       CÓ          KHÔNG                 │                      │
    │                       │             │                   │                      │
    │<── [13a] Hiện ────────│             │                   │                      │
    │    Bàn phím PIN máy   │             │                   │                      │
    │    (Native OS PIN)    │             │                   │                      │
    │                       │             │                   │                      │
    │<──────────────────────┴─────────────┴─── [13b] Mở thẳng Start Screen ──────────│
```

---

## 3. Khung Đăng Ký Mở Rộng Hệ Thống (Configuration & Descriptors)

Để ứng dụng xuất hiện trong danh sách lựa chọn tại mục **Cài đặt > Màn hình khóa** (*Settings > Lock Screen*) của điện thoại, ứng dụng cần 2 phần cấu hình bắt buộc.

### 3.1 Cấu hình `Properties/WMAppManifest.xml`

Khai báo quyền đặc quyền (`Capability`) và cặp `Extension` định danh:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Deployment xmlns="http://schemas.microsoft.com/windowsphone/2014/deployment" AppPlatformVersion="8.1">
  <App xmlns="" ProductID="{YOUR-APP-GUID-HERE}" 
       Title="Custom Lock Screen" 
       RuntimeType="Silverlight" 
       Version="1.0.0.0" 
       Genre="apps.normal" 
       Author="Developer" 
       Description="Live Lock Screen Application" 
       Publisher="PublisherName" 
       PublisherID="{YOUR-PUBLISHER-GUID}">

    <IconPath IsRelative="true" IsResource="false">Assets\ApplicationIcon.png</IconPath>

    <Capabilities>
      <!-- QUAN TRỌNG: Quyền tương tác với API bảo vệ và mở khóa màn hình -->
      <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
      <Capability Name="ID_CAP_NETWORKING" />
    </Capabilities>

    <!-- Điểm vào mặc định: Bắt buộc trỏ tới Router trung gian -->
    <Tasks>
      <DefaultTask Name="_default" NavigationPage="LockScreenRouter.xaml" ActivationPolicy="Resume" />
    </Tasks>

    <!-- Đăng ký các Extension Consumer ID với hệ điều hành -->
    <Extensions>
      <!-- Extension 1: Định danh ứng dụng là Live Lock Screen Provider -->
      <Extension ExtensionName="LockScreen_Application"
                 ConsumerID="{CD4601F6-351B-43C7-9087-6B12BD98ED63}"
                 TaskID="_default"
                 ExtraFile="Extensions\\LockAppExtension.xml" />

      <!-- Extension 2: Cho phép ứng dụng xử lý dữ liệu nền màn hình khóa -->
      <Extension ExtensionName="LockScreen_Background"
                 ConsumerID="{111DFF24-AA15-4A96-8006-2BFF8122084F}"
                 TaskID="_default" />
    </Extensions>

  </App>
</Deployment>
```

#### Giải mã các Consumer ID chuẩn của Microsoft:
* `{CD4601F6-351B-43C7-9087-6B12BD98ED63}`: GUID nội bộ của OS Shell Windows Phone 8.1. Hệ điều hành quét GUID này để biết ứng dụng có khả năng vẽ giao diện khóa.
* `{111DFF24-AA15-4A96-8006-2BFF8122084F}`: GUID cho phép tích hợp ảnh nền hệ thống.

### 3.2 Tệp tin chỉ thị `Extensions\LockAppExtension.xml`

Tạo thư mục `Extensions` ở thư mục gốc của project, bên trong tạo file `LockAppExtension.xml`:

```xml
<?xml version="1.0"?>
<x:Extension xmlns:x="urn:LockApp">
  <AppID>App</AppID>
</x:Extension>
```

> **Thiết lập thuộc tính file trong Visual Studio:**
> - **Build Action:** `Content`
> - **Copy to Output Directory:** `Copy if newer`

---

## 4. Kiến Trúc Điều Hướng Hai Vai Trò ("Dual-Role" Routing Pattern)

Một ứng dụng Live Lock Screen luôn có 2 vai trò hoàn toàn độc lập:
1. **Chế độ Mở (Unlocked Context):** Người dùng bấm mở icon app từ màn hình Start để chỉnh sửa cài đặt, theme, cấu hình.
2. **Chế độ Khóa (Locked Context):** Người dùng bấm nút nguồn bật máy, ứng dụng cần hiển thị màn hình khóa.

### Triển khai `LockScreenRouter.xaml.cs`
Không bao giờ trỏ `DefaultTask NavigationPage` trực tiếp vào trang khóa hoặc trang cài đặt. Phải sử dụng một trang rẽ nhánh:

```csharp
using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.Phone.System;

namespace CustomLockScreen
{
    public partial class LockScreenRouter : PhoneApplicationPage
    {
        public LockScreenRouter()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                // Kiểm tra trạng thái khóa phần cứng của hệ thống
                if (SystemProtection.ScreenLocked)
                {
                    // Thiết bị đang khóa -> Điều hướng ngay tới giao diện khóa
                    NavigationService.Navigate(new Uri("/Pages/LockViewPage.xaml", UriKind.Relative));
                }
                else
                {
                    // Thiết bị đã mở -> Điều hướng tới giao diện cài đặt/tùy biến
                    NavigationService.Navigate(new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
                }
            }
            catch
            {
                // Fallback an toàn
                NavigationService.Navigate(new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
            }
        }
    }
}
```

> **API Cốt Lõi:** `Windows.Phone.System.SystemProtection.ScreenLocked` trả về giá trị `true` khi màn hình đang ở trạng thái khóa bởi hệ điều hành.

---

## 5. Cơ Chế Tương Tác, Cử Chỉ & Mở Khóa (Gestures & Unlock Flow)

Khi xây dựng trang hiển thị màn hình khóa (`LockViewPage.xaml`), có 3 nguyên tắc bảo mật và tương tác bắt buộc:

### 5.1 Chặn phím cứng Back (Hardware Back Button)
Nếu không chặn phím Back, người dùng chỉ cần nhấn nút Back trên điện thoại là trang khóa sẽ lùi lại hoặc đóng ứng dụng, làm lộ màn hình Start mà không cần vuốt.

```csharp
protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
{
    base.OnBackKeyPress(e);
    // BẮT BUỘC: Luôn hủy sự kiện nút Back khi đang ở màn hình khóa
    e.Cancel = true;
}
```

### 5.2 Xử lý cử chỉ vuốt (Gesture Physics & Snap-back)
Giao diện khóa hiện đại thường hỗ trợ cử chỉ vuốt lên theo tay người dùng. Cần xử lý các sự kiện `Manipulation`:

```csharp
private double dragDeltaY = 0;
private const double UNLOCK_THRESHOLD = -150.0; // Kéo lên quá 150px sẽ mở khóa

private void LayoutRoot_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
{
    dragDeltaY += e.DeltaManipulation.Translation.Y;
    
    // Giới hạn: chỉ cho phép vuốt lên, không cho kéo tụt xuống dưới
    if (dragDeltaY > 0) dragDeltaY = 0;

    // Dịch chuyển panel theo ngón tay
    ContentTransform.TranslateY = dragDeltaY;

    // Giảm độ mờ dần theo quãng đường kéo
    double opacity = 1.0 - Math.Min(1.0, Math.Abs(dragDeltaY) / 400.0);
    ContentPanel.Opacity = opacity;
}

private void LayoutRoot_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
{
    // Mở khóa nếu: kéo vượt ngưỡng HOẶC vuốt với vận tốc nhanh (flick gesture)
    if (dragDeltaY < UNLOCK_THRESHOLD || e.FinalVelocities.LinearVelocity.Y < -800)
    {
        ExecuteUnlockSequence();
    }
    else
    {
        // Chưa đủ điều kiện mở khóa -> Chạy hoạt ảnh Snap-back đàn hồi về vị trí cũ
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

### 5.3 Gọi lệnh mở khóa hệ thống: `SystemProtection.RequestScreenUnlock()`

Khi hoàn thành hiệu ứng mở khóa, gọi API của hệ điều hành:

```csharp
private void ExecuteUnlockSequence()
{
    // Có thể kích hoạt hoạt cảnh trượt hết màn hình trước
    TriggerUnlockExitAnimation(() =>
    {
        try
        {
            if (SystemProtection.ScreenLocked)
            {
                // BÀN GIAO QUYỀN MỞ KHÓA CHO HỆ ĐIỀU HÀNH
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

### 5.4 Cách hệ điều hành phản hồi lệnh mở khóa
* **Nếu máy KHÔNG có mật khẩu:** Hệ điều hành lập tức hạ lớp màn hình khóa, đưa người dùng vào Start Screen hoặc ứng dụng đang chạy trước đó.
* **Nếu máy CÓ mật khẩu (PIN 4-6 số cấu hình trong cài đặt hệ thống):** Hệ điều hành **ngay lập tức đẩy bàn phím số Native PIN của Windows Phone lên trên**. Người dùng phải nhập đúng PIN của máy mới vào được bên trong.
* **Ý nghĩa:** Ứng dụng của bạn không bao giờ có thể gây nguy cơ bảo mật hay vượt mặt mã PIN của điện thoại.

---

## 6. Tối Ưu Hiệu Năng & Bài Toán "Resuming..." (Thiết Bị 512MB RAM)

### 6.1 Nguyên nhân của hiện tượng "Resuming..." trên Windows Phone 8.1
Ứng dụng Live Lock Screen chính thức của Microsoft từng bị người dùng phàn nàn vì xuất hiện màn hình đen/chữ *"Resuming..."* mất 1-2 giây mỗi khi bấm nút nguồn. Các nguyên nhân kỹ thuật gồm:

1. **Hạn mức RAM khắt khe trên máy 512MB:** Các dòng máy như Lumia 520, 525, 530, 630 chiếm lượng lớn thị phần nhưng chỉ có 512MB RAM. Hạn mức RAM khả dụng cho ứng dụng màn hình khóa rất nhỏ.
2. **Giải mã ảnh (Image Decoding Heap):** Mỗi ảnh nền chuẩn uncompressed chiếm:
   $$\text{RAM} = \text{Chiều rộng} \times \text{Chiều cao} \times 4 \text{ bytes (ARGB)}$$
   Một bức ảnh $1080 \times 1920$ tiêu tốn khoảng **8.3 MB** bộ nhớ RAM thuần cho mỗi instance. Nạp nhiều ảnh cùng lúc sẽ dẫn tới tràn bộ nhớ (Out Of Memory).
3. **OS Watchdog Timeout (500ms):** Hệ điều hành giám sát chặt chẽ: nếu ứng dụng khóa không vẽ xong khung hình đầu tiên (Frame 0) trong vòng **500ms - 1000ms**, hệ điều hành sẽ coi ứng dụng bị treo và tự động hạ cấp (fallback) về màn hình khóa tĩnh mặc định của hệ thống.

### 6.2 Các kỹ thuật tối ưu hóa bắt buộc

#### A. Dọn dẹp bộ nhớ triệt để khi rời trang (`OnNavigatedFrom`)
Mỗi khi màn hình tắt hoặc khi người dùng mở khóa thành công, phải hủy hoàn toàn tham chiếu ảnh để bộ gom rác (Garbage Collector) thu hồi bộ nhớ đồ họa:

```csharp
protected override void OnNavigatedFrom(NavigationEventArgs e)
{
    base.OnNavigatedFrom(e);

    // Hủy liên kết ảnh để giải phóng RAM GPU/Bitmap lập tức
    if (BackgroundBrush != null) BackgroundBrush.ImageSource = null;

    // Dừng tất cả Storyboard lặp vô hạn và DispatcherTimer
    if (clockTimer != null && clockTimer.IsEnabled) clockTimer.Stop();

    // Thu gom rác chủ động
    GC.Collect();
}
```

#### B. Khử nhòe đồ họa bằng Pixel Snapping (Làm tròn tọa độ số nguyên)
Trên Silverlight, nếu tọa độ `Margin` hoặc `Canvas` là số thực dấu phẩy động (float/double), hệ thống sẽ nội suy sub-pixel gây nhòe mờ văn bản và làm GPU tốn tài nguyên chống răng cưa (anti-aliasing). Luôn ép kiểu về số nguyên:

```csharp
// Luôn ép kiểu int cho Margin/Canvas Coordinates
int roundedX = (int)Math.Round(calculatedX);
int roundedY = (int)Math.Round(calculatedY);
MyClockElement.Margin = new Thickness(roundedX, roundedY, 0, 0);
```

#### C. Giữ cây giao diện (Visual Tree) tinh gọn
- Không lồng ghép quá nhiều cấp layout (`Grid` lồng trong `StackPanel` lồng trong `Border`).
- Hạn chế sử dụng DataBinding phức tạp ở trang khóa; thay vào đó, gán trực tiếp thuộc tính qua code-behind (`ClockText.Text = DateTime.Now.ToString("HH:mm")`) để đạt tốc độ render tối đa.

---

## 7. Hướng Dẫn Từng Bước Tạo Dự Án Mới Từ Đầu (Step-by-Step Tutorial)

Dưới đây là quy trình hoàn chỉnh để bạn tạo ra một dự án Live Lock Screen của riêng mình:

### Bước 1: Khởi tạo Project
1. Mở **Visual Studio 2015** (hoặc 2013).
2. Chọn `File > New > Project`.
3. Chọn template: `Visual C# > Windows Phone Apps > Blank App (Windows Phone Silverlight)`.
4. Đặt tên project (ví dụ: `MyLiveLockScreen`).
5. Chọn Target Version: **Windows Phone 8.1**.

### Bước 2: Khai báo Manifest & Extension
1. Mở file `Properties\WMAppManifest.xml` bằng chế độ mã nguồn (**View Code**).
2. Thêm capability trong thẻ `<Capabilities>`:
   ```xml
   <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
   ```
3. Đổi thuộc tính `NavigationPage` trong thẻ `<DefaultTask>` thành:
   ```xml
   NavigationPage="LockRouter.xaml"
   ```
4. Thêm thẻ `<Extensions>` ngay sau thẻ `</Tokens>`:
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

### Bước 3: Tạo tệp tin `LockAppExtension.xml`
1. Nhấp chuột phải vào Project, chọn `Add > New Folder`, đặt tên thư mục là `Extensions`.
2. Nhấp chuột phải vào thư mục `Extensions`, chọn `Add > New Item > XML File`, đặt tên là `LockAppExtension.xml`.
3. Nhập nội dung:
   ```xml
   <?xml version="1.0"?>
   <x:Extension xmlns:x="urn:LockApp">
     <AppID>App</AppID>
   </x:Extension>
   ```
4. Trong cửa sổ **Properties** của file `LockAppExtension.xml`:
   - Đặt `Build Action` = **Content**.
   - Đặt `Copy to Output Directory` = **Copy if newer**.

### Bước 4: Tạo trang điều hướng `LockRouter.xaml`
1. Tạo trang mới: `Add > New Item > Windows Phone Portrait Page`, đặt tên `LockRouter.xaml`.
2. Mở file `LockRouter.xaml.cs`, thay thế nội dung phương thức `OnNavigatedTo`:
   ```csharp
   protected override void OnNavigatedTo(NavigationEventArgs e)
   {
       base.OnNavigatedTo(e);
       if (Windows.Phone.System.SystemProtection.ScreenLocked)
       {
           NavigationService.Navigate(new Uri("/LockView.xaml", UriKind.Relative));
       }
       else
       {
           NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
       }
   }
   ```

### Bước 5: Tạo trang màn hình khóa `LockView.xaml`
1. Tạo trang mới: `Add > New Item > Windows Phone Portrait Page`, đặt tên `LockView.xaml`.
2. Xây dựng giao diện XAML cơ bản:
   ```xml
   <phone:PhoneApplicationPage
       x:Class="MyLiveLockScreen.LockView"
       xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:phone="clr-namespace:Microsoft.Phone.Controls;assembly=Microsoft.Phone"
       xmlns:shell="clr-namespace:Microsoft.Phone.Shell;assembly=Microsoft.Phone"
       SupportedOrientations="Portrait" Orientation="Portrait"
       shell:SystemTray.IsVisible="False">

       <Grid x:Name="LayoutRoot" Background="Black"
             ManipulationDelta="LayoutRoot_ManipulationDelta"
             ManipulationCompleted="LayoutRoot_ManipulationCompleted">
           
           <StackPanel x:Name="ClockPanel" VerticalAlignment="Center" HorizontalAlignment="Center">
               <StackPanel.RenderTransform>
                   <CompositeTransform x:Name="ClockTransform" />
               </StackPanel.RenderTransform>
               
               <TextBlock x:Name="TimeText" Text="12:00" FontSize="80" 
                          HorizontalAlignment="Center" Foreground="White" />
               <TextBlock x:Name="DateText" Text="Monday, January 1" FontSize="20" 
                          HorizontalAlignment="Center" Foreground="#AAFFFFFF" />
               <TextBlock Text="▲ Swipe up to unlock" FontSize="16" Margin="0,50,0,0"
                          HorizontalAlignment="Center" Foreground="#66FFFFFF" />
           </StackPanel>
       </Grid>
   </phone:PhoneApplicationPage>
   ```
3. Trong `LockView.xaml.cs`:
   - Bắt sự kiện `OnBackKeyPress` và đặt `e.Cancel = true;`.
   - Sử dụng `DispatcherTimer` để cập nhật đồng hồ mỗi giây.
   - Viết sự kiện `ManipulationDelta` và `ManipulationCompleted` để khi vuốt lên sẽ gọi `Windows.Phone.System.SystemProtection.RequestScreenUnlock()`.

---

## 8. Bảng Tra Cứu API & Những Lưu Ý Cho Nhà Phát Triển (Cheat Sheet)

| Tác Vụ | ❌ API Bị Cấm (Gây Lỗi Build / Không Chạy) | ✅ API Chuẩn Trên WP8.1 Silverlight |
|---|---|---|
| **Cấu trúc XAML** | `Windows.UI.Xaml.*` (WinRT) | `System.Windows.*` |
| **Lưu tập tin** | `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| **Lưu cấu hình cài đặt** | `Windows.Storage.ApplicationData` | `System.IO.IsolatedStorage.IsolatedStorageSettings` |
| **Chọn ảnh từ máy** | `Windows.Storage.Pickers.FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| **Nút Back phần cứng** | `HardwareButtons.BackPressed` | `PhoneApplicationPage.BackKeyPress` event |
| **Chuyển trang** | `Frame.Navigate(...)` | `NavigationService.Navigate(...)` |
| **Chuyển luồng UI** | `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke(...)` |
| **Hoạt ảnh** | CSS / XAML Transitions phức tạp | `Storyboard`, `DoubleAnimation` với Easing Functions |
| **Lệnh mở khóa** | Tự thoát app (`Application.Current.Exit()`) | `SystemProtection.RequestScreenUnlock()` |

---

## 9. Tổng Kết

Cơ chế **Live Lock Screen** trên Windows Phone 8.1 là một thiết kế thông minh kết hợp giữa:
1. **Tính an toàn tuyệt đối:** Giao diện tùy biến chạy trên tầng ứng dụng, lớp mật khẩu gốc của OS vẫn bảo vệ phía dưới.
2. **Khả năng tương tác cao:** Hỗ trợ toàn bộ công nghệ XAML, cử chỉ cảm ứng, hoạt ảnh Storyboard của Silverlight.
3. **Hiệu năng tức thì nếu tối ưu đúng cách:** Bằng việc giải phóng bộ nhớ ảnh khi không hiển thị, làm tròn pixel và giữ Visual Tree tinh gọn, bất kỳ lập trình viên nào cũng có thể tạo nên những trải nghiệm mở khóa mượt mà, phản hồi ngay lập tức trên mọi thiết bị Windows Phone 8.1.
