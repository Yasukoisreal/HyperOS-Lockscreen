using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Windows.Phone.System.LockScreenExtensibility;
using System.Net;
using System.Device.Location;
using System.Collections.Generic;

namespace HyperOS.Pages
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        private bool isLoading = true;
        private bool isUpdatingCheckBox = false;

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            LoadSettings();
            CheckLockScreenStatus();
            isLoading = false;
        }

        #region Lock Screen Registration

        private void CheckLockScreenStatus()
        {
            try
            {
                bool isRegistered = ExtensibilityApp.IsLockScreenApplicationRegistered();
                useIt.IsChecked = isRegistered;
                LockScreenStatus.Text = isRegistered
                    ? "✓ Active as live lock screen"
                    : "";
            }
            catch (Exception ex)
            {
                LockScreenStatus.Text = "Error: " + ex.Message;
            }
        }

        private void useIt_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading || isUpdatingCheckBox) return;
            isUpdatingCheckBox = true;

            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Background.jpg"))
                    {
                        if (!ExtensibilityApp.IsLockScreenApplicationRegistered())
                        {
                            ExtensibilityApp.RegisterLockScreenApplication();
                        }
                        useIt.IsChecked = ExtensibilityApp.IsLockScreenApplicationRegistered();
                        LockScreenStatus.Text = useIt.IsChecked == true
                            ? "✓ Active as live lock screen" : "";
                    }
                    else
                    {
                        MessageBox.Show("Please choose a background image first.",
                            "Background Required", MessageBoxButton.OK);
                        useIt.IsChecked = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LockScreenStatus.Text = "Error: " + ex.Message;
                useIt.IsChecked = false;
            }
            finally
            {
                isUpdatingCheckBox = false;
            }
        }

        private void useIt_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading || isUpdatingCheckBox) return;
            isUpdatingCheckBox = true;

            try
            {
                if (ExtensibilityApp.IsLockScreenApplicationRegistered())
                {
                    var result = MessageBox.Show(
                        "Do you want to remove HyperOS as your live lock screen?",
                        "Remove Lock Screen",
                        MessageBoxButton.OKCancel);

                    if (result == MessageBoxResult.OK)
                    {
                        ExtensibilityApp.UnregisterLockScreenApplication();
                    }
                }
                useIt.IsChecked = ExtensibilityApp.IsLockScreenApplicationRegistered();
                LockScreenStatus.Text = useIt.IsChecked == true
                    ? "✓ Active as live lock screen" : "";
            }
            catch (Exception ex)
            {
                LockScreenStatus.Text = "Error: " + ex.Message;
            }
            finally
            {
                isUpdatingCheckBox = false;
            }
        }

        #endregion

        #region Security Settings

        private void PasswordToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsPasswordEnabled", true);
            PinSetupPanel.Visibility = Visibility.Visible;

            // Disable pattern if PIN is enabled
            if (PatternToggle.IsChecked == true)
            {
                PatternToggle.IsChecked = false;
            }
        }

        private void PasswordToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsPasswordEnabled", false);
            PinSetupPanel.Visibility = Visibility.Collapsed;
        }

        private void SetPin_Click(object sender, RoutedEventArgs e)
        {
            string pin = PinInput.Password;
            if (pin.Length == 4)
            {
                SaveSetting("UserPassword", pin);
                PinSetupPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show("PIN has been set!", "PIN Set", MessageBoxButton.OK);
            }
            else
            {
                MessageBox.Show("Please enter exactly 4 digits.",
                    "Invalid PIN", MessageBoxButton.OK);
            }
        }

        private void PatternToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;

            // Disable PIN if pattern is enabled
            if (PasswordToggle.IsChecked == true)
            {
                PasswordToggle.IsChecked = false;
            }

            // Show pattern setup overlay
            PatternSetupOverlay.Visibility = Visibility.Visible;
        }

        private void PatternToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsPatternOn", false);
        }

        private void PatternSetupControl_RegistrationSuccess(object sender, EventArgs e)
        {
            // Pattern saved by the control — enable pattern lock
            SaveSetting("bIsPatternOn", true);
            PatternSetupOverlay.Visibility = Visibility.Collapsed;
            MessageBox.Show("Pattern saved!", "Success", MessageBoxButton.OK);
        }

        private void PatternSetupCancel_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // User cancelled — turn off toggle
            PatternSetupOverlay.Visibility = Visibility.Collapsed;
            PatternToggle.IsChecked = false;
        }

        #endregion

        #region Display Settings

        private void AnimToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsAnimOn", true);
        }

        private void AnimToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsAnimOn", false);
        }

        private void ClockStylePicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockStyle", ClockStylePicker.SelectedIndex);
        }

        private void ClockPositionPicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockPosition", ClockPositionPicker.SelectedIndex);
        }

        private void ClockHAlignPicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockHAlign", ClockHAlignPicker.SelectedIndex);
        }

        private void ClockColorPicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockColor", ClockColorPicker.SelectedIndex);
            // Reset blend when solid is picked
            if (ClockBlendPicker.SelectedIndex != 0)
            {
                isLoading = true;
                ClockBlendPicker.SelectedIndex = 0;
                isLoading = false;
                SaveSetting("ClockBlend", 0);
            }
        }

        private void ClockBlendPicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockBlend", ClockBlendPicker.SelectedIndex);
        }

        private void ClockSizePicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ClockSize", ClockSizePicker.SelectedIndex);
        }

        private void ChangeWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var photoChooser = new PhotoChooserTask();
            photoChooser.ShowCamera = true;
            photoChooser.Completed += PhotoChooser_Completed;
            photoChooser.Show();
        }

        private void PhotoChooser_Completed(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK && e.ChosenPhoto != null)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.SetSource(e.ChosenPhoto);

                    var wb = new WriteableBitmap(bitmap);

                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        using (var stream = store.CreateFile("Background.jpg"))
                        {
                            wb.SaveJpeg(stream, wb.PixelWidth, wb.PixelHeight, 0, 90);
                        }
                    }

                    MessageBox.Show("Wallpaper has been updated!", "Success",
                        MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving wallpaper: " + ex.Message,
                        "Error", MessageBoxButton.OK);
                }
            }
        }

        private void SaveOwnerInfo_Click(object sender, RoutedEventArgs e)
        {
            string info = OwnerInfoInput.Text.Trim();
            SaveSetting("OwnerInfo", info);
            MessageBox.Show(
                string.IsNullOrEmpty(info) ? "Owner info cleared." : "Owner info saved!",
                "Saved", MessageBoxButton.OK);
        }

        #endregion

        #region Weather

        private void WeatherToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ShowWeather", true);
        }

        private void WeatherToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ShowWeather", false);
        }

        private void WeatherGPS_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
                watcher.PositionChanged += (s, args) =>
                {
                    var pos = args.Position.Location;
                    if (!pos.IsUnknown)
                    {
                        watcher.Stop();
                        Dispatcher.BeginInvoke(() =>
                        {
                            SaveSetting("WeatherLat", pos.Latitude);
                            SaveSetting("WeatherLon", pos.Longitude);
                            WeatherLocationInfo.Text = "GPS: " +
                                pos.Latitude.ToString("F2") + ", " + pos.Longitude.ToString("F2");
                            MessageBox.Show("Location saved!", "GPS", MessageBoxButton.OK);
                        });
                    }
                };
                watcher.Start();
                WeatherLocationInfo.Text = "Getting GPS...";
            }
            catch
            {
                MessageBox.Show("GPS not available.", "Error", MessageBoxButton.OK);
            }
        }

        // Offline city database — 300+ cities worldwide
        private static readonly Dictionary<string, double[]> CityDB = new Dictionary<string, double[]>(
            StringComparer.OrdinalIgnoreCase)
        {
            // ===== VIETNAM =====
            {"Ha Noi", new[]{21.03, 105.85}}, {"Hanoi", new[]{21.03, 105.85}},
            {"Ho Chi Minh", new[]{10.82, 106.63}}, {"HCM", new[]{10.82, 106.63}},
            {"Saigon", new[]{10.82, 106.63}}, {"Sai Gon", new[]{10.82, 106.63}},
            {"Da Nang", new[]{16.07, 108.22}}, {"Danang", new[]{16.07, 108.22}},
            {"Hai Phong", new[]{20.86, 106.68}}, {"Can Tho", new[]{10.03, 105.77}},
            {"Nha Trang", new[]{12.24, 109.19}}, {"Hue", new[]{16.46, 107.60}},
            {"Da Lat", new[]{11.94, 108.44}}, {"Dalat", new[]{11.94, 108.44}},
            {"Vung Tau", new[]{10.35, 107.08}}, {"Quy Nhon", new[]{13.77, 109.22}},
            {"Bien Hoa", new[]{10.95, 106.82}}, {"Thu Duc", new[]{10.85, 106.75}},
            {"Buon Ma Thuot", new[]{12.67, 108.05}}, {"Phan Thiet", new[]{10.93, 108.10}},
            {"Long Xuyen", new[]{10.39, 105.44}}, {"Vinh", new[]{18.68, 105.68}},
            {"Thai Nguyen", new[]{21.59, 105.85}}, {"Nam Dinh", new[]{20.42, 106.17}},
            {"Rach Gia", new[]{10.01, 105.08}}, {"Phu Quoc", new[]{10.23, 103.97}},
            {"Thanh Hoa", new[]{19.81, 105.78}}, {"Ha Long", new[]{20.95, 107.05}},
            {"Ninh Binh", new[]{20.25, 105.97}}, {"Bac Ninh", new[]{21.19, 106.07}},
            {"Pleiku", new[]{13.98, 108.00}}, {"Kon Tum", new[]{14.35, 108.00}},
            {"Tuy Hoa", new[]{13.09, 109.32}}, {"Tam Ky", new[]{15.57, 108.47}},
            {"My Tho", new[]{10.35, 106.37}}, {"Ben Tre", new[]{10.24, 106.38}},
            {"Soc Trang", new[]{9.60, 105.98}}, {"Ca Mau", new[]{9.18, 105.15}},
            {"Bac Lieu", new[]{9.29, 105.72}}, {"Tra Vinh", new[]{9.94, 106.34}},
            {"Sa Dec", new[]{10.30, 105.76}}, {"Cao Lanh", new[]{10.46, 105.63}},
            {"Dong Ha", new[]{16.82, 107.10}}, {"Son La", new[]{21.33, 103.91}},
            {"Lao Cai", new[]{22.49, 103.97}}, {"Sa Pa", new[]{22.34, 103.84}},
            {"Dien Bien", new[]{21.39, 103.02}}, {"Lai Chau", new[]{22.07, 103.16}},
            {"Ha Giang", new[]{22.82, 104.98}}, {"Dong Hoi", new[]{17.47, 106.60}},

            // ===== SOUTHEAST ASIA =====
            {"Bangkok", new[]{13.76, 100.50}}, {"Singapore", new[]{1.35, 103.82}},
            {"Jakarta", new[]{-6.21, 106.85}}, {"Manila", new[]{14.60, 120.98}},
            {"Kuala Lumpur", new[]{3.14, 101.69}}, {"Phnom Penh", new[]{11.56, 104.92}},
            {"Vientiane", new[]{17.97, 102.63}}, {"Yangon", new[]{16.87, 96.20}},
            {"Naypyidaw", new[]{19.76, 96.07}}, {"Bandar Seri Begawan", new[]{4.94, 114.95}},
            {"Dili", new[]{-8.56, 125.57}}, {"Chiang Mai", new[]{18.79, 98.98}},
            {"Phuket", new[]{7.88, 98.39}}, {"Pattaya", new[]{12.93, 100.87}},
            {"Bali", new[]{-8.34, 115.09}}, {"Surabaya", new[]{-7.25, 112.75}},
            {"Bandung", new[]{-6.91, 107.61}}, {"Medan", new[]{3.59, 98.67}},
            {"Cebu", new[]{10.31, 123.89}}, {"Davao", new[]{7.19, 125.46}},
            {"Penang", new[]{5.42, 100.33}}, {"Johor Bahru", new[]{1.49, 103.74}},
            {"Siem Reap", new[]{13.36, 103.86}}, {"Luang Prabang", new[]{19.89, 102.14}},

            // ===== EAST ASIA =====
            {"Tokyo", new[]{35.68, 139.69}}, {"Seoul", new[]{37.57, 126.98}},
            {"Beijing", new[]{39.90, 116.40}}, {"Shanghai", new[]{31.23, 121.47}},
            {"Hong Kong", new[]{22.32, 114.17}}, {"Taipei", new[]{25.03, 121.57}},
            {"Osaka", new[]{34.69, 135.50}}, {"Kyoto", new[]{35.01, 135.77}},
            {"Yokohama", new[]{35.44, 139.64}}, {"Nagoya", new[]{35.18, 136.91}},
            {"Sapporo", new[]{43.06, 141.35}}, {"Fukuoka", new[]{33.59, 130.40}},
            {"Busan", new[]{35.18, 129.08}}, {"Incheon", new[]{37.46, 126.70}},
            {"Guangzhou", new[]{23.13, 113.26}}, {"Shenzhen", new[]{22.54, 114.06}},
            {"Chengdu", new[]{30.57, 104.07}}, {"Wuhan", new[]{30.59, 114.31}},
            {"Chongqing", new[]{29.43, 106.91}}, {"Hangzhou", new[]{30.27, 120.15}},
            {"Nanjing", new[]{32.06, 118.80}}, {"Tianjin", new[]{39.13, 117.20}},
            {"Xi'an", new[]{34.26, 108.94}}, {"Macau", new[]{22.20, 113.54}},
            {"Kaohsiung", new[]{22.63, 120.30}}, {"Ulaanbaatar", new[]{47.89, 106.91}},
            {"Pyongyang", new[]{39.02, 125.75}},

            // ===== SOUTH ASIA =====
            {"Mumbai", new[]{19.08, 72.88}}, {"Delhi", new[]{28.61, 77.21}},
            {"New Delhi", new[]{28.61, 77.21}}, {"Bangalore", new[]{12.97, 77.59}},
            {"Kolkata", new[]{22.57, 88.36}}, {"Chennai", new[]{13.08, 80.27}},
            {"Hyderabad", new[]{17.38, 78.49}}, {"Ahmedabad", new[]{23.02, 72.57}},
            {"Pune", new[]{18.52, 73.86}}, {"Jaipur", new[]{26.92, 75.79}},
            {"Colombo", new[]{6.93, 79.85}}, {"Dhaka", new[]{23.81, 90.41}},
            {"Kathmandu", new[]{27.72, 85.32}}, {"Islamabad", new[]{33.69, 73.04}},
            {"Karachi", new[]{24.86, 67.01}}, {"Lahore", new[]{31.55, 74.35}},
            {"Thimphu", new[]{27.47, 89.64}}, {"Male", new[]{4.18, 73.51}},
            {"Goa", new[]{15.30, 74.00}},

            // ===== MIDDLE EAST =====
            {"Dubai", new[]{25.20, 55.27}}, {"Abu Dhabi", new[]{24.45, 54.65}},
            {"Riyadh", new[]{24.69, 46.72}}, {"Jeddah", new[]{21.49, 39.19}},
            {"Mecca", new[]{21.39, 39.86}}, {"Medina", new[]{24.47, 39.61}},
            {"Doha", new[]{25.29, 51.53}}, {"Kuwait City", new[]{29.38, 47.99}},
            {"Manama", new[]{26.23, 50.59}}, {"Muscat", new[]{23.59, 58.59}},
            {"Tehran", new[]{35.69, 51.39}}, {"Baghdad", new[]{33.31, 44.37}},
            {"Amman", new[]{31.95, 35.93}}, {"Beirut", new[]{33.89, 35.50}},
            {"Damascus", new[]{33.51, 36.29}}, {"Jerusalem", new[]{31.77, 35.23}},
            {"Tel Aviv", new[]{32.09, 34.77}}, {"Ankara", new[]{39.93, 32.85}},
            {"Istanbul", new[]{41.01, 28.98}}, {"Baku", new[]{40.41, 49.87}},
            {"Tbilisi", new[]{41.69, 44.80}}, {"Yerevan", new[]{40.18, 44.51}},

            // ===== EUROPE =====
            {"London", new[]{51.51, -0.13}}, {"Paris", new[]{48.86, 2.35}},
            {"Berlin", new[]{52.52, 13.41}}, {"Madrid", new[]{40.42, -3.70}},
            {"Rome", new[]{41.90, 12.50}}, {"Amsterdam", new[]{52.37, 4.90}},
            {"Brussels", new[]{50.85, 4.35}}, {"Vienna", new[]{48.21, 16.37}},
            {"Zurich", new[]{47.38, 8.54}}, {"Geneva", new[]{46.20, 6.14}},
            {"Munich", new[]{48.14, 11.58}}, {"Frankfurt", new[]{50.11, 8.68}},
            {"Hamburg", new[]{53.55, 9.99}}, {"Barcelona", new[]{41.39, 2.17}},
            {"Milan", new[]{45.46, 9.19}}, {"Naples", new[]{40.85, 14.27}},
            {"Florence", new[]{43.77, 11.25}}, {"Venice", new[]{45.44, 12.32}},
            {"Lisbon", new[]{38.72, -9.14}}, {"Porto", new[]{41.15, -8.61}},
            {"Athens", new[]{37.98, 23.73}}, {"Dublin", new[]{53.35, -6.26}},
            {"Edinburgh", new[]{55.95, -3.19}}, {"Manchester", new[]{53.48, -2.24}},
            {"Stockholm", new[]{59.33, 18.07}}, {"Oslo", new[]{59.91, 10.75}},
            {"Copenhagen", new[]{55.68, 12.57}}, {"Helsinki", new[]{60.17, 24.94}},
            {"Warsaw", new[]{52.23, 21.01}}, {"Prague", new[]{50.08, 14.44}},
            {"Budapest", new[]{47.50, 19.04}}, {"Bucharest", new[]{44.43, 26.10}},
            {"Moscow", new[]{55.76, 37.62}}, {"Saint Petersburg", new[]{59.93, 30.32}},
            {"Kyiv", new[]{50.45, 30.52}}, {"Kiev", new[]{50.45, 30.52}},
            {"Sofia", new[]{42.70, 23.32}}, {"Belgrade", new[]{44.79, 20.47}},
            {"Zagreb", new[]{45.81, 15.98}}, {"Ljubljana", new[]{46.06, 14.51}},
            {"Bratislava", new[]{48.15, 17.11}}, {"Riga", new[]{56.95, 24.11}},
            {"Tallinn", new[]{59.44, 24.75}}, {"Vilnius", new[]{54.69, 25.28}},
            {"Reykjavik", new[]{64.15, -21.94}}, {"Monaco", new[]{43.73, 7.42}},
            {"Luxembourg", new[]{49.61, 6.13}},

            // ===== NORTH AMERICA =====
            {"New York", new[]{40.71, -74.01}}, {"Los Angeles", new[]{34.05, -118.24}},
            {"Chicago", new[]{41.88, -87.63}}, {"Houston", new[]{29.76, -95.37}},
            {"Phoenix", new[]{33.45, -112.07}}, {"San Francisco", new[]{37.77, -122.42}},
            {"Seattle", new[]{47.61, -122.33}}, {"Miami", new[]{25.76, -80.19}},
            {"Boston", new[]{42.36, -71.06}}, {"Atlanta", new[]{33.75, -84.39}},
            {"Denver", new[]{39.74, -104.98}}, {"Dallas", new[]{32.78, -96.80}},
            {"Las Vegas", new[]{36.17, -115.14}}, {"San Diego", new[]{32.72, -117.16}},
            {"Washington", new[]{38.91, -77.04}}, {"Philadelphia", new[]{39.95, -75.17}},
            {"Detroit", new[]{42.33, -83.05}}, {"Minneapolis", new[]{44.98, -93.27}},
            {"Portland", new[]{45.52, -122.68}}, {"Nashville", new[]{36.16, -86.78}},
            {"Toronto", new[]{43.65, -79.38}}, {"Montreal", new[]{45.50, -73.57}},
            {"Vancouver", new[]{49.28, -123.12}}, {"Ottawa", new[]{45.42, -75.70}},
            {"Calgary", new[]{51.05, -114.07}}, {"Mexico City", new[]{19.43, -99.13}},
            {"Cancun", new[]{21.16, -86.85}}, {"Havana", new[]{23.11, -82.37}},
            {"San Juan", new[]{18.47, -66.11}}, {"Panama City", new[]{8.98, -79.52}},
            {"San Jose CR", new[]{9.93, -84.08}}, {"Guatemala City", new[]{14.63, -90.51}},
            {"Honolulu", new[]{21.31, -157.86}}, {"Anchorage", new[]{61.22, -149.90}},

            // ===== SOUTH AMERICA =====
            {"Sao Paulo", new[]{-23.55, -46.63}}, {"Rio de Janeiro", new[]{-22.91, -43.17}},
            {"Buenos Aires", new[]{-34.60, -58.38}}, {"Lima", new[]{-12.05, -77.04}},
            {"Bogota", new[]{4.71, -74.07}}, {"Santiago", new[]{-33.45, -70.67}},
            {"Caracas", new[]{10.48, -66.90}}, {"Quito", new[]{-0.18, -78.47}},
            {"Montevideo", new[]{-34.88, -56.17}}, {"Medellin", new[]{6.25, -75.56}},
            {"Brasilia", new[]{-15.79, -47.88}}, {"Cartagena", new[]{10.39, -75.51}},
            {"Cusco", new[]{-13.53, -71.97}}, {"La Paz", new[]{-16.50, -68.15}},
            {"Asuncion", new[]{-25.26, -57.58}}, {"Georgetown", new[]{6.80, -58.16}},
            {"Guayaquil", new[]{-2.17, -79.92}},

            // ===== AFRICA =====
            {"Cairo", new[]{30.04, 31.24}}, {"Lagos", new[]{6.52, 3.38}},
            {"Nairobi", new[]{-1.29, 36.82}}, {"Johannesburg", new[]{-26.20, 28.05}},
            {"Cape Town", new[]{-33.93, 18.42}}, {"Casablanca", new[]{33.57, -7.59}},
            {"Addis Ababa", new[]{9.02, 38.75}}, {"Accra", new[]{5.60, -0.19}},
            {"Dakar", new[]{14.72, -17.47}}, {"Dar es Salaam", new[]{-6.79, 39.28}},
            {"Kampala", new[]{0.31, 32.58}}, {"Abuja", new[]{9.06, 7.49}},
            {"Kinshasa", new[]{-4.32, 15.31}}, {"Luanda", new[]{-8.84, 13.23}},
            {"Algiers", new[]{36.74, 3.06}}, {"Tunis", new[]{36.81, 10.18}},
            {"Marrakech", new[]{31.63, -8.01}}, {"Zanzibar", new[]{-6.16, 39.19}},
            {"Kigali", new[]{-1.94, 30.06}}, {"Maputo", new[]{-25.97, 32.57}},
            {"Windhoek", new[]{-22.56, 17.08}}, {"Harare", new[]{-17.83, 31.05}},

            // ===== OCEANIA =====
            {"Sydney", new[]{-33.87, 151.21}}, {"Melbourne", new[]{-37.81, 144.96}},
            {"Brisbane", new[]{-27.47, 153.03}}, {"Perth", new[]{-31.95, 115.86}},
            {"Auckland", new[]{-36.85, 174.76}}, {"Wellington", new[]{-41.29, 174.78}},
            {"Christchurch", new[]{-43.53, 172.64}}, {"Adelaide", new[]{-34.93, 138.60}},
            {"Gold Coast", new[]{-28.02, 153.43}}, {"Canberra", new[]{-35.28, 149.13}},
            {"Fiji", new[]{-17.71, 177.99}}, {"Suva", new[]{-18.14, 178.44}},
            {"Hobart", new[]{-42.88, 147.33}}, {"Darwin", new[]{-12.46, 130.84}},
        };

        private void WeatherCity_Click(object sender, RoutedEventArgs e)
        {
            string city = CityInput.Text.Trim();
            if (string.IsNullOrEmpty(city)) return;

            double[] coords;
            if (CityDB.TryGetValue(city, out coords))
            {
                SaveSetting("WeatherLat", coords[0]);
                SaveSetting("WeatherLon", coords[1]);
                WeatherLocationInfo.Text = city + " (" +
                    coords[0].ToString("F2") + ", " + coords[1].ToString("F2") + ")";
                MessageBox.Show("Location saved!", "Success", MessageBoxButton.OK);
            }
            else
            {
                // Try partial match
                string lower = city.ToLower();
                foreach (var kv in CityDB)
                {
                    if (kv.Key.ToLower().Contains(lower) || lower.Contains(kv.Key.ToLower()))
                    {
                        SaveSetting("WeatherLat", kv.Value[0]);
                        SaveSetting("WeatherLon", kv.Value[1]);
                        WeatherLocationInfo.Text = kv.Key + " (" +
                            kv.Value[0].ToString("F2") + ", " + kv.Value[1].ToString("F2") + ")";
                        MessageBox.Show("Found: " + kv.Key, "Success", MessageBoxButton.OK);
                        return;
                    }
                }
                MessageBox.Show("City not in database.\nTry GPS or use a major city name.",
                    "Not Found", MessageBoxButton.OK);
            }
        }

        private double ParseJsonVal(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search);
            if (idx < 0) return 0;
            idx += search.Length;
            // Skip optional quote (Nominatim returns "lat":"16.06" as string)
            bool quoted = idx < json.Length && json[idx] == '"';
            if (quoted) idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;
            double val;
            double.TryParse(json.Substring(idx, end - idx),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out val);
            return val;
        }

        #endregion

        #region Countdown

        private void CountdownToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ShowCountdown", true);
        }

        private void CountdownToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("ShowCountdown", false);
        }

        private void SaveCountdown_Click(object sender, RoutedEventArgs e)
        {
            SaveSetting("CountdownName", CountdownNameInput.Text.Trim());
            DateTime target;
            if (DateTime.TryParse(CountdownDateInput.Text.Trim(), out target))
            {
                SaveSetting("CountdownTarget", target);
                MessageBox.Show("Countdown saved!", "Saved", MessageBoxButton.OK);
            }
            else
            {
                MessageBox.Show("Invalid date. Use yyyy-MM-dd", "Error", MessageBoxButton.OK);
            }
        }

        #endregion

        #region Depth Effect

        private void DepthToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("UseDepthEffect", true);
        }

        private void DepthToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("UseDepthEffect", false);
        }

        private void ChooseForeground_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.PickSingleFileAndContinue();
        }

        #endregion

        #region My Sets

        private static readonly string[] SetKeys = { "ClockStyle", "ClockPosition", "ClockHAlign",
            "ClockColor", "ClockBlend", "ClockSize",
            "ShowWeather", "ShowCountdown", "UseDepthEffect", "bIsAnimOn" };

        private void SaveSet(int setNum)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            foreach (var key in SetKeys)
            {
                if (s.Contains(key))
                    s["Set" + setNum + "_" + key] = s[key];
            }
            s.Save();
            MessageBox.Show("Set " + setNum + " saved!", "My Sets", MessageBoxButton.OK);
        }

        private void LoadSet(int setNum)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            bool found = false;
            foreach (var key in SetKeys)
            {
                string setKey = "Set" + setNum + "_" + key;
                if (s.Contains(setKey))
                {
                    s[key] = s[setKey];
                    found = true;
                }
            }
            if (found)
            {
                s.Save();
                isLoading = true;
                LoadSettings();
                isLoading = false;
                MessageBox.Show("Set " + setNum + " loaded!", "My Sets", MessageBoxButton.OK);
            }
            else
            {
                // No saved set — offer to save
                if (MessageBox.Show("No set saved. Save current settings?", "My Sets",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    SaveSet(setNum);
                }
            }
        }

        private void LoadSet1_Click(object sender, RoutedEventArgs e) { LoadSet(1); }
        private void LoadSet2_Click(object sender, RoutedEventArgs e) { LoadSet(2); }
        private void LoadSet3_Click(object sender, RoutedEventArgs e) { LoadSet(3); }

        #endregion

        #region Navigation

        private void About_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("/Pages/About.xaml", UriKind.Relative));
        }

        #endregion

        #region Settings Storage

        private void LoadSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            PasswordToggle.IsChecked = GetSetting<bool>(s, "bIsPasswordEnabled", false);
            PatternToggle.IsChecked = GetSetting<bool>(s, "bIsPatternOn", false);
            AnimToggle.IsChecked = GetSetting<bool>(s, "bIsAnimOn", true);

            // Clock style
            int clockStyle = GetSetting<int>(s, "ClockStyle", 0);
            if (clockStyle >= 0 && clockStyle < ClockStylePicker.Items.Count)
                ClockStylePicker.SelectedIndex = clockStyle;

            // Clock position (default: 1 = Center)
            int clockPos = GetSetting<int>(s, "ClockPosition", 1);
            if (clockPos >= 0 && clockPos < ClockPositionPicker.Items.Count)
                ClockPositionPicker.SelectedIndex = clockPos;

            // Horizontal alignment (default: 1 = Center)
            int hAlign = GetSetting<int>(s, "ClockHAlign", 1);
            if (hAlign >= 0 && hAlign < ClockHAlignPicker.Items.Count)
                ClockHAlignPicker.SelectedIndex = hAlign;

            // Clock color (default: 0 = White)
            int clockColor = GetSetting<int>(s, "ClockColor", 0);
            if (clockColor >= 0 && clockColor < ClockColorPicker.Items.Count)
                ClockColorPicker.SelectedIndex = clockColor;

            // Clock blend (default: 0 = None)
            int clockBlend = GetSetting<int>(s, "ClockBlend", 0);
            if (clockBlend >= 0 && clockBlend < ClockBlendPicker.Items.Count)
                ClockBlendPicker.SelectedIndex = clockBlend;

            // Clock size (default: 2 = L/105)
            int clockSize = GetSetting<int>(s, "ClockSize", 2);
            if (clockSize >= 0 && clockSize < ClockSizePicker.Items.Count)
                ClockSizePicker.SelectedIndex = clockSize;

            // Owner info
            string ownerInfo = GetSetting<string>(s, "OwnerInfo", "");
            OwnerInfoInput.Text = ownerInfo;

            // Weather
            WeatherToggle.IsChecked = GetSetting<bool>(s, "ShowWeather", false);
            double wLat = GetSetting<double>(s, "WeatherLat", 0);
            double wLon = GetSetting<double>(s, "WeatherLon", 0);
            if (wLat != 0 || wLon != 0)
                WeatherLocationInfo.Text = wLat.ToString("F2") + ", " + wLon.ToString("F2");

            // Countdown
            CountdownToggle.IsChecked = GetSetting<bool>(s, "ShowCountdown", false);
            CountdownNameInput.Text = GetSetting<string>(s, "CountdownName", "");
            if (s.Contains("CountdownTarget"))
                CountdownDateInput.Text = ((DateTime)s["CountdownTarget"]).ToString("yyyy-MM-dd");

            // Depth
            DepthToggle.IsChecked = GetSetting<bool>(s, "UseDepthEffect", false);

            if (PasswordToggle.IsChecked == true)
                PinSetupPanel.Visibility = Visibility.Collapsed;
        }

        private T GetSetting<T>(IsolatedStorageSettings settings, string key, T defaultValue)
        {
            if (settings.Contains(key))
                return (T)settings[key];
            return defaultValue;
        }

        private void SaveSetting(string key, object value)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            s[key] = value;
            s.Save();
        }

        #endregion
    }
}
