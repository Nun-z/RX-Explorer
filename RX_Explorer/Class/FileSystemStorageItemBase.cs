﻿using NetworkAccess;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public class FileSystemStorageItemBase : INotifyPropertyChanged, IEquatable<FileSystemStorageItemBase>
    {
        /// <summary>
        /// 指示所包含的存储对象类型
        /// </summary>
        public StorageItemTypes StorageType { get; protected set; }

        /// <summary>
        /// 存储对象
        /// </summary>
        protected IStorageItem StorageItem { get; set; }

        /// <summary>
        /// 用于兼容WIN_Native_API所提供的路径
        /// </summary>
        protected string InternalPathString { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public virtual BitmapImage Thumbnail
        {
            get
            {
                if (Inner_Thumbnail != null)
                {
                    return Inner_Thumbnail;
                }
                else
                {
                    if (StorageType == StorageItemTypes.File)
                    {
                        return AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image : Const_File_Black_Image;
                    }
                    else
                    {
                        return Const_Folder_Image;
                    }
                }
            }
            protected set => Inner_Thumbnail = value;
        }

        private BitmapImage Inner_Thumbnail { get; set; }

        public WIN_Native_API.WIN32_FIND_DATA? RawStorageItemData { get; }

        protected static readonly BitmapImage Const_Folder_Image = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));

        protected static readonly BitmapImage Const_File_White_Image = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_White.png"));

        protected static readonly BitmapImage Const_File_Black_Image = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path, ItemFilters Filters = ItemFilters.File | ItemFilters.Folder)
        {
            if (System.IO.Path.GetPathRoot(Path) != Path && WIN_Native_API.GetStorageItem(Path, Filters) is FileSystemStorageItemBase Item)
            {
                return Item;
            }
            else
            {
                LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                if (Filters.HasFlag(ItemFilters.File))
                {
                    try
                    {
                        StorageFile File = await StorageFile.GetFileFromPathAsync(Path);
                        return new FileSystemStorageItemBase(File, await File.GetSizeRawDataAsync().ConfigureAwait(false), await File.GetThumbnailBitmapAsync().ConfigureAwait(false), File.DateCreated, await File.GetModifiedTimeAsync().ConfigureAwait(false));
                    }
                    catch
                    {
                        LogTracer.Log($"UWP storage API could not found file: \"{Path}\"");
                    }
                }

                if (Filters.HasFlag(ItemFilters.Folder))
                {
                    try
                    {
                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                        return new FileSystemStorageItemBase(Folder, Folder.DateCreated, await Folder.GetModifiedTimeAsync().ConfigureAwait(false));
                    }
                    catch
                    {
                        LogTracer.Log($"UWP storage API could not found folder: \"{Path}\"");
                    }
                }

                return null;
            }
        }

        public static async Task<FileSystemStorageItemBase> CreateAsync(string Path, StorageItemTypes ItemTypes, CreateOption Option)
        {
            switch (ItemTypes)
            {
                case StorageItemTypes.File:
                    {
                        if (WIN_Native_API.CreateFileFromPath(Path, Option, out string NewPath))
                        {
                            return await OpenAsync(NewPath, ItemFilters.File);
                        }
                        else
                        {
                            LogTracer.Log($"Native API could not create file: \"{Path}\", fall back to UWP storage API");

                            try
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return new FileSystemStorageItemBase(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(false), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(false), NewFile.DateCreated, await NewFile.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageItemBase(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(false), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(false), NewFile.DateCreated, await NewFile.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageItemBase(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(false), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(false), NewFile.DateCreated, await NewFile.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                            catch
                            {
                                LogTracer.Log($"UWP storage API could not create file: \"{Path}\"");
                                return null;
                            }
                        }
                    }

                case StorageItemTypes.Folder:
                    {
                        if (WIN_Native_API.CreateDirectoryFromPath(Path, Option, out string NewPath))
                        {
                            return await OpenAsync(NewPath, ItemFilters.Folder);
                        }
                        else
                        {
                            LogTracer.Log($"Native API could not create file: \"{Path}\", fall back to UWP storage API");

                            try
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return new FileSystemStorageItemBase(NewFolder, NewFolder.DateCreated, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageItemBase(NewFolder, NewFolder.DateCreated, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageItemBase(NewFolder, NewFolder.DateCreated, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(false));
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                            catch
                            {
                                LogTracer.Log($"UWP storage API could not create folder: \"{Path}\"");
                                return null;
                            }
                        }
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件</param>
        /// <param name="Size">大小</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItemBase(StorageFile Item, ulong Size, BitmapImage Thumbnail, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.File;

            SizeRaw = Size;

            CreationTimeRaw = CreationTime.ToLocalTime();
            ModifiedTimeRaw = ModifiedTime.ToLocalTime();

            this.Thumbnail = Thumbnail;
        }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件夹</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItemBase(StorageFolder Item, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.Folder;

            CreationTimeRaw = CreationTime.ToLocalTime();
            ModifiedTimeRaw = ModifiedTime.ToLocalTime();
        }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Data">WIN_Native_API所提供的数据</param>
        /// <param name="StorageType">指示存储类型</param>
        /// <param name="Path">路径</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItemBase(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime)
        {
            InternalPathString = Path;

            CreationTimeRaw = CreationTime.ToLocalTime();
            ModifiedTimeRaw = ModifiedTime.ToLocalTime();

            this.StorageType = StorageType;
            RawStorageItemData = Data;

            if (StorageType != StorageItemTypes.Folder)
            {
                SizeRaw = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;
            }
        }

        protected FileSystemStorageItemBase()
        {

        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 调用此方法以获得存储对象
        /// </summary>
        /// <returns></returns>
        public virtual async Task<IStorageItem> GetStorageItem()
        {
            try
            {
                if (StorageItem == null)
                {
                    if (WIN_Native_API.CheckExist(InternalPathString))
                    {
                        if (StorageType == StorageItemTypes.File)
                        {
                            return StorageItem = await StorageFile.GetFileFromPathAsync(InternalPathString);
                        }
                        else
                        {
                            return StorageItem = await StorageFolder.GetFolderFromPathAsync(InternalPathString);
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return StorageItem;
                }
            }
            catch
            {
                return null;
            }
        }

        public virtual FileStream GetFileStreamFromFile(AccessMode Mode)
        {
            if (StorageType == StorageItemTypes.File)
            {
                return WIN_Native_API.CreateFileStreamFromExistingPath(Path, Mode);
            }
            else
            {
                return null;
            }
        }

        public virtual async Task<IRandomAccessStream> GetRandomAccessStreamFromFileAsync(FileAccessMode Mode)
        {
            return await FileRandomAccessStream.OpenAsync(Path, Mode, StorageOpenOptions.AllowReadersAndWriters, FileOpenDisposition.OpenExisting);
        }

        public virtual List<FileSystemStorageItemBase> GetChildrenItems(bool IncludeHiddenItems, ItemFilters Filter = ItemFilters.File | ItemFilters.Folder)
        {
            if (StorageType == StorageItemTypes.Folder)
            {
                return WIN_Native_API.GetStorageItems(Path, IncludeHiddenItems, Filter);
            }
            else
            {
                return new List<FileSystemStorageItemBase>(0);
            }
        }

        public bool PermanentDelete()
        {
            return WIN_Native_API.DeleteFromPath(Path);
        }

        /// <summary>
        /// 加载并获取更多属性，例如缩略图，显示名称等
        /// </summary>
        /// <returns></returns>
        public async Task LoadMorePropertyAsync()
        {
            if (Inner_Thumbnail == null)
            {
                switch (SettingControl.ContentLoadMode)
                {
                    case LoadMode.None:
                        {
                            break;
                        }
                    case LoadMode.OnlyFile:
                        {
                            if (StorageType == StorageItemTypes.File)
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    await LoadMorePropertyCore().ConfigureAwait(true);
                                    OnPropertyChanged(nameof(Thumbnail));
                                    OnPropertyChanged(nameof(DisplayType));
                                });
                            }
                            break;
                        }
                    case LoadMode.FileAndFolder:
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                            {
                                await LoadMorePropertyCore().ConfigureAwait(true);
                                OnPropertyChanged(nameof(Thumbnail));
                                OnPropertyChanged(nameof(DisplayType));
                            });
                            break;
                        }
                }
            }
        }

        protected virtual async Task LoadMorePropertyCore()
        {
            if (await GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
            {
                Thumbnail = await Item.GetThumbnailBitmapAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 设置缩略图的透明度，用于表示文件的是否处于待移动或隐藏状态
        /// </summary>
        /// <param name="Status">状态</param>
        public virtual void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            switch (Status)
            {
                case ThumbnailStatus.Normal:
                    {
                        if (ThumbnailOpacity != 1d)
                        {
                            ThumbnailOpacity = 1d;
                        }
                        break;
                    }
                case ThumbnailStatus.ReduceOpacity:
                    {
                        if (ThumbnailOpacity != 0.5)
                        {
                            ThumbnailOpacity = 0.5;
                        }
                        break;
                    }
            }

            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        /// <summary>
        /// 用新路径的存储对象替代当前的FileSystemStorageItem的内容
        /// </summary>
        /// <param name="NewPath">新的路径</param>
        /// <returns></returns>
        public virtual async Task Replace(string NewPath)
        {
            try
            {
                if (WIN_Native_API.CheckType(NewPath) == StorageItemTypes.File)
                {
                    if (WIN_Native_API.GetStorageItem(NewPath, ItemFilters.File) is FileSystemStorageItemBase Item)
                    {
                        if (StorageItem != null)
                        {
                            StorageItem = await Item.GetStorageItem().ConfigureAwait(true);
                        }

                        StorageType = StorageItemTypes.File;

                        SizeRaw = Item.SizeRaw;
                        ModifiedTimeRaw = Item.ModifiedTimeRaw;
                        CreationTimeRaw = Item.CreationTimeRaw;
                        InternalPathString = NewPath;
                        Inner_Thumbnail = null;

                        await LoadMorePropertyAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        LogTracer.Log($"File not found or access deny when executing FileSystemStorageItemBase.Update, path: {NewPath}");
                    }
                }
                else
                {
                    if (WIN_Native_API.GetStorageItem(NewPath, ItemFilters.Folder) is FileSystemStorageItemBase Item)
                    {
                        if (StorageItem != null)
                        {
                            StorageItem = await Item.GetStorageItem().ConfigureAwait(true);
                        }

                        StorageType = StorageItemTypes.Folder;

                        ModifiedTimeRaw = Item.ModifiedTimeRaw;
                        CreationTimeRaw = Item.CreationTimeRaw;
                        InternalPathString = NewPath;
                        Inner_Thumbnail = null;

                        await LoadMorePropertyAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        LogTracer.Log($"Folder not found or access deny when executing FileSystemStorageItemBase.Update, path: {NewPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when executing FileSystemStorageItemBase.Replace, path: {NewPath}");
            }
            finally
            {
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(DisplayType));
                OnPropertyChanged(nameof(Size));
            }
        }

        /// <summary>
        /// 手动更新界面显示
        /// </summary>
        /// <returns></returns>
        public virtual async Task Update()
        {
            try
            {
                if (WIN_Native_API.CheckType(Path) == StorageItemTypes.File)
                {
                    if (WIN_Native_API.GetStorageItem(Path, ItemFilters.File) is FileSystemStorageItemBase Item)
                    {
                        if (StorageItem != null)
                        {
                            StorageItem = await Item.GetStorageItem().ConfigureAwait(true);
                        }

                        SizeRaw = Item.SizeRaw;
                        ModifiedTimeRaw = Item.ModifiedTimeRaw;
                        CreationTimeRaw = Item.CreationTimeRaw;
                        Inner_Thumbnail = null;

                        await LoadMorePropertyAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        LogTracer.Log($"File not found or access deny when executing FileSystemStorageItemBase.Update, path: {Path}");
                    }
                }
                else
                {
                    if (WIN_Native_API.GetStorageItem(Path, ItemFilters.Folder) is FileSystemStorageItemBase Item)
                    {
                        if (StorageItem != null)
                        {
                            StorageItem = await Item.GetStorageItem().ConfigureAwait(true);
                        }

                        ModifiedTimeRaw = Item.ModifiedTimeRaw;
                        CreationTimeRaw = Item.CreationTimeRaw;
                        Inner_Thumbnail = null;

                        await LoadMorePropertyAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        LogTracer.Log($"Folder not found or access deny when executing FileSystemStorageItemBase.Update, path: {Path}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when executing FileSystemStorageItemBase.Update, path: {Path}");
            }
            finally
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(DisplayType));
                OnPropertyChanged(nameof(Size));
            }
        }

        public async Task<SecureAreaStorageItem> EncryptAsync(string ExportFolderPath, string Key, int KeySize, CancellationToken CancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(ExportFolderPath))
            {
                throw new ArgumentNullException(nameof(ExportFolderPath), "ExportFolder could not be null");
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            byte[] KeyArray = null;

            int KeyLengthNeed = KeySize / 8;

            KeyArray = Key.Length > KeyLengthNeed
                       ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed))
                       : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));

            string EncryptedFilePath = System.IO.Path.Combine(ExportFolderPath, $"{System.IO.Path.GetFileNameWithoutExtension(Name)}.sle");

            if (await CreateAsync(EncryptedFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageItemBase EncryptedFile)
            {
                using (FileStream EncryptFileStream = EncryptedFile.GetFileStreamFromFile(AccessMode.Write))
                using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Key = KeyArray,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (FileStream OriginFileStream = GetFileStreamFromFile(AccessMode.Read))
                            using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                            {
                                byte[] ExtraInfoPart1 = Encoding.UTF8.GetBytes($"${KeySize}|{System.IO.Path.GetExtension(Path)}$");
                                await EncryptFileStream.WriteAsync(ExtraInfoPart1, 0, ExtraInfoPart1.Length).ConfigureAwait(false);

                                byte[] PasswordConfirm = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
                                byte[] PasswordConfirmEncrypted = Encryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length);
                                await EncryptFileStream.WriteAsync(PasswordConfirmEncrypted, 0, PasswordConfirmEncrypted.Length).ConfigureAwait(false);

                                using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    await OriginFileStream.CopyToAsync(TransformStream, 2048, CancelToken).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        WIN_Native_API.DeleteFromPath(EncryptedFilePath);
                        throw;
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }

                switch (await OpenAsync(EncryptedFile.Path, ItemFilters.File).ConfigureAwait(false))
                {
                    case SecureAreaStorageItem SItem:
                        {
                            return SItem;
                        }
                    case FileSystemStorageItemBase Item:
                        {
                            return new SecureAreaStorageItem(Item.RawStorageItemData.GetValueOrDefault(), Item.Path, Item.CreationTimeRaw, Item.ModifiedTimeRaw);
                        }
                    default:
                        {
                            return null;
                        }
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 获取原始的修改时间
        /// </summary>
        public DateTimeOffset ModifiedTimeRaw { get; protected set; }

        /// <summary>
        /// 获取文件的修改时间描述
        /// </summary>
        public string ModifiedTime
        {
            get
            {
                if (ModifiedTimeRaw == DateTimeOffset.MaxValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return ModifiedTimeRaw.ToString("G");
                }
            }
        }

        public DateTimeOffset CreationTimeRaw { get; protected set; }

        /// <summary>
        /// 获取文件的创建时间描述
        /// </summary>
        public string CreationTime
        {
            get
            {
                if (CreationTimeRaw == DateTimeOffset.MaxValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return CreationTimeRaw.ToString("G");
                }
            }
        }

        public double ThumbnailOpacity { get; protected set; } = 1d;

        /// <summary>
        /// 获取文件的路径
        /// </summary>
        public virtual string Path => StorageItem == null ? InternalPathString : StorageItem.Path;

        /// <summary>
        /// 获取文件大小描述
        /// </summary>
        public string Size
        {
            get
            {
                if (StorageType == StorageItemTypes.File)
                {
                    return SizeRaw.ToFileSizeDescription();
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 获取原始大小数据
        /// </summary>
        public ulong SizeRaw { get; protected set; }

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public virtual string Name => StorageItem == null ? System.IO.Path.GetFileName(InternalPathString) : StorageItem.Name;

        public virtual string DisplayName
        {
            get
            {
                if (StorageItem == null)
                {
                    return Name;
                }
                else
                {
                    if (StorageItem is StorageFolder Folder)
                    {
                        return Folder.DisplayName;
                    }
                    else if (StorageItem is StorageFile File)
                    {
                        return File.DisplayName;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// 获取文件类型描述
        /// </summary>
        public virtual string DisplayType
        {
            get
            {
                if (StorageItem is StorageFile File)
                {
                    return File.DisplayType;
                }
                else if (StorageItem is StorageFolder Folder)
                {
                    return Folder.DisplayType;
                }
                else
                {
                    return StorageType == StorageItemTypes.File ? System.IO.Path.GetExtension(Name).ToUpper() : Globalization.GetString("Folder_Admin_DisplayType");
                }
            }
        }

        /// <summary>
        /// 获取文件的类型
        /// </summary>
        public virtual string Type
        {
            get
            {
                if (StorageItem is StorageFile File)
                {
                    return File.FileType;
                }
                else if (StorageItem is StorageFolder Folder)
                {
                    return Folder.DisplayType;
                }
                else
                {
                    return StorageType == StorageItemTypes.File ? System.IO.Path.GetExtension(Name).ToUpper() : Globalization.GetString("Folder_Admin_DisplayType");
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is FileSystemStorageItemBase Item)
                {
                    return Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public bool Equals(FileSystemStorageItemBase other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    return other.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator ==(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator !=(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is object;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
