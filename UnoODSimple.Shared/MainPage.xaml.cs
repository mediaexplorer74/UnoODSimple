﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Popups;

using UnoODSimple.Request;
using UnoODSimple.Response;
using UnoODSimple.Helpers;

namespace UnoODSimple
{
    public sealed partial class MainPage : Page
    {
        private static string _downloadFilePath;
        private static string _folderPath;

        private readonly OneDriveService _service = ((App)Application.Current).ServiceInstance;
        
        private string _savedId;

        private bool _isBusy;

        public MainPage()
        {
            InitializeComponent();

            _isBusy = false;

            //Progress.IsActive = false;
            Progress.Visibility = _isBusy ? Visibility.Visible : Visibility.Collapsed;

            var navManager = SystemNavigationManager.GetForCurrentView();
            navManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            //Progress.IsActive = false;
            Progress.Visibility = _isBusy ? Visibility.Visible : Visibility.Collapsed;
        }

        // OnNavigatedTo
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Restore the entered values
            FolderPathText.Text = _folderPath ?? string.Empty;
            DownloadFilePathText.Text = _downloadFilePath ?? string.Empty;
        }

        //  OnNavigatingFrom
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Save the entered values for later
            _folderPath = FolderPathText.Text;
            _downloadFilePath = DownloadFilePathText.Text;

            base.OnNavigatingFrom(e);
        }

        // ShowBusy
        private void ShowBusy(bool isBusy)
        {
            _isBusy = isBusy;

            //Progress.IsActive = isBusy;
            Progress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

            PleaseWaitCache.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

        }//ShowBusy

        //AuthenticateClick
        private void AuthenticateClick(object sender, RoutedEventArgs e)
        {
            ShowBusy(true);

            if (!_service.CheckAuthenticate(
                async () =>
                {
                    var dialog = new MessageDialog("You are authenticated!", "Success!");
                    await dialog.ShowAsync();
                    ShowBusy(false);

                    Frame.GoBack();
                },
                async () =>
                {
                    var dialog = new MessageDialog("Problem when authenticating!", "Sorry!");
                    await dialog.ShowAsync();
                    ShowBusy(false);

                    Frame.GoBack();
                }))
            {
                Frame.Navigate(typeof(AuthenticationPage));
            }

        }//AuthenticateClick


        // BrowseSubfolderClick
        private async void BrowseSubfolderClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FolderPathText.Text))
            {
                var dialog = new MessageDialog("Please enter a path to a folder, for example Apps/OneDriveSample", "Error!");
                await dialog.ShowAsync();
                return;
            }

            Exception error = null;
            ItemInfoResponse subfolder = null;

            ShowBusy(true);

            try
            {
                subfolder = await _service.GetItem(FolderPathText.Text);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            if (subfolder == null)
            {
                var dialog = new MessageDialog($"Not found: {FolderPathText.Text}");
                await dialog.ShowAsync();
            }
            else
            {
                var children = await _service.PopulateChildren(subfolder);
                DisplayHelper.ShowContent(
                    "SHOW SUBFOLDER CONTENT ------------------------",
                    subfolder,
                    children,
                    async message =>
                    {
                        var dialog = new MessageDialog(message);
                        await dialog.ShowAsync();
                    });
            }

            ShowBusy(false);
        }//DownloadFileClick

        // DownloadFileClick
        private async void DownloadFileClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DownloadFilePathText.Text))
            {
                var dialog = new MessageDialog("Please enter a path to an existing file, for example Apps/OneDriveSample/Test.jpg", "Error!");
                await dialog.ShowAsync();
                return;
            }

            Exception error = null;
            ItemInfoResponse foundFile = null;
            Stream contentStream = null;

            ShowBusy(true);

            try
            {
                foundFile = await _service.GetItem(DownloadFilePathText.Text);

                if (foundFile == null)
                {
                    var dialog = new MessageDialog($"Not found: {DownloadFilePathText.Text}");
                    await dialog.ShowAsync();
                    ShowBusy(false);
                    return;
                }

                // Get the file's content
                contentStream = await _service.RefreshAndDownloadContent(foundFile, false);

                if (contentStream == null)
                {
                    var dialog = new MessageDialog($"Content not found: {DownloadFilePathText.Text}");
                    await dialog.ShowAsync();
                    ShowBusy(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            // Save the retrieved stream to the local drive

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = foundFile.Name
            };

            var extension = Path.GetExtension(foundFile.Name);

            picker.FileTypeChoices.Add(
                $"{extension} files",
                new List<string>
                {
                    extension
                });

            var targetFile = await picker.PickSaveFileAsync();

            using (var targetStream = await targetFile.OpenStreamForWriteAsync())
            {
                using (var writer = new BinaryWriter(targetStream))
                {
                    contentStream.Position = 0;

                    using (var reader = new BinaryReader(contentStream))
                    {
                        byte[] bytes;

                        do
                        {
                            bytes = reader.ReadBytes(1024);
                            writer.Write(bytes);
                        }
                        while (bytes.Length == 1024);
                    }
                }
            }

            var successDialog = new MessageDialog("Done saving the file!", "Success");
            await successDialog.ShowAsync();
            ShowBusy(false);
        }

        // GetAppRootClick
        private async void GetAppRootClick(object sender, RoutedEventArgs e)
        {
            Exception error = null;
            ItemInfoResponse folder = null;
            IList<ItemInfoResponse> children = null;

            ShowBusy(true);

            try
            {
                folder = await _service.GetAppRoot();
                children = await _service.PopulateChildren(folder);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            DisplayHelper.ShowContent(
                "SHOW APP FOLDER CONTENT ++++++++++++++++++++++",
                folder,
                children,
                async message =>
                {
                    var dialog = new MessageDialog(message);
                    await dialog.ShowAsync();
                    ShowBusy(false);
                });
        }//GetAppRootClick

        private async void GetLinkClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_savedId))
            {
                var dialog =
                    new MessageDialog(
                        "For the purpose of this demo, save a file first using the Upload File button",
                        "No file saved");
                await dialog.ShowAsync();
                return;
            }

            Exception error = null;
            LinkResponseInfo linkInfo = null;

            ShowBusy(true);

            try
            {
                linkInfo = await _service.GetLink(LinkKind.View, _savedId); // This could also be LinkKind.Edit
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            //Debug.WriteLine("RETRIEVED LINK ---------------------");
            //Debug.WriteLine(linkInfo.Link.WebUrl);

            var successDialog = new MessageDialog(
                $"The link was copied to the Debug window: {linkInfo.Link.WebUrl}",
                "No file saved");

            await successDialog.ShowAsync();
            ShowBusy(false);
        }

        private async void GetRootFolderClick(object sender, RoutedEventArgs e)
        {
            Exception error = null;
            ItemInfoResponse folder = null;
            IList<ItemInfoResponse> children = null;

            ShowBusy(true);

            try
            {
                folder = await _service.GetRootFolder();
                children = await _service.PopulateChildren(folder);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            DisplayHelper.ShowContent(
                "SHOW ROOT FOLDER ++++++++++++++++++++++",
                folder,
                children,
                async message =>
                {
                    var dialog = new MessageDialog(message);
                    await dialog.ShowAsync();
                });

            ShowBusy(false);
        }

        private async void LogOffClick(object sender, RoutedEventArgs e)
        {
            Exception error = null;
            ShowBusy(true);

            try
            {
                await _service.Logout();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                var dialog = new MessageDialog(error.Message, "Error!");
                await dialog.ShowAsync();
                ShowBusy(false);
                return;
            }

            var successDialog = new MessageDialog("You are now logged off", "Success");
            await successDialog.ShowAsync();
            ShowBusy(false);
        }

        private async void UploadFileClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                ShowBusy(true);

                Exception error = null;

                try
                {
                    // For the demo, save this file in the App folder
                    var folder = await _service.GetAppRoot();

                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        var info = await _service.SaveFile(folder.Id, file.Name, stream);

                        // Save for the GetLink demo
                        _savedId = info.Id;

                        var successDialog =
                            new MessageDialog(
                                $"Uploaded file has ID {info.Id}. You can now use the Get Link button to retrieve a direct link to the file",
                                "Success");

                        await successDialog.ShowAsync();
                    }

                    ShowBusy(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                if (error != null)
                {
                    var dialog = new MessageDialog(error.Message, "Error!");
                    await dialog.ShowAsync();
                    ShowBusy(false);
                }
            }
        }
    }
}

