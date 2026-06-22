using System;
using System.IO.IsolatedStorage;
using System.Windows;
using Microsoft.Phone.Controls;

namespace HyperOS.Pages
{
    public partial class AdvancedSettings : PhoneApplicationPage
    {
        private bool isLoading = true;

        public AdvancedSettings()
        {
            InitializeComponent();
        }

        private void AdvancedSettings_Loaded(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            LoadSettings();
            isLoading = false;
        }

        private void LoadSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            // Animation set
            int animSet = 0;
            if (s.Contains("AnimSet"))
                animSet = (int)s["AnimSet"];
            if (animSet >= 0 && animSet < AnimPicker.Items.Count)
                AnimPicker.SelectedIndex = animSet;

            // City
            if (s.Contains("cityName"))
                CityInput.Text = (string)s["cityName"];

            // WiFi only
            WiFiOnlyToggle.IsChecked = s.Contains("bIsWiFOnly") && (bool)s["bIsWiFOnly"];
        }

        private void AnimPicker_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("AnimSet", AnimPicker.SelectedIndex);
        }

        private void SaveLocation_Click(object sender, RoutedEventArgs e)
        {
            string city = CityInput.Text.Trim();
            if (!string.IsNullOrEmpty(city))
            {
                SaveSetting("cityName", city);
                MessageBox.Show("Location saved: " + city, "Saved",
                    MessageBoxButton.OK);
            }
        }

        private void WiFiOnlyToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsWiFOnly", true);
        }

        private void WiFiOnlyToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            SaveSetting("bIsWiFOnly", false);
        }

        private void SaveSetting(string key, object value)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            s[key] = value;
            s.Save();
        }
    }
}
