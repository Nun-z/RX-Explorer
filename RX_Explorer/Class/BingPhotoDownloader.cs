﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class BingPictureDownloader
    {
        public static async Task<StorageFile> UpdateBingPicture()
        {
            string Path = await GetDailyPhotoPath().ConfigureAwait(false);

            if ((await ApplicationData.Current.LocalFolder.TryGetItemAsync("BingDailyPicture.jpg")) is StorageFile ExistFile)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return ExistFile;
                    }

                    if (await CheckIfNeedToUpdate().ConfigureAwait(false))
                    {
                        StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg", CreationCollisionOption.GenerateUniqueName);

                        using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                        {
                            HttpWebRequest Request = WebRequest.CreateHttp(new Uri($"https://www.bing.com{Path}"));
                            Request.Timeout = 8000;
                            Request.ReadWriteTimeout = 6000;

                            using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                            using (Stream ResponseStream = Response.GetResponseStream())
                            {
                                await ResponseStream.CopyToAsync(TempFileStream).ConfigureAwait(false);
                            }

                            using (Stream FileStream = await ExistFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            using (MD5 MD5Alg1 = MD5.Create())
                            using (MD5 MD5Alg2 = MD5.Create())
                            {
                                Task<string> CalTask1 = MD5Alg1.GetHashAsync(FileStream);
                                Task<string> CalTask2 = MD5Alg2.GetHashAsync(TempFileStream);

                                string[] ResultArray = await Task.WhenAll(CalTask1, CalTask2).ConfigureAwait(false);

                                if (ResultArray[0] == ResultArray[1])
                                {
                                    return ExistFile;
                                }
                            }

                            TempFileStream.Seek(0, SeekOrigin.Begin);

                            using (StorageStreamTransaction Transaction = await ExistFile.OpenTransactedWriteAsync())
                            {
                                await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                                await Transaction.CommitAsync();
                            }
                        }

                        return ExistFile;
                    }
                    else
                    {
                        return ExistFile;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(UpdateBingPicture)}");
                    return ExistFile;
                }
            }
            else
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Path))
                    {
                        return null;
                    }

                    StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"BingDailyPicture_Cache_[{DateTime.Now:yyyy-MM-dd HH-mm-ss}].jpg", CreationCollisionOption.GenerateUniqueName);

                    using (Stream TempFileStream = (await TempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                    {
                        HttpWebRequest Request = WebRequest.CreateHttp(new Uri($"https://www.bing.com{Path}"));
                        Request.Timeout = 8000;
                        Request.ReadWriteTimeout = 6000;

                        using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                        using (Stream ResponseStream = Response.GetResponseStream())
                        {
                            await ResponseStream.CopyToAsync(TempFileStream).ConfigureAwait(false);
                        }

                        StorageFile BingDailyPictureFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("BingDailyPicture.jpg", CreationCollisionOption.ReplaceExisting);

                        using (StorageStreamTransaction Transaction = await BingDailyPictureFile.OpenTransactedWriteAsync())
                        {
                            TempFileStream.Seek(0, SeekOrigin.Begin);
                            await TempFileStream.CopyToAsync(Transaction.Stream.AsStreamForWrite()).ConfigureAwait(false);
                            await Transaction.CommitAsync();
                        }

                        return BingDailyPictureFile;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(UpdateBingPicture)}");
                    return null;
                }
            }
        }

        private static async Task<string> GetDailyPhotoPath()
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp(new Uri("http://cn.bing.com/HPImageArchive.aspx?idx=0&n=1"));
                Request.Timeout = 2000;
                Request.ReadWriteTimeout = 2000;

                using (WebResponse Response = await Request.GetResponseAsync().ConfigureAwait(false))
                using (Stream ResponseStream = Response.GetResponseStream())
                using (StreamReader Reader = new StreamReader(ResponseStream))
                {
                    string HtmlString = await Reader.ReadToEndAsync().ConfigureAwait(false);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(HtmlString);

                    if (Document.DocumentNode.SelectSingleNode("/images/image/url") is HtmlNode Node)
                    {
                        return Node.InnerText;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Network is not available");
                return string.Empty;
            }
        }

        public static async Task<bool> CheckIfNeedToUpdate()
        {
            try
            {
                FileSystemStorageItemBase TempFolder = await FileSystemStorageItemBase.OpenAsync(ApplicationData.Current.TemporaryFolder.Path, ItemFilters.Folder);
                List<FileSystemStorageItemBase> AllPreviousPictureList = TempFolder.GetChildrenItems(false, ItemFilters.File).Where((Item) => Item.Name.StartsWith("BingDailyPicture_Cache")).ToList();

                if (AllPreviousPictureList.All((Item) => DateTime.TryParseExact(Regex.Match(Item.Name, @"(?<=\[)(.+)(?=\])").Value, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime LastUpdateDate) && LastUpdateDate < DateTime.Now.Date))
                {
                    foreach (FileSystemStorageItemBase ToDelete in AllPreviousPictureList)
                    {
                        ToDelete.PermanentDelete();
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(CheckIfNeedToUpdate)}");
                return true;
            }
        }
    }
}
