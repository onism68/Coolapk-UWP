﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using InAppNotify = Microsoft.Toolkit.Uwp.UI.Controls.InAppNotification;

namespace CoolapkUWP.Helpers
{
    public enum ImageType
    {
        SmallImage,
        OriginImage,
        SmallAvatar,
        BigAvatar,
        Icon,
        Captcha,
    }

    internal static class ImageCacheHelper
    {
        private static readonly BitmapImage whiteNoPicMode = new BitmapImage(new Uri("ms-appx:/Assets/img_placeholder.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 };
        private static readonly BitmapImage darkNoPicMode = new BitmapImage(new Uri("ms-appx:/Assets/img_placeholder_night.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 };
        private static readonly Dictionary<ImageType, StorageFolder> folders = new Dictionary<ImageType, StorageFolder>();
        internal static BitmapImage NoPic { get => SettingsHelper.Get<bool>(SettingsHelper.IsDarkMode) ? darkNoPicMode : whiteNoPicMode; }

        internal static async Task<StorageFolder> GetFolderAsync(ImageType type)
        {
            StorageFolder folder;
            if (folders.ContainsKey(type))
            {
                folder = folders[type];
            }
            else
            {
                folder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(type.ToString()) as StorageFolder;
                if (folder is null)
                {
                    folder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(type.ToString(), CreationCollisionOption.OpenIfExists);
                }
                if (!folders.ContainsKey(type))
                {
                    folders.Add(type, folder);
                }
            }
            return folder;
        }

        internal static async Task<BitmapImage> GetImageAsync(ImageType type, string url, Pages.ImageModel model = null, InAppNotify notify = null)
        {
            if (string.IsNullOrEmpty(url)) { return null; }

            if (url.IndexOf("ms-appx") == 0)
            {
                return new BitmapImage(new Uri(url));
            }
            else if (model == null && SettingsHelper.Get<bool>(SettingsHelper.IsNoPicsMode))
            {
                return NoPic;
            }
            else
            {
                var fileName = Core.Helpers.Utils.GetMD5(url);
                var folder = await GetFolderAsync(type);
                var item = await folder.TryGetItemAsync(fileName);
                if (type == ImageType.SmallImage || type == ImageType.SmallAvatar)
                {
                    url += ".s.jpg";
                }
                var forceGetPic = model != null;
                if (item is null)
                {
                    StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                    return await DownloadImageAsync(file, url, model, notify);
                }
                else
                {
                    return item is StorageFile file ? GetLocalImageAsync(file.Path, forceGetPic) : null;
                }
            }
        }

        private static BitmapImage GetLocalImageAsync(string filename, bool forceGetPic)
        {
            try
            {
                return (filename is null || (!forceGetPic && SettingsHelper.Get<bool>(SettingsHelper.IsNoPicsMode))) ? NoPic : new BitmapImage(new Uri(filename));
            }
            catch
            {
                return NoPic;
            }
        }

        private static async Task<BitmapImage> DownloadImageAsync(StorageFile file, string url, Pages.ImageModel model, InAppNotify notify)
        {
            try
            {
                if (model != null)
                {
                    model.IsProgressRingActived = true;
                }
                using (var hc = new HttpClient())
                using (var stream = await hc.GetStreamAsync(new Uri(url)))
                using (var fs = await file.OpenStreamForWriteAsync())
                {
                    await stream.CopyToAsync(fs);
                }
                return new BitmapImage(new Uri(file.Path));
            }
            catch (FileLoadException) { return NoPic; }
            catch (HttpRequestException)
            {
                var str = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString("ImageLoadError");
                if (notify == null)
                {
                    UIHelper.ShowMessage(str);
                }
                else
                {
                    notify.Show(str, UIHelper.duration);
                }
                return NoPic;
            }
            finally
            {
                if (model != null)
                {
                    model.IsProgressRingActived = false;
                }
            }
        }

        internal static async Task CleanCacheAsync()
        {
            for (int i = 0; i < 6; i++)
            {
                var type = (ImageType)i;
                await (await GetFolderAsync(type)).DeleteAsync();
                await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(type.ToString());
            }
        }
    }
}