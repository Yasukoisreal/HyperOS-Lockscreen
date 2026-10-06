using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Info;

namespace HyperOS.Helpers
{
    public class NotificationBadgeItem
    {
        public BitmapImage Icon { get; set; }
        public string Counter { get; set; }
        public bool HasCounter
        {
            get { return !string.IsNullOrEmpty(Counter); }
        }
    }

    public class LockScreenSnapshotResult
    {
        public bool HasAlarm { get; set; }
        public bool HasDriving { get; set; }
        public bool HasDoNotDisturb { get; set; }
        public string DetailedNotificationText { get; set; }

        public bool HasDetailedNotification
        {
            get { return !string.IsNullOrEmpty(DetailedNotificationText); }
        }

        public List<NotificationBadgeItem> Badges { get; set; }

        public bool HasBadges
        {
            get { return Badges != null && Badges.Count > 0; }
        }

        public LockScreenSnapshotResult()
        {
            Badges = new List<NotificationBadgeItem>();
        }
    }

    public static class LockScreenBridgeHelper
    {
        private static bool _bridgeFailed = false;
        private static string _resolution = null;

        static LockScreenBridgeHelper()
        {
            try
            {
                object obj;
                if (DeviceExtendedProperties.TryGetValue("PhysicalScreenResolution", out obj) && obj is Size)
                {
                    Size sz = (Size)obj;
                    _resolution = (int)sz.Width + "x" + (int)sz.Height;
                }
                else if (Application.Current != null && Application.Current.Host != null && Application.Current.Host.Content != null)
                {
                    double scale = Application.Current.Host.Content.ScaleFactor;
                    double w = scale * Application.Current.Host.Content.ActualWidth / 100.0;
                    double h = scale * Application.Current.Host.Content.ActualHeight / 100.0;
                    _resolution = (int)w + "x" + (int)h;
                }
            }
            catch
            {
                _resolution = "480x800";
            }
        }

        /// <summary>
        /// Reads notification badges, alarm status, and detailed text from LockScreen_Bridge.
        /// Gracefully falls back to mock preview if running on x86 Emulator or when native bridge is unavailable.
        /// </summary>
        public static LockScreenSnapshotResult GetSnapshot()
        {
            var result = new LockScreenSnapshotResult();

            if (!_bridgeFailed)
            {
                try
                {
                    // Calls native ARM bridge via WinMD
                    GetRealSnapshot(result);
                    return result;
                }
                catch (Exception)
                {
                    // On x86 emulator or if native LockScreen.Bridge.dll cannot be loaded
                    _bridgeFailed = true;
                }
            }

            // Fallback for emulator / testing preview
            PopulateMockSnapshot(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetRealSnapshot(LockScreenSnapshotResult result)
        {
            var provider = new LockScreen_Bridge.LockScreenInfoProvider();
            var snapshot = new LockScreen_Bridge.DeviceLockscreenSnapshot();
            provider.GetSnapshot(snapshot);

            result.HasAlarm = !string.IsNullOrEmpty(snapshot.AlarmIconUri);
            result.HasDriving = !string.IsNullOrEmpty(snapshot.DrivingModeIcon);
            result.HasDoNotDisturb = !string.IsNullOrEmpty(snapshot.DoNotDisturbModeIconUri);

            // Detailed notification text
            if (snapshot.DetailedTexts != null)
            {
                var texts = snapshot.DetailedTexts
                    .Where(t => t != null && !string.IsNullOrEmpty(t.Text))
                    .Select(t => t.Text)
                    .ToList();

                if (texts.Count > 0)
                {
                    result.DetailedNotificationText = string.Join("\n", texts);
                }
            }

            // Quick Badges
            if (snapshot.Badges != null)
            {
                foreach (var badge in snapshot.Badges)
                {
                    if (badge == null || string.IsNullOrEmpty(badge.BadgeIconUri))
                        continue;

                    var item = new NotificationBadgeItem
                    {
                        Counter = badge.BadgeValue,
                        Icon = LoadBadgeIcon(badge.BadgeIconUri)
                    };

                    result.Badges.Add(item);
                    if (result.Badges.Count >= 5)
                        break;
                }
            }
        }

        private static BitmapImage LoadBadgeIcon(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return null;

            try
            {
                if (uri.StartsWith("res:", StringComparison.OrdinalIgnoreCase))
                {
                    // Format: res://<dll>!<resource_id>
                    string[] parts = uri.Substring(4).TrimStart('/').Split(new char[] { '!' }, 2);
                    if (parts.Length == 2)
                    {
                        string resourceId = parts[1];
                        string dllName = parts[0].Replace("{ScreenResolution}", _resolution ?? "480x800");
                        string dllPath = "c:\\windows\\system32\\" + dllName + ".dll";

                        byte[] bytes = LockScreen_Bridge.LockScreenInfoProvider.GetImageFromResource(dllPath, resourceId);
                        if (bytes != null && bytes.Length > 0)
                        {
                            var bmp = new BitmapImage();
                            bmp.SetSource(new MemoryStream(bytes));
                            return bmp;
                        }
                    }
                }
                else
                {
                    var bmp = new BitmapImage();
                    bmp.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
                    return bmp;
                }
            }
            catch
            {
            }

            // Default fallback icon
            try
            {
                var fallback = new BitmapImage();
                fallback.UriSource = new Uri("/Assets/DefaultLockImage.png", UriKind.Relative);
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        private static void PopulateMockSnapshot(LockScreenSnapshotResult result)
        {
            // On emulator: provide realistic mock status so developer can preview UI
            result.HasAlarm = true;

            try
            {
                result.Badges.Add(new NotificationBadgeItem
                {
                    Counter = "2",
                    Icon = new BitmapImage(new Uri("/Assets/DefaultLockImage.png", UriKind.Relative))
                });
                result.Badges.Add(new NotificationBadgeItem
                {
                    Counter = "5",
                    Icon = new BitmapImage(new Uri("/Assets/DefaultLockImage.png", UriKind.Relative))
                });
            }
            catch
            {
            }
        }
    }
}
