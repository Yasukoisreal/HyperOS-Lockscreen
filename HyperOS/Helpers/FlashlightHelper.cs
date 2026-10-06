using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Devices;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Flashlight/Torch controller using Windows.Media.Capture and TorchControl.
    /// Based on Microsoft Tetra Lockscreen implementation.
    /// </summary>
    public static class FlashlightHelper
    {
        private static MediaCapture _mediaCapture;
        private static bool _isOn = false;
        private static bool _isProcessing = false;

        public static event Action<bool> StateChanged;

        public static bool IsOn
        {
            get { return _isOn; }
        }

        public static async Task<bool> ToggleAsync()
        {
            return await SetTorchAsync(!_isOn);
        }

        public static async Task<bool> SetTorchAsync(bool state)
        {
            if (_isProcessing) return _isOn;
            _isProcessing = true;

            try
            {
                if (state)
                {
                    if (_mediaCapture == null)
                    {
                        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                        var backCamera = devices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back)
                                        ?? devices.FirstOrDefault();
                        if (backCamera == null)
                        {
                            _isProcessing = false;
                            return false;
                        }

                        var settings = new MediaCaptureInitializationSettings
                        {
                            VideoDeviceId = backCamera.Id,
                            AudioDeviceId = string.Empty, // Avoid requesting microphone access
                            StreamingCaptureMode = StreamingCaptureMode.Video,
                            PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                        };

                        _mediaCapture = new MediaCapture();
                        await _mediaCapture.InitializeAsync(settings);
                    }

                    var torchControl = _mediaCapture.VideoDeviceController.TorchControl;
                    if (torchControl.Supported)
                    {
                        if (torchControl.PowerSupported)
                        {
                            torchControl.PowerPercent = 100f;
                        }
                        torchControl.Enabled = true;
                        _isOn = true;
                        NotifyStateChanged(true);
                        return true;
                    }
                    else
                    {
                        DisposeMediaCapture();
                        _isOn = false;
                        NotifyStateChanged(false);
                        return false;
                    }
                }
                else
                {
                    if (_mediaCapture != null)
                    {
                        var torchControl = _mediaCapture.VideoDeviceController.TorchControl;
                        if (torchControl.Supported)
                        {
                            torchControl.Enabled = false;
                        }
                        DisposeMediaCapture();
                    }
                    _isOn = false;
                    NotifyStateChanged(false);
                    return true;
                }
            }
            catch
            {
                DisposeMediaCapture();
                _isOn = false;
                NotifyStateChanged(false);
                return false;
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public static void TurnOff()
        {
            try
            {
                if (_mediaCapture != null)
                {
                    var torchControl = _mediaCapture.VideoDeviceController.TorchControl;
                    if (torchControl.Supported)
                    {
                        torchControl.Enabled = false;
                    }
                }
            }
            catch { }
            finally
            {
                DisposeMediaCapture();
                if (_isOn)
                {
                    _isOn = false;
                    NotifyStateChanged(false);
                }
            }
        }

        private static void NotifyStateChanged(bool isOn)
        {
            try
            {
                if (System.Windows.Deployment.Current != null && System.Windows.Deployment.Current.Dispatcher != null)
                {
                    System.Windows.Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try { StateChanged?.Invoke(isOn); } catch { }
                    });
                }
                else
                {
                    StateChanged?.Invoke(isOn);
                }
            }
            catch { }
        }

        private static void DisposeMediaCapture()
        {
            try
            {
                if (_mediaCapture != null)
                {
                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                }
            }
            catch { }
        }
    }
}
