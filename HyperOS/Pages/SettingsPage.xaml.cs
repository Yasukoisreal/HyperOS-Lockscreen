using System;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Media;
using Microsoft.Phone.Controls;
using Windows.Phone.System.LockScreenExtensibility;

namespace HyperOS.Pages
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        private bool isLoading = true;
        private bool isSettingNewPattern = false;

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

            string pattern = Get<string>(s, "AppPatternToMatch", "");
            bool hasPattern = !string.IsNullOrEmpty(pattern);
            PatternPanel.Visibility = (patternOn && hasPattern) ? Visibility.Visible : Visibility.Collapsed;
            if (patternOn)
            {
                PatternHint.Text = hasPattern
                    ? "✅ Pattern is configured for the lock screen"
                    : "⚠️ No pattern saved. Tap toggle to set pattern";
            }
            else
            {
                PatternHint.Text = "";
            }

            if (pinOn)
                SecurityStatus.Text = "🔒 PIN enabled";
            else if (patternOn && hasPattern)
                SecurityStatus.Text = "🔒 Pattern lock enabled";
            else
                SecurityStatus.Text = "🔓 No security";

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

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            if (PatternSetupDialog.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                ClosePatternSetupDialog(false);
                return;
            }
            base.OnBackKeyPress(e);
        }

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
                    PatternPanel.Visibility = Visibility.Collapsed;
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
                SecurityStatus.Text = "🔓 PIN disabled";
            }
        }

        private void SavePin_Click(object sender, RoutedEventArgs e)
        {
            string pin = PinBox.Text.Trim();
            bool valid = pin.Length == 4;
            if (valid)
            {
                for (int i = 0; i < pin.Length; i++)
                {
                    char c = pin[i];
                    if (c < '0' || c > '9')
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid)
            {
                MessageBox.Show("PIN must be exactly 4 digits (0-9).", "Invalid format", MessageBoxButton.OK);
                return;
            }
            Save("sPassword", pin);
            Save("bIsPasswordEnabled", true);
            SecurityStatus.Text = "🔒 PIN saved";
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
                    Save("sPassword", "");
                    PinPanel.Visibility = Visibility.Collapsed;
                }

                var s = IsolatedStorageSettings.ApplicationSettings;
                string pattern = Get<string>(s, "AppPatternToMatch", "");
                if (string.IsNullOrEmpty(pattern))
                {
                    OpenPatternSetupDialog(true);
                }
                else
                {
                    Save("bIsPatternOn", true);
                    PatternPanel.Visibility = Visibility.Visible;
                    PatternHint.Text = "✅ Pattern is configured for the lock screen";
                    SecurityStatus.Text = "🔒 Pattern lock enabled";
                }
            }
            else
            {
                Save("bIsPatternOn", false);
                PatternPanel.Visibility = Visibility.Collapsed;
                PatternHint.Text = "";
                SecurityStatus.Text = "🔓 No security";
            }
        }

        private void ChangePattern_Click(object sender, RoutedEventArgs e)
        {
            OpenPatternSetupDialog(false);
        }

        private void OpenPatternSetupDialog(bool isNewSetup)
        {
            isSettingNewPattern = isNewSetup;
            PatternSetupHintText.Text = "Draw pattern (connect at least 3 dots)";
            PatternSetupHintText.Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
            SetupPatternControl.Reset();
            PatternSetupDialog.Visibility = Visibility.Visible;
        }

        private void ClosePatternSetupDialog(bool patternSaved)
        {
            PatternSetupDialog.Visibility = Visibility.Collapsed;
            SetupPatternControl.Reset();

            if (!patternSaved && isSettingNewPattern)
            {
                isLoading = true;
                PatternToggle.IsChecked = false;
                isLoading = false;
                Save("bIsPatternOn", false);
                PatternPanel.Visibility = Visibility.Collapsed;
                PatternHint.Text = "";
            }
        }

        private void PatternSetupDialog_Cancel_Click(object sender, RoutedEventArgs e)
        {
            ClosePatternSetupDialog(false);
        }

        private void PatternSetupDialog_Cancel_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ClosePatternSetupDialog(false);
        }

        private void SetupPatternControl_RegistrationSuccess(object sender, EventArgs e)
        {
            Save("bIsPatternOn", true);
            PatternPanel.Visibility = Visibility.Visible;
            PatternHint.Text = "✅ Pattern is configured for the lock screen";
            SecurityStatus.Text = "🔒 Pattern lock enabled";
            ClosePatternSetupDialog(true);
            MessageBox.Show("Pattern lock has been set successfully!", "Pattern Saved", MessageBoxButton.OK);
        }

        private void SetupPatternControl_RegistrationInvalid(object sender, EventArgs e)
        {
            PatternSetupHintText.Text = "⚠️ Connect at least 3 dots to set pattern";
            PatternSetupHintText.Foreground = new SolidColorBrush(Colors.Red);
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
