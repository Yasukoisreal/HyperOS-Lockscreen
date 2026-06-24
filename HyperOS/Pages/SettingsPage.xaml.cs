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

        private void Security_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("/Pages/About.xaml", UriKind.Relative));
        }

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
