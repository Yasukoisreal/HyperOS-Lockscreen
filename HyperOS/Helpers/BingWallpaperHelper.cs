using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Helper to download Bing Image of the Day wallpaper.
    /// Based on Microsoft Live Lock Screen BETA & Tetra implementation.
    /// </summary>
    public static class BingWallpaperHelper
    {
        private const string BING_ARCHIVE_URL = "http://www.bing.com/HPImageArchive.aspx?format=xml&idx=0&n=1&mbl=1&mkt=en-ww";
        private const string SAVED_BING_IMAGE_NAME = "BingDailyWallpaper.jpg";

        public static async Task<string> DownloadDailyWallpaperAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "HyperOS-LockScreen/1.0");
                    string xmlContent = await client.GetStringAsync(new Uri(BING_ARCHIVE_URL));
                    if (string.IsNullOrEmpty(xmlContent)) return null;

                    var doc = XElement.Parse(xmlContent);
                    var imageElem = doc.Element("image");
                    if (imageElem == null) return null;

                    var urlBaseElem = imageElem.Element("urlBase");
                    if (urlBaseElem == null) return null;

                    string urlBase = urlBaseElem.Value;
                    string fullImageUrl = "http://www.bing.com" + urlBase + "_1080x1920.jpg";

                    // Fallback to standard 1920x1080 if portrait not found
                    byte[] imageBytes = null;
                    try
                    {
                        imageBytes = await client.GetByteArrayAsync(new Uri(fullImageUrl));
                    }
                    catch
                    {
                        fullImageUrl = "http://www.bing.com" + urlBase + "_1920x1080.jpg";
                        imageBytes = await client.GetByteArrayAsync(new Uri(fullImageUrl));
                    }

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            if (store.FileExists(SAVED_BING_IMAGE_NAME))
                            {
                                store.DeleteFile(SAVED_BING_IMAGE_NAME);
                            }

                            using (var fileStream = store.OpenFile(SAVED_BING_IMAGE_NAME, FileMode.Create, FileAccess.Write))
                            {
                                await fileStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                            }
                        }
                        return SAVED_BING_IMAGE_NAME;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
