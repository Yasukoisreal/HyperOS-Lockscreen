# 📘 Windows Phone 8.1 Live Lock Screen: Tài Liệu Kỹ Thuật & Kiến Trúc Chuyên Sâu Tầng Hệ Điều Hành

Tài liệu kỹ thuật toàn diện và chuyên sâu nhất dành cho lập trình viên giải thích bản chất, cơ chế hoạt động tầng hệ điều hành (OS Internals), mô hình luồng, bảo mật và kỹ thuật tối ưu hóa khi xây dựng ứng dụng **Live Lock Screen** trên nền tảng **Windows Phone 8.1 Silverlight**.

---

## 1. Bối Cảnh & Bản Chất Của Live Lock Screen

### 1.1 Lịch sử hình thành
Tại hội nghị **Microsoft Build 2014**, Joe Belfiore đã công bố tính năng **Live Lock Screen** cho bản cập nhật Windows Phone 8.1. Trước đó trên Windows Phone 8.0, màn hình khóa chỉ là một giao diện tĩnh: nhà phát triển chỉ có thể đăng ký làm Lock Screen Provider để thay đổi ảnh nền tĩnh (`LockScreen.SetImageFileUri`) và gửi 5 biểu tượng badge thông báo nhỏ.

Live Lock Screen trên WP8.1 ra đời nhằm mở khóa khả năng tùy biến chuyển động (interactive animations), kiểu hiển thị đồng hồ phong phú và hỗ trợ tương tác cảm ứng đa điểm trực tiếp ngay khi bật màn hình.

### 1.2 Bản chất kỹ thuật thực sự tầng OS (Architecture Truth)
Một câu hỏi nền tảng mà mọi lập trình viên đều cần hiểu rõ: **Live Lock Screen có thay thế kernel lock screen bảo mật của Windows Phone không?**

> **CÂU TRẢ LỜI LÀ: HOÀN TOÀN KHÔNG.**
>
> Ứng dụng Live Lock Screen thực chất là một **ứng dụng Silverlight đặc biệt** chạy trong một sandbox chuyên dụng được hệ điều hành Windows Phone 8.1 cấp quyền hiển thị ngay trên bề mặt màn hình khóa (**Lock Screen Compositor Surface**). Hệ điều hành vẫn kiểm soát toàn bộ phần cứng, bảo mật hạt nhân (Secure Kernel), mã PIN gốc của máy và trạng thái nguồn.

```
┌────────────────────────────────────────────────────────────────────────┐
│                      MÀN HÌNH HIỂN THỊ (DISPLAY)                       │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 1 - Ưu tiên cao nhất] Native OS PIN Keypad / Emergency Call      │
│  - Do chính Windows Phone OS Shell dựng, app hoàn toàn không can thiệp │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 2] Live Lock Screen Extensibility App (Silverlight AgHost)       │
│  - Đồng hồ HyperOS, hoạt ảnh Storyboard, hiệu ứng 2.5D Depth, v.v.     │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 3] OS Lock Screen Host & System Protection Watchdog              │
│  - Quản lý vòng đời, kiểm soát RAM, giám sát timeout 500ms             │
├────────────────────────────────────────────────────────────────────────┤
│  [Lớp 4 - Đáy] Start Screen / Ứng dụng đang chạy ngầm phía sau         │
└────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Vì sao WinRT 8.1 KHÔNG THỂ làm Live Lock Screen?
Windows Phone 8.1 hỗ trợ hai nền tảng runtime song song: **Silverlight 8.1** (`Microsoft.Phone.*`) và **WinRT 8.1** (`Windows.UI.Xaml.*`).
- Trên WinRT 8.1, Microsoft chỉ cung cấp namespace `Windows.ApplicationModel.LockScreen`, vốn chỉ cho phép thay đổi ảnh tĩnh hoặc hiển thị văn bản badge.
- Kiến trúc mở rộng Live Lock Screen (`LockAppExtension`) được thiết kế cắm trực tiếp vào **Silverlight Application Host (`AgHost.exe`)**. Do đó, **bắt buộc phải sử dụng Windows Phone Silverlight 8.1** mới có thể tạo ra Live Lock Screen tương tác chuyển động.

---

## 2. Toàn Bộ Chu Trình Hoạt Động (End-to-End Sequence Diagram)

Sơ đồ trình tự mô tả chính xác những gì diễn ra từ lúc người dùng bấm nút nguồn đến khi vào được màn hình chính:

```
User (Người dùng)       Power Mgr / OS Shell       AgHost (App)           SystemProtection
     │                           │                      │                         │
     │── [1] Bấm Nút Nguồn ─────>│                      │                         │
     │                           │── [2] Kích hoạt app >│                         │
     │                           │   (Warm Resume)      │                         │
     │                           │                      │── [3] Kiểm tra ────────>│
     │                           │                      │   ScreenLocked          │
     │                           │                      │<── [4] Trả về true ─────│
     │                           │                      │                         │
     │                           │                      │── [5] Navigate tới ────>│
     │                           │                      │   LockScreen.xaml       │
     │                           │                      │                         │
     │                           │<── [6] Vẽ Frame 0 ───│ (Phải < 500ms để        │
     │<── [7] Màn hình sáng ─────│                      │  tránh Fallback)        │
     │    (Thấy đồng hồ HyperOS) │                      │                         │
     │                           │                      │                         │
     │── [8] Vuốt ngón tay lên ────────────────────────>│                         │
     │   (ManipulationDelta)     │                      │ (Đồng bộ TranslateY     │
     │                           │                      │  cho cả 2 lớp Depth)    │
     │                           │                      │                         │
     │── [9] Thả tay (Vượt ngưỡng unlock) ─────────────>│                         │
     │                           │                      │── [10] Chạy UnlockAnim ─│
     │                           │                      │    (Trượt mượt lên -800)│
     │                           │                      │                         │
     │                           │                      │── [11] Gọi Unlock ─────>│
     │                           │<── [12] Báo OS ────────────────────────────────│
     │                           │    RequestScreenUnlock                         │
     │                           │                      │                         │
     │                   [Có mã PIN máy?]               │                         │
     │                      ┌────┴────┐                 │                         │
     │                    CÓ          KHÔNG             │                         │
     │                    │             │               │                         │
     │<── [13a] Hiện ─────│             │               │                         │
     │    Native PIN      │             │               │                         │
     │    Bàn phím OS     │             │               │                         │
     │                    │             │               │                         │
     │<───────────────────┴─────────────┴─ [13b] Mở thẳng Start Screen ───────────│
```

---

## 3. Cơ Chế Đăng Ký Mở Rộng Hệ Thống (Extensibility Architecture)

Để biến một ứng dụng thông thường thành Live Lock Screen được hệ điều hành công nhận, ứng dụng phải khai báo đúng bộ descriptor chuẩn mực.

### 3.1 Cấu hình `Properties/WMAppManifest.xml`

Ứng dụng bắt buộc phải đăng ký Capability chuyên biệt và Extension Consumer ID:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Deployment xmlns="http://schemas.microsoft.com/windowsphone/2014/deployment" AppPlatformVersion="8.1">
  <App xmlns="" ProductID="{d07b4623-ef8d-425d-9d79-23e30ac7c3b1}" 
       Title="HyperOS Lockscreen" 
       RuntimeType="Silverlight" 
       Version="1.0.0.0" 
       Genre="apps.normal" 
       Author="HyperOS" 
       Description="HyperOS Live Lock Screen" 
       Publisher="HyperOS" 
       PublisherID="{90ac6b85-475a-4dfd-8bf7-00ba0e3700b6}">

    <IconPath IsRelative="true" IsResource="false">Assets\ApplicationIcon.png</IconPath>

    <!-- 1. Capabilities bắt buộc -->
    <Capabilities>
      <Capability Name="ID_CAP_NETWORKING" />
      <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" /> <!-- QUAN TRỌNG NHẤT -->
      <Capability Name="ID_CAP_IDENTITY_DEVICE" />
    </Capabilities>

    <!-- 2. Điểm vào Router mặc định -->
    <Tasks>
      <DefaultTask Name="_default" NavigationPage="LockScreenPage.xaml" ActivationPolicy="Resume" />
    </Tasks>

    <!-- 3. Đăng ký Extension Consumer ID -->
    <Extensions>
      <!-- LockScreen Application: Định danh ứng dụng giao diện khóa -->
      <Extension ExtensionName="LockScreen_Application"
                 ConsumerID="{CD4601F6-351B-43C7-9087-6B12BD98ED63}"
                 TaskID="_default"
                 ExtraFile="Extensions\\LockAppExtension.xml" />

      <!-- LockScreen Background: Cho phép cung cấp luồng ảnh nền -->
      <Extension ExtensionName="LockScreen_Background"
                 ConsumerID="{111DFF24-AA15-4A96-8006-2BFF8122084F}"
                 TaskID="_default" />
    </Extensions>

  </App>
</Deployment>
```

#### Giải mã các GUID định danh:
| GUID | Tên Định Danh | Ý Nghĩa Kỹ Thuật |
|---|---|---|
| `{CD4601F6-351B-43C7-9087-6B12BD98ED63}` | `LockScreen_Application` | GUID nội bộ của OS Shell WP8.1. Khi người dùng vào *Cài đặt > Màn hình khóa*, hệ điều hành sẽ quét Registry tìm tất cả ứng dụng có ConsumerID này để hiển thị trong mục "Ứng dụng hiển thị màn hình khóa". |
| `{111DFF24-AA15-4A96-8006-2BFF8122084F}` | `LockScreen_Background` | Cho phép ứng dụng đóng vai trò nhà cung cấp hình nền nền (Background Provider). |
| `ID_CAP_SHELL_DEVICE_LOCK_UI_API` | Lock UI Capability | Quyền đặc quyền cho phép ứng dụng Silverlight gọi các hàm tương tác màn hình khóa trong namespace `Windows.Phone.System.SystemProtection`. |

### 3.2 Tệp tin chỉ định giao thức `Extensions\LockAppExtension.xml`

Tệp tin này phải đặt tại đường dẫn `Extensions\LockAppExtension.xml`, thuộc tính tệp là `Content` và `Copy to Output Directory = Copy if newer`:

```xml
<?xml version="1.0"?>
<x:Extension xmlns:x="urn:LockApp">
  <AppID>App</AppID>
</x:Extension>
```

Tệp XML này đóng vai trò xác thực hợp đồng liên kết (Contract Descriptor) giữa OS Shell và ứng dụng theo không gian tên `urn:LockApp`.

---

## 4. Mô Hình "Dual-Role" & Cửa Ngõ Điều Hướng (Routing Gateway)

Ứng dụng Live Lock Screen luôn tồn tại dưới dạng **Dual-Role (Ứng dụng 2 vai trò)**:
1. **Trạng thái Mở (Unlocked Context):** Người dùng bấm vào biểu tượng ứng dụng ngoài màn hình chính để tùy chỉnh giao diện (phải hiển thị `MySetsPage.xaml` hoặc `EditorPage.xaml`).
2. **Trạng thái Khóa (Locked Context):** Người dùng bấm nút nguồn bật sáng màn hình (phải hiển thị `LockScreen.xaml`).

### 4.1 Bộ định tuyến: `LockScreenPage.xaml.cs`
Điểm vào `DefaultTask NavigationPage` **không bao giờ được trỏ trực tiếp** vào màn hình khóa hay màn hình cài đặt, mà phải thông qua router trung gian:

```csharp
using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.Phone.System;

namespace HyperOS
{
    public partial class LockScreenPage : PhoneApplicationPage
    {
        public LockScreenPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                // Kiểm tra trạng thái khóa phần cứng của OS
                if (SystemProtection.ScreenLocked)
                {
                    // Thiết bị ĐANG KHÓA -> Điều hướng tức thì tới giao diện màn hình khóa
                    NavigationService.Navigate(
                        new Uri("/Pages/LockScreen.xaml", UriKind.Relative));
                }
                else
                {
                    // Thiết bị ĐÃ MỞ KHÓA -> Người dùng đang mở app bình thường
                    NavigationService.Navigate(
                        new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
                }
            }
            catch
            {
                // Fallback an toàn nếu có ngoại lệ bảo mật
                NavigationService.Navigate(
                    new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
            }
        }
    }
}
```

> **Nguyên Lý:** `Windows.Phone.System.SystemProtection.ScreenLocked` trả về giá trị `true` khi và chỉ khi hệ điều hành đang ở trạng thái khóa màn hình.

---

## 5. Cơ Chế Bảo Mật & Quá Trình Mở Khóa (Security & Unlock Flow)

Khi một trang Silverlight đóng vai trò màn hình khóa, nó phải tuân thủ nghiêm ngặt các quy tắc bảo vệ:

### 5.1 Chặn phím cứng Back (Hardware Back Key Interception)
Nếu không chặn phím Back, người dùng chỉ cần nhấn nút Back trên điện thoại là ứng dụng sẽ bị đóng hoặc lùi trang, vô hiệu hóa toàn bộ màn hình khóa.

```csharp
// Trong Pages/LockScreen.xaml.cs
private void PhoneApplicationPage_BackKeyPress(object sender, CancelEventArgs e)
{
    // BẮT BUỘC: Hủy bỏ sự kiện Back Key khi đang ở màn hình khóa
    e.Cancel = true;
}
```

### 5.2 Xử lý cử chỉ vuốt & Vật lý đàn hồi (Swipe Physics & Snap-back)
Khi người dùng kéo ngón tay lên, giao diện phải di chuyển mượt mà theo ngón tay. Nếu thả tay ra khi chưa kéo đủ độ cao, giao diện phải tự động đàn hồi trở về vị trí cũ (Snap-back):

```csharp
private double dragDeltaY = 0;
private const double UNLOCK_THRESHOLD = -150.0; // Kéo quá 150px sẽ mở khóa

private void RootGrid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
{
    dragDeltaY += e.DeltaManipulation.Translation.Y;
    if (dragDeltaY > 0) dragDeltaY = 0; // Không cho phép kéo tụt xuống dưới

    // Cập nhật vị trí tức thời theo ngón tay
    FrontTransform.TranslateY = dragDeltaY;
    BehindTransform.TranslateY = dragDeltaY;

    // Làm mờ dần khi kéo lên cao
    double opacity = 1.0 - Math.Min(1.0, Math.Abs(dragDeltaY) / 500.0);
    OverlayInformationPanel.Opacity = opacity;
    BehindForegroundGrid.Opacity = opacity;
}

private void RootGrid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
{
    // Kiểm tra nếu kéo vượt ngưỡng hoặc vuốt nhanh (Vận tốc lớn)
    if (dragDeltaY < UNLOCK_THRESHOLD || e.FinalVelocities.LinearVelocity.Y < -800)
    {
        RequestScreenUnlock();
    }
    else
    {
        // Chưa đủ lực/quãng đường -> Chạy animation Snap-back về vị trí 0
        PlaySnapBackAnimation();
    }
}
```

### 5.3 Gọi lệnh mở khóa hệ điều hành: `SystemProtection.RequestScreenUnlock()`
```csharp
private void RequestScreenUnlock()
{
    if (bIsAnimOn)
    {
        try 
        { 
            // Bắt đầu animation trượt thẳng lên trên (-800px)
            UnlockAnim.Begin(); 
            return; 
        } 
        catch { }
    }
    DoActualUnlock();
}

private void UnlockAnim_Completed(object sender, EventArgs e)
{
    DoActualUnlock();
}

private void DoActualUnlock()
{
    try
    {
        if (SystemProtection.ScreenLocked)
        {
            // BÀN GIAO QUYỀN KIỂM SOÁT LẠI CHO HỆ ĐIỀU HÀNH
            SystemProtection.RequestScreenUnlock();
        }
    }
    catch { }
}
```

### 5.4 Cơ chế tương tác với mật khẩu cấp OS
* **Nếu máy không có mật khẩu:** Ngay khi `RequestScreenUnlock()` được gọi, hệ điều hành lập tức tắt lớp phủ màn hình khóa, đưa người dùng vào màn hình Start hoặc ứng dụng đang dùng dở.
* **Nếu máy có mật khẩu (PIN 4-6 số do người dùng cài trong Settings điện thoại):** Hệ điều hành **ngay lập tức đẩy giao diện bàn phím số Native PIN của Windows Phone lên đè lên trên**. Người dùng phải nhập đúng mật khẩu này mới có thể truy cập điện thoại.
* **Kết luận:** Live Lock Screen an toàn 100%, không bao giờ có thể bị lợi dụng để hack hay bypass mật khẩu của người dùng.

---

## 6. Bài Toán Hiệu Năng "Resuming..." & Tối Ưu Cho Máy 512MB RAM

### 6.1 Vì sao ứng dụng gốc của Microsoft từng bị người dùng chỉ trích?
Năm 2014, ứng dụng *Live Lock Screen BETA* của Microsoft bị phàn nàn nhiều nhất ở điểm: **Mỗi khi bấm nút nguồn mở máy, màn hình xuất hiện chữ "Resuming..." màu xám mất 1 đến 2 giây trước khi hiện mặt đồng hồ.**

**Nguyên nhân kỹ thuật sâu xa:**
1. **Dung lượng RAM eo hẹp của thiết bị bình dân:** Hầu hết máy Windows Phone 8.1 thời đó (Lumia 520, 525, 530, 620, 625, 630, 720) chỉ có **512MB RAM**.
2. **Áp lực bộ nhớ đồ họa khi nạp ảnh (Bitmap Heap):** Mỗi bức ảnh nền Full HD (1080×1920) khi giải nén ra bộ nhớ RAM dạng uncompressed 32-bit ARGB sẽ ngốn:
   $$\text{RAM} = 1080 \times 1920 \times 4 \text{ bytes} \approx 8.3 \text{ MB}$$
   Nếu nạp 2 ảnh (ảnh nền + ảnh chủ thể tách nền Depth) cộng với các texture của hệ thống, bộ nhớ đồ họa có thể tăng thêm 20-30MB trong chớp mắt.
3. **OS Watchdog Timeout (500ms):** Nếu ứng dụng Live Lock Screen không hoàn tất vẽ khung hình đầu tiên (Frame 0) trong khoảng thời gian quy định của hệ điều hành, OS sẽ cưỡng chế hiển thị màn hình khóa tĩnh dự phòng để tránh người dùng bị kẹt ở màn hình đen.

### 6.2 Giải pháp tối ưu hóa cực hạn trong dự án HyperOS

| Chiến Lược Tối Ưu | Kỹ Thuật Triển Khai Trong Mã Nguồn | Lợi Ích Mang Lại |
|---|---|---|
| **Dọn dẹp bộ nhớ triệt để (`OnNavigatedFrom`)** | Gán toàn bộ `ImageBrush.ImageSource = null`, dừng Storyboard vô hạn (`FlashBattery.Stop()`, `ChargingPulse.Stop()`) và gọi `GC.Collect()`. | Giải phóng ngay lập tức 100% RAM đồ họa khi tắt màn hình hoặc mở khóa, ngăn chặn tràn RAM (OOM). |
| **Khử nhòe sub-pixel (Pixel Snapping)** | Mọi phép tính toán tọa độ `Margin` hoặc kích thước đều được ép kiểu số nguyên `(int)Math.Round(...)`. | Loại bỏ hiện tượng anti-aliasing nội suy sub-pixel của Silverlight, giúp chữ sắc nét tuyệt đối và giảm tải GPU shader. |
| **Khởi tạo trực tiếp bằng C# thay vì parse XAML (`ClockRenderer.cs`)** | Tạo động `TextBlock` bằng code-behind thuần túy, tái sử dụng các instance cọ vẽ `SolidColorBrush` tĩnh. | Giảm thời gian nạp giao diện từ ~800ms xuống còn <50ms, triệt tiêu hoàn toàn hiện tượng "Resuming...". |
| **Khóa kích thước AI Wallpaper** | Giới hạn tối đa kích thước ảnh do Pollinations AI tạo ra ở mức `1024×1024`. | Đảm bảo tỷ lệ khung hình chuẩn và không gây sốc bộ nhớ trên máy 512MB. |

---

## 7. Kiến Trúc Hiệu Ứng Chiều Sâu 2.5D (Wallpaper Depth Effect)

Hiệu ứng chiều sâu mang lại phong cách hiện đại cho màn hình khóa bằng cách đặt một phần chữ số đồng hồ chìm ra phía sau chủ thể:

```
┌────────────────────────────────────────────────────────────────────────┐
│  LỚP 4: Front Layer (`OverlayInformationPanel`)                        │
│  - Chữ số giờ / ngày / widget nằm đè lên mặt trước chủ thể              │
├────────────────────────────────────────────────────────────────────────┤
│  LỚP 3: Foreground Subject Layer (`ForegroundLayerGrid`)               │
│  - Bức ảnh PNG trong suốt đã được tách nền (chân dung, xe, hoa...)     │
├────────────────────────────────────────────────────────────────────────┤
│  LỚP 2: Behind Layer (`BehindForegroundGrid`)                          │
│  - Chữ số giờ / phút nằm chìm phía sau lưng chủ thể                     │
├────────────────────────────────────────────────────────────────────────┤
│  LỚP 1: Background Layer (`RootGrid.Background`)                       │
│  - Ảnh nền phong cảnh gốc (Full Wallpaper JPG)                         │
└────────────────────────────────────────────────────────────────────────┘
```

### Kỹ thuật đồng bộ chuyển vị (Transform Synchronization)
Để tránh hiện tượng văn bản ở Lớp 2 và Lớp 4 bị "rách hình" hoặc trôi lệch nhau khi người dùng vuốt ngón tay:
- Cả hai lớp đều được gán `CompositeTransform` riêng biệt: `FrontTransform` và `BehindTransform`.
- Trong hàm `RootGrid_ManipulationDelta`, cả hai Transform này **bắt buộc phải nhận cùng một giá trị `dragDeltaY`** trong cùng một chu kỳ vẽ (render frame).

---

## 8. Hướng Dẫn Từng Bước Cho Lập Trình Viên Tạo Mới Live Lock Screen

### Bước 1: Khởi tạo Project
- Dùng **Visual Studio 2015** (hoặc VS 2013).
- Chọn template: `Visual C# > Windows Phone Apps > Blank App (Windows Phone Silverlight)`.
- Target OS Version: **Windows Phone 8.1**.

### Bước 2: Cấu hình `WMAppManifest.xml`
- Mở file bằng `View Code` (XML Editor).
- Thêm capability:
  ```xml
  <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
  ```
- Thêm cụm thẻ `<Extensions>` ngay sau thẻ `</Tokens>`:
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

### Bước 3: Tạo File Descriptor
- Tạo thư mục mới trong project: `Extensions`.
- Tạo file XML bên trong: `LockAppExtension.xml`.
- Nội dung file:
  ```xml
  <?xml version="1.0"?>
  <x:Extension xmlns:x="urn:LockApp">
    <AppID>App</AppID>
  </x:Extension>
  ```
- Trong cửa sổ **Properties** của file này:
  - `Build Action`: **Content**
  - `Copy to Output Directory`: **Copy if newer**

### Bước 4: Tạo Router `LockScreenPage.xaml`
- Tạo một trang rỗng `LockScreenPage.xaml`.
- Trong code-behind `OnNavigatedTo`:
  ```csharp
  if (Windows.Phone.System.SystemProtection.ScreenLocked)
      NavigationService.Navigate(new Uri("/Pages/LockScreen.xaml", UriKind.Relative));
  else
      NavigationService.Navigate(new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
  ```

### Bước 5: Viết Giao Diện Khóa `LockScreen.xaml`
- Gắn sự kiện `BackKeyPress="PhoneApplicationPage_BackKeyPress"` và đặt `e.Cancel = true;`.
- Bắt cử chỉ vuốt `ManipulationDelta` và `ManipulationCompleted`.
- Khi người dùng vuốt lên đủ ngưỡng, gọi `Windows.Phone.System.SystemProtection.RequestScreenUnlock()`.

---

## 9. Bảng Tra Cứu API An Toàn vs Bị Cấm (WP8.1 Silverlight)

| Danh Mục | ❌ Tuyệt Đối Tránh (WinRT / UWP) | ✅ Sử Dụng Thay Thế (Silverlight WP8.1) |
|---|---|---|
| **UI Framework** | `Windows.UI.Xaml.*` | `System.Windows.*` |
| **File I/O** | `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| **Cài đặt App** | `Windows.Storage.ApplicationData` | `System.IO.IsolatedStorage.IsolatedStorageSettings` |
| **Chọn ảnh** | `FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| **Nút Back cứng** | `HardwareButtons.BackPressed` | `PhoneApplicationPage.BackKeyPress` event |
| **Điều hướng** | `Frame.Navigate()` | `NavigationService.Navigate()` |
| **Luồng giao diện**| `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke()` |
| **Giao diện Binding**| `x:Bind` | `{Binding}` hoặc gán trực tiếp bằng C# |
| **Mở khóa máy** | `Application.Current.Exit()` | `SystemProtection.RequestScreenUnlock()` |

---

*Tài liệu được biên soạn dựa trên nghiên cứu kiến trúc thực tế và hoàn thiện mã nguồn của dự án HyperOS Live Lock Screen.*
