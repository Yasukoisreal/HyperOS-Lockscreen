using System;
using System.IO.IsolatedStorage;
using System.Windows;
using Microsoft.Phone.Controls;
using Windows.Phone.System.LockScreenExtensibility;

namespace HyperOS.Pages
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        private bool isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            var s = IsolatedStorageSettings.ApplicationSettings;

            // Lock screen
            try { LockToggle.IsChecked = ExtensibilityApp.IsLockScreenApplicationRegistered(); }
            catch { }

            // Security
            bool pinOn = Get(s, "bIsPasswordEnabled", false);
            bool patternOn = Get(s, "bIsPatternOn", false);
            PinToggle.IsChecked = pinOn;
            PatternToggle.IsChecked = patternOn;
            PinPanel.Visibility = pinOn ? Visibility.Visible : Visibility.Collapsed;
            if (pinOn)
                PinBox.Text = Get<string>(s, "sPassword", "");
            if (patternOn)
                PatternHint.Text = "✅ Pattern đã được thiết lập trên màn hình khoá";

            // Owner info
            OwnerInfoBox.Text = Get<string>(s, "OwnerInfo", "");

            // Animations
            AnimToggle.IsChecked = Get(s, "bIsAnimOn", true);

            // API Key
            ApiKeyBox.Text = Get<string>(s, "RemoveBgApiKey", "");

            isLoading = false;
        }

        #region Helpers

        private T Get<T>(IsolatedStorageSettings s, string key, T def)
        {
            return s.Contains(key) ? (T)s[key] : def;
        }

        private void Save(string key, object val)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            s[key] = val;
            s.Save();
        }

        #endregion

        #region Handlers

        private void Back_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void LockToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            try
            {
                if (LockToggle.IsChecked == true)
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (store.FileExists("Background.jpg"))
                        {
                            if (!ExtensibilityApp.IsLockScreenApplicationRegistered())
                                ExtensibilityApp.RegisterLockScreenApplication();
                        }
                        else
                        {
                            MessageBox.Show("Please choose a background image first.",
                                "Background Required", MessageBoxButton.OK);
                            LockToggle.IsChecked = false;
                        }
                    }
                }
                else
                {
                    if (ExtensibilityApp.IsLockScreenApplicationRegistered())
                    {
                        var result = MessageBox.Show(
                            "Remove HyperOS as your live lock screen?",
                            "Remove", MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK)
                            ExtensibilityApp.UnregisterLockScreenApplication();
                        else
                            LockToggle.IsChecked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK);
            }
        }

        #endregion

        #region Security

        private void PinToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            bool on = PinToggle.IsChecked == true;

            if (on)
            {
                // Disable pattern if enabling PIN
                if (PatternToggle.IsChecked == true)
                {
                    isLoading = true;
                    PatternToggle.IsChecked = false;
                    isLoading = false;
                    Save("bIsPatternOn", false);
                    PatternHint.Text = "";
                }
                PinPanel.Visibility = Visibility.Visible;
                PinBox.Text = "";
                PinBox.Focus();
            }
            else
            {
                Save("bIsPasswordEnabled", false);
                Save("sPassword", "");
                PinPanel.Visibility = Visibility.Collapsed;
                SecurityStatus.Text = "🔓 PIN đã tắt";
            }
        }

        private void SavePin_Click(object sender, RoutedEventArgs e)
        {
            string pin = PinBox.Text.Trim();
            if (pin.Length != 4)
            {
                MessageBox.Show("PIN phải có đúng 4 chữ số.", "Sai định dạng", MessageBoxButton.OK);
                return;
            }
            Save("sPassword", pin);
            Save("bIsPasswordEnabled", true);
            SecurityStatus.Text = "🔒 PIN đã lưu";
        }

        private void PatternToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            bool on = PatternToggle.IsChecked == true;

            if (on)
            {
                // Disable PIN if enabling Pattern
                if (PinToggle.IsChecked == true)
                {
                    isLoading = true;
                    PinToggle.IsChecked = false;
                    isLoading = false;
                    Save("bIsPasswordEnabled", false);
                    PinPanel.Visibility = Visibility.Collapsed;
                }
                Save("bIsPatternOn", true);
                PatternHint.Text = "⬆ Vẽ pattern trên màn hình khoá để thiết lập";
                SecurityStatus.Text = "🔒 Pattern lock đã bật";
            }
            else
            {
                Save("bIsPatternOn", false);
                Save("AppPatternToMatch", "");
                PatternHint.Text = "";
                SecurityStatus.Text = "🔓 Pattern lock đã tắt";
            }
        }

        #endregion

        #region Other Handlers

        private void SaveOwner_Click(object sender, RoutedEventArgs e)
        {
            string info = OwnerInfoBox.Text.Trim();
            Save("OwnerInfo", info);
            MessageBox.Show(
                string.IsNullOrEmpty(info) ? "Owner info cleared." : "Owner info saved!",
                "Saved", MessageBoxButton.OK);
        }

        private void AnimToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            Save("bIsAnimOn", AnimToggle.IsChecked == true);
        }

        private void ApiKey_LostFocus(object sender, RoutedEventArgs e)
        {
            Save("RemoveBgApiKey", ApiKeyBox.Text.Trim());
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("/Pages/About.xaml", UriKind.Relative));
        }

        #endregion
    }
}
