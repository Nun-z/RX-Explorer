﻿using ShareClassLibrary;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class HyperlinkStorageItem : FileSystemStorageItemBase
    {
        public ShellLinkType LinkType { get; private set; } = ShellLinkType.Normal;

        public string LinkTargetPath
        {
            get
            {
                return Data?.LinkTargetPath ?? Globalization.GetString("UnknownText");
            }
        }

        public string[] Arguments
        {
            get
            {
                return Data?.Argument ?? Array.Empty<string>();
            }
        }

        public bool NeedRunAsAdmin
        {
            get
            {
                return (Data?.NeedRunAsAdmin).GetValueOrDefault();
            }
        }

        private HyperlinkPackage Data;

        public override string Path
        {
            get
            {
                return InternalPathString;
            }
        }

        public override string Name
        {
            get
            {
                return System.IO.Path.GetFileName(InternalPathString);
            }
        }

        public override string DisplayType
        {
            get
            {
                return Globalization.GetString("Link_Admin_DisplayType");
            }
        }

        public override string Type
        {
            get
            {
                return System.IO.Path.GetExtension(InternalPathString);
            }
        }

        public async Task LaunchAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                if (LinkType == ShellLinkType.Normal)
                {
                    await Exclusive.Controller.RunAsync(LinkTargetPath, NeedRunAsAdmin, false, false, Arguments).ConfigureAwait(true);
                }
                else
                {
                    if (!await Exclusive.Controller.LaunchUWPLnkAsync(LinkTargetPath).ConfigureAwait(true))
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync().ConfigureAwait(true);
                        });
                    }
                }
            }
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override async Task LoadMorePropertyCore()
        {
            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    Data = await Exclusive.Controller.GetLnkDataAsync(InternalPathString).ConfigureAwait(true);

                    if (!string.IsNullOrEmpty(Data.LinkTargetPath))
                    {
                        if (Data.IconData.Length != 0)
                        {
                            using (MemoryStream IconStream = new MemoryStream(Data.IconData))
                            {
                                Thumbnail = new BitmapImage();
                                await Thumbnail.SetSourceAsync(IconStream.AsRandomAccessStream());
                            }
                        }

                        if (await CheckExist(Data.LinkTargetPath).ConfigureAwait(true))
                        {
                            LinkType = ShellLinkType.Normal;
                        }
                        else
                        {
                            LinkType = ShellLinkType.UWP;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get hyperlink file, path: {InternalPathString}");
            }
        }

        public HyperlinkStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime) : base(Data, StorageItemTypes.File, Path, CreationTime, ModifiedTime)
        {

        }
    }
}
