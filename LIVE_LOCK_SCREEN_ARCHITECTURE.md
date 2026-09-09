# 📘 Windows Phone 8.1 Live Lock Screen: Tài Liệu Kỹ Thuật & Kiến Trúc Chuyên Sâu

Tài liệu kỹ thuật chi tiết dành cho lập trình viên giải thích bản chất, cơ chế hoạt động tầng hệ điều hành và kỹ thuật xây dựng ứng dụng **Live Lock Screen** trên nền tảng **Windows Phone 8.1 Silverlight**.

---

## 1. Bối Cảnh & Bản Chất Của Live Lock Screen

### 1.1 Lịch sử & Công bố
Tại hội nghị **Microsoft Build 2014**, Joe Belfiore đã trình diễn một tính năng được cộng đồng Windows Phone chờ đợi từ lâu: **Live Lock Screen**. Không giống như màn hình khóa truyền thống của Windows Phone 8.0 (chỉ cho phép thay đổi ảnh nền tĩnh và 5 biểu tượng thông báo badge), Live Lock Screen trên WP8.1 cho phép nhà phát triển bên thứ ba kiểm soát toàn bộ giao diện và chuyển động (animation) của màn hình khóa.

### 1.2 Bản chất kỹ thuật thực sự
Một câu hỏi lớn mà nhiều lập trình viên thắc mắc: **Live Lock Screen có thay thế kernel lock screen bảo mật của Windows Phone không?**

> **Câu trả lời là: KHÔNG.**
>
> Ứng dụng Live Lock Screen thực chất là một **ứng dụng Silverlight đặc biệt** chạy trong một sandbox chuyên dụng được hệ điều hành Windows Phone 8.1 cấp quyền hiển thị ngay trên bề mặt màn hình khóa (Lock Screen Compositor Surface). Hệ điều hành vẫn kiểm soát toàn bộ phần cứng, bảo mật PIN gốc và các trạng thái nguồn của thiết bị.

```
┌─────────────────────────────────────────────────────────────┐
│                 Màn hình hiển thị (Display)                 │
├─────────────────────────────────────────────────────────────┤
│  [Lớp 1 - Cao nhất] Native OS PIN Keypad (Nếu máy có PIN)   │
├─────────────────────────────────────────────────────────────┤
│  [Lớp 2] Live Lock Screen Extensibility App (Silverlight)   │
│         - Đồng hồ HyperOS, hoạt ảnh, hiệu ứng Depth, v.v.    │
├─────────────────────────────────────────────────────────────┤
│  [Lớp 3] OS Lock Screen Host & System Protection Daemon     │
├─────────────────────────────────────────────────────────────┤
│  [Lớp 4 - Thấp nhất] Start Screen / App đang chạy ngầm      │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Cơ Chế Đăng Ký Mở Rộng (Extensibility Architecture)

Để biến một ứng dụng thông thường thành Live Lock Screen, ứng dụng phải đăng ký các Consumer ID và Task Extensibility đặc thù trong cấu hình ứng dụng.

### 2.1 Khai báo trong `WMAppManifest.xml`

Trong file `Properties/WMAppManifest.xml`, ứng dụng cần đăng ký hai thành phần: **Extension** và **Capability**.

```xml
<Deployment xmlns="http://schemas.microsoft.com/windowsphone/2014/deployment" AppPlatformVersion="8.1">
  <App ... RuntimeType="Silverlight">
    
    <!-- 1. Quyền tương tác với API màn hình khóa hệ thống -->
    <Capabilities>
      <Capability Name="ID_CAP_SHELL_DEVICE_LOCK_UI_API" />
      <Capability Name="ID_CAP_IDENTITY_DEVICE" />
    </Capabilities>

    <!-- 2. Task mặc định trỏ tới Router Gateway -->
    <Tasks>
      <DefaultTask Name="_default" NavigationPage="LockScreenPage.xaml" ActivationPolicy="Resume" />
    </Tasks>

    <!-- 3. Đăng ký Extension điểm nối hệ thống (LockScreen Consumer IDs) -->
    <Extensions>
      <!-- LockScreen Application: Định danh ứng dụng giao diện khóa -->
      <Extension ExtensionName="LockScreen_Application"
                 ConsumerID="{CD4601F6-351B-43C7-9087-6B12BD98ED63}"
                 TaskID="_default"
                 ExtraFile="Extensions\\LockAppExtension.xml" />
      
      <!-- LockScreen Background: Cho phép xử lý nền màn khóa -->
      <Extension ExtensionName="LockScreen_Background"
                 ConsumerID="{111DFF24-AA15-4A96-8006-2BFF8122084F}"
                 TaskID="_default" />
    </Extensions>

  </App>
</Deployment>
```

#### Ý nghĩa các GUID đặc biệt:
* `{CD4601F6-351B-43C7-9087-6B12BD98ED63}`: GUID nội bộ của Windows Phone 8.1 OS Shell để nhận diện extension provider cho Live Lock Screen. Khi người dùng vào *Settings > Lock Screen* trên điện thoại, OS sẽ quét các app có ConsumerID này để hiển thị trong danh sách chọn.
* `{111DFF24-AA15-4A96-8006-2BFF8122084F}`: GUID cho phép ứng dụng cung cấp dữ liệu nền trong background stream.

### 2.2 File chỉ dẫn `Extensions\LockAppExtension.xml`

Tệp tin này bắt buộc phải nằm đúng đường dẫn được khai báo trong thuộc tính `ExtraFile` của thẻ Extension:

```xml
<?xml version="1.0"?>
<x:Extension xmlns:x="urn:LockApp">
  <AppID>App</AppID>
</x:Extension>
```

Tệp này xác thực với hệ thống rằng ứng dụng triển khai chuẩn giao thức `urn:LockApp`.

---

## 3. Vòng Đời Thực Thi & Mô Hình "Dual-Role" (Hai Trạng Thái)

Một ứng dụng Live Lock Screen là một **Dual-Role App (Ứng dụng hai vai trò)**. Người dùng có thể khởi chạy ứng dụng từ hai ngữ cảnh hoàn toàn khác nhau:
1. **Ngữ cảnh Mở (Unlocked Context):** Người dùng chạm vào icon app trên danh sách ứng dụng hoặc Live Tile ở màn hình Start. Ứng dụng phải mở giao diện cài đặt/tùy biến (`MySetsPage.xaml` hoặc `EditorPage.xaml`).
2. **Ngữ cảnh Khóa (Locked Context):** Người dùng bấm nút nguồn bật sáng màn hình. Hệ điều hành kích hoạt ứng dụng để hiển thị màn hình khóa (`LockScreen.xaml`).

### 3.1 Cửa ngõ phân luồng: `LockScreenPage.xaml.cs`
Điểm vào mặc định của ứng dụng (`DefaultTask NavigationPage`) **không được trỏ trực tiếp** vào `LockScreen.xaml` hay `MySetsPage.xaml`, mà phải trỏ vào một trang trung gian điều hướng (`LockScreenPage.xaml`):

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
                // Kiểm tra trạng thái khóa của thiết bị
                if (SystemProtection.ScreenLocked)
                {
                    // Thiết bị ĐANG KHÓA -> Chuyển ngay đến trang Màn hình khóa
                    NavigationService.Navigate(
                        new Uri("/Pages/LockScreen.xaml", UriKind.Relative));
                }
                else
                {
                    // Thiết bị ĐÃ MỞ KHÓA -> Chuyển vào trang Bộ sưu tập cài đặt
                    NavigationService.Navigate(
                        new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
                }
            }
            catch
            {
                // Fallback an toàn nếu API SystemProtection gặp sự cố
                NavigationService.Navigate(
                    new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
            }
        }
    }
}
```

> **API Cốt Lõi:** `Windows.Phone.System.SystemProtection.ScreenLocked` trả về `true` khi và chỉ khi hệ thống đang ở trạng thái khóa bảo vệ. Đây là mấu chốt để phân tách hoàn toàn hai luồng giao diện người dùng.

---

## 4. Cơ Chế Mở Khóa & Bảo Mật (Security & Unlock Flow)

Khi hoạt động trong ngữ cảnh màn hình khóa, ứng dụng chịu sự kiểm soát chặt chẽ của các quy tắc an toàn.

### 4.1 Chặn phím cứng Back (Hardware Back Button Blocking)
Trên Windows Phone, nút Back vật lý mặc định sẽ đưa ứng dụng về trang trước đó hoặc thoát ra Start Screen. Nếu không chặn, người dùng có thể vượt qua (bypass) màn hình khóa mà không cần vuốt.

```csharp
// Trong Pages/LockScreen.xaml.cs
private void PhoneApplicationPage_BackKeyPress(object sender, CancelEventArgs e)
{
    // BẮT BUỘC: Hủy bỏ sự kiện Back Key khi đang hiển thị màn hình khóa
    e.Cancel = true;
}
```

### 4.2 Kích hoạt mở khóa: `SystemProtection.RequestScreenUnlock()`
Khi người dùng hoàn tất cử chỉ vuốt lên (hoặc nhập đúng mã PIN/Pattern tùy chỉnh trong ứng dụng), ứng dụng gửi tín hiệu yêu cầu mở khóa về cho OS:

```csharp
private void DoActualUnlock()
{
    try
    {
        if (SystemProtection.ScreenLocked)
        {
            // Yêu cầu hệ điều hành hoàn tất chu trình mở khóa
            SystemProtection.RequestScreenUnlock();
        }
    }
    catch (Exception ex)
    {
        // Xử lý ngoại lệ nếu lời gọi bị hệ thống từ chối
    }
}
```

### 4.3 Tương tác với Mật khẩu cấp Hệ Điều Hành (Native OS Password)
Nhiều lập trình viên lo ngại: *"Nếu ứng dụng của tôi bị lỗi, hoặc nếu người dùng cài PIN của máy thì sao?"*
* Khi ứng dụng gọi `SystemProtection.RequestScreenUnlock()`:
  * **Trường hợp máy KHÔNG có mật khẩu OS:** Hệ thống ngay lập tức gỡ bỏ lớp che chắn màn khóa, đưa người dùng thẳng vào ứng dụng trước đó hoặc màn hình Start.
  * **Trường hợp máy CÓ cài mật khẩu OS:** Hệ điều hành **ngay lập tức đẩy giao diện bàn phím số Native PIN của Windows Phone lên trên**. Người dùng phải nhập đúng PIN của máy mới vào được điện thoại.
* Điều này chứng minh Live Lock Screen **không tạo ra lỗ hổng bảo mật** cho thiết bị, vì tầng bảo mật hạt nhân của OS luôn được kích hoạt sau khi ứng dụng nhường quyền kiểm soát.

---

## 5. Thách Thức Hiệu Năng & Bài Toán "Resuming..." (512MB RAM)

### 5.1 Vấn đề "Resuming..." kinh điển của Microsoft
Năm 2014, ứng dụng *Live Lock Screen BETA* của chính Microsoft nhận nhiều phản hồi tiêu cực vì mỗi khi người dùng bấm nút nguồn để xem giờ, màn hình hiển thị dòng chữ *"Resuming..."* mất 1-2 giây trước khi hiện mặt đồng hồ.

**Nguyên nhân kỹ thuật:**
1. **Dung lượng RAM eo hẹp:** Các dòng máy giá rẻ chiếm đa số thị phần thời đó (Lumia 520, 525, 630) chỉ có **512MB RAM**.
2. **Quá trình giải nén BitmapImage nặng nề:** Tải các bức ảnh nền JPEG/PNG kích thước lớn từ IsolatedStorage mỗi lần mở máy gây áp lực I/O và CPU cực lớn.
3. **Cây giao diện (Visual Tree) cồng kềnh:** Sử dụng quá nhiều UserControl lồng ghép, bộ lọc đồ họa phức tạp và Data Binding chậm chạp.

### 5.2 Kỹ thuật tối ưu hóa trong dự án HyperOS

Dự án HyperOS đạt được khả năng phản hồi tức thì nhờ áp dụng các nguyên tắc kỹ thuật sau:

#### A. Giải phóng bộ nhớ triệt để khi rời trang (`OnNavigatedFrom`)
Khi người dùng mở khóa thành công hoặc thoát khỏi ứng dụng, toàn bộ bộ nhớ ảnh phải được trả lại cho OS ngay lập tức để tránh tràn bộ nhớ (Out Of Memory - OOM):

```csharp
protected override void OnNavigatedFrom(NavigationEventArgs e)
{
    base.OnNavigatedFrom(e);

    // Hủy tham chiếu Source của ImageBrush để GC dọn dẹp bộ nhớ đồ họa
    MainBackgroundBrush.ImageSource = null;
    ForegroundBrush.ImageSource = null;

    // Dừng toàn bộ Storyboard animation chạy vô tận
    if (FlashBattery != null) FlashBattery.Stop();
    if (ChargingPulse != null) ChargingPulse.Stop();

    // Kích hoạt thu gom rác chủ động
    GC.Collect();
}
```

#### B. Khử nhòe Anti-Aliasing bằng phép làm tròn tọa độ số nguyên (Pixel Snapping)
Trên Silverlight, nếu tọa độ `Margin` hoặc kích thước `Canvas.Left/Top` là số thực thập phân (floating-point), renderer sẽ thực hiện nội suy sub-pixel, khiến chữ và số bị nhòe:

```csharp
// Luôn ép kiểu tọa độ tính toán về số nguyên (int)
int targetX = (int)Math.Round(calculatedX);
int targetY = (int)Math.Round(calculatedY);
element.Margin = new Thickness(targetX, targetY, 0, 0);
```

#### C. Kiến trúc Render tập trung (`ClockRenderer.cs`)
Thay vì viết lại XAML hiển thị đồng hồ ở nhiều nơi (vừa ở LockScreen, vừa ở Editor, vừa ở MySets preview), dự án sử dụng một helper tĩnh tập trung `ClockRenderer`. Helper này trực tiếp tạo và cấu hình các `TextBlock` gọn nhẹ, loại bỏ hoàn toàn chi phí khởi tạo XAML parser runtime.

---

## 6. Kiến Trúc Hiệu Ứng Chiều Sâu 2.5D (Depth Effect Architecture)

Hiệu ứng Wallpaper Depth (chữ đồng hồ chìm một phần ra sau chủ thể người/vật) được tạo ra nhờ kỹ thuật phân lớp đa tầng (**Multi-Layer Compositing**):

```
┌────────────────────────────────────────────────────────┐
│  Lớp 4: Front Overlay Panel                           │
│  - Chữ hiển thị ở mặt trước (Hour / Date / Widgets)   │
├────────────────────────────────────────────────────────┤
│  Lớp 3: Foreground Image Layer (PNG Trong Suốt)       │
│  - Đối tượng chủ thể (người, hoa, tòa nhà...)          │
├────────────────────────────────────────────────────────┤
│  Lớp 2: Behind Grid Layer                             │
│  - Chữ hiển thị ở mặt sau (Hour / Minute / Colon)     │
├────────────────────────────────────────────────────────┤
│  Lớp 1: Background Layer                              │
│  - Ảnh nền gốc (Wallpaper)                            │
└────────────────────────────────────────────────────────┘
```

### Đồng bộ hóa cử chỉ vuốt (Swipe Synchronization)
Khi người dùng vuốt tay để mở khóa, cả `OverlayInformationPanel` (Lớp 4) và `BehindForegroundGrid` (Lớp 2) phải cùng nhận một giá trị dịch chuyển `TranslateY` thông qua `CompositeTransform`:

```csharp
// Đồng bộ vị trí vuốt giữa lớp trước và lớp sau
private void RootGrid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
{
    dragDeltaY += e.DeltaManipulation.Translation.Y;
    if (dragDeltaY > 0) dragDeltaY = 0; // Chỉ cho phép vuốt lên

    FrontTransform.TranslateY = dragDeltaY;
    BehindTransform.TranslateY = dragDeltaY;
}
```

Kỹ thuật này đảm bảo văn bản ở hai lớp không bao giờ bị lệch vị trí của nhau trong suốt quá trình vuốt của ngón tay.

---

## 7. Quy Trình 5 Bước Tạo Dự Án Live Lock Screen Mới

Dành cho lập trình viên muốn tự tạo một ứng dụng Live Lock Screen từ đầu:

1. **Khởi tạo dự án:** Mở Visual Studio 2015, chọn template `Windows Phone Silverlight > Blank App (Windows Phone Silverlight)` với target **Windows Phone 8.1**.
2. **Khai báo Manifest:**
   - Mở `WMAppManifest.xml` bằng XML Editor.
   - Thêm capability: `ID_CAP_SHELL_DEVICE_LOCK_UI_API`.
   - Thêm cụm `<Extensions>` với hai Consumer ID (`LockScreen_Application` và `LockScreen_Background`).
3. **Tạo descriptor:** Tạo thư mục `Extensions`, thêm file `LockAppExtension.xml` có nội dung `<x:Extension xmlns:x="urn:LockApp"><AppID>App</AppID></x:Extension>`. Đặt thuộc tính file là `Content` và `Copy if newer`.
4. **Xây dựng Router:** Tạo trang `LockScreenPage.xaml`, trong `OnNavigatedTo` kiểm tra `Windows.Phone.System.SystemProtection.ScreenLocked` để rẽ nhánh điều hướng.
5. **Xây dựng Lock Screen Page:**
   - Trong sự kiện `BackKeyPress`: Đặt `e.Cancel = true;`.
   - Bắt sự kiện vuốt `ManipulationDelta` / `ManipulationCompleted`.
   - Khi hoàn thành cử chỉ vuốt: Gọi `SystemProtection.RequestScreenUnlock()`.

---

## 8. Bảng Tra Cứu API An Toàn vs Bị Cấm (Cheat Sheet)

| Mục Đích | ❌ Không Dùng (Gây Lỗi Build/Crash) | ✅ Bắt Buộc Dùng (Silverlight WP8.1) |
|---|---|---|
| **Giao diện XAML** | `Windows.UI.Xaml.*` (WinRT) | `System.Windows.*` |
| **Lưu trữ file** | `Windows.Storage.StorageFile` | `System.IO.IsolatedStorage.IsolatedStorageFile` |
| **Lưu cài đặt** | `Windows.Storage.ApplicationData` | `System.IO.IsolatedStorage.IsolatedStorageSettings` |
| **Chọn ảnh** | `FileOpenPicker` | `Microsoft.Phone.Tasks.PhotoChooserTask` |
| **Nút Back** | `HardwareButtons.BackPressed` | `PhoneApplicationPage.BackKeyPress` |
| **Điều hướng UI** | `Frame.Navigate()` | `NavigationService.Navigate()` |
| **Chạy luồng UI** | `CoreDispatcher` | `Deployment.Current.Dispatcher.BeginInvoke()` |
| **Mở khóa máy** | Tự ý tắt app (`Application.Current.Exit()`) | `SystemProtection.RequestScreenUnlock()` |

---

*Tài liệu được biên soạn dựa trên cấu trúc mã nguồn thực tế của dự án HyperOS Live Lock Screen.*
