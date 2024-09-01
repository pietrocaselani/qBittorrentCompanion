﻿using QBittorrent.Client;
using qBittorrentCompanion.Helpers;
using qBittorrentCompanion.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using System.Threading;

namespace qBittorrentCompanion.ViewModels
{
    /**
     * Avalonia seems to run into problems displaying a DataGrid with multiple classes even if 
     * they enherit from the same baseclass/follow the same blueprint. That means that this class
     * is trying to fulfill the role of two classes (file and folder viewmodels).
     * Basically if it doesn't work as it should wor
     */
    public class TorrentContentViewModel : INotifyPropertyChanged
    {

        private TorrentContent? _torrentContent;

        public ObservableCollection<TorrentContentViewModel> Contents { get; set; } = [];

        public string[] TorrentContentPriorities => [
            DataConverter.TorrentContentPriorities.Skip, // Do not download
            DataConverter.TorrentContentPriorities.Minimal, //Normal
            //DataConverter.TorrentContentPriorities.VeryLow,
            //DataConverter.TorrentContentPriorities.Low,
            //DataConverter.TorrentContentPriorities.Normal,
            //DataConverter.TorrentContentPriorities.High,
            DataConverter.TorrentContentPriorities.VeryHigh, // High
            DataConverter.TorrentContentPriorities.Maximal, // Maximal
            DataConverter.TorrentContentPriorities.Mixed
        ];

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        private bool _isUpdating = false;
        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if(_isUpdating != value)
                {
                    _isUpdating = value; 
                    OnPropertyChanged(nameof(IsUpdating));
                }
            }
        }

        public string DisplayName { get; } //Set is ommitted - immutable 
        public bool IsFile = false;
        private string _infoHash;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TorrentContentViewModel(string infoHash, TorrentContent torrentContent)
        {
            _infoHash = infoHash;
            _torrentContent = torrentContent;
            DisplayName = torrentContent.Name.Split('/').Last();
            IsFile = true;
        }

        public TorrentContentViewModel(string infoHash, string name, string displayName)
        {
            _infoHash = infoHash;
            Name = name;
            DisplayName = displayName;
        }

        public double? Availability
        {
            get => _torrentContent?.Availability ?? 0;
            set
            {
                if (_torrentContent is not null && value != _torrentContent.Availability)
                {
                    _torrentContent.Availability = value;
                    OnPropertyChanged(nameof(Availability));
                }
            }
        }

        public FluentIcons.Common.Symbol icon
        {
            get
            {
                if (_torrentContent is null)
                    return FluentIcons.Common.Symbol.Folder;

                string extension = Path.GetExtension(_torrentContent.Name).ToLower();
                switch (extension)
                {
                    // Compressed files
                    case ".zip":
                    case ".rar":
                    case ".7z":
                    case ".tar":
                    case ".gz":
                    case ".bz2":
                    case ".xz":
                        return FluentIcons.Common.Symbol.FolderZip;

                    // Video files
                    case ".mp4":
                    case ".mkv":
                    case ".avi":
                    case ".mov":
                    case ".wmv":
                    case ".flv":
                    case ".webm":
                    case ".m4v":
                        return FluentIcons.Common.Symbol.Video;

                    // Image files
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".gif":
                    case ".bmp":
                    case ".tiff":
                    case ".svg":
                    case ".webp":
                        return FluentIcons.Common.Symbol.Image;

                    // Audio files
                    case ".mp3":
                    case ".wav":
                    case ".flac":
                    case ".aac":
                    case ".ogg":
                    case ".m4a":
                    case ".wma":
                    case ".aiff":
                        return FluentIcons.Common.Symbol.MusicNote1;

                    // Document files
                    case ".pdf":
                        return FluentIcons.Common.Symbol.DocumentPdf;
                    case ".doc":
                    case ".docx":
                    case ".odt":
                    case ".rtf":
                    case ".txt":
                        return FluentIcons.Common.Symbol.DocumentText;

                    // Spreadsheet files
                    case ".xls":
                    case ".xlsx":
                    case ".ods":
                    case ".csv":
                        return FluentIcons.Common.Symbol.LayoutCellFourFocusBottomLeft;

                    // Code files
                    case ".css":
                        return FluentIcons.Common.Symbol.DocumentCss;
                    case ".js":
                        return FluentIcons.Common.Symbol.DocumentJavascript;
                    case ".java":
                        return FluentIcons.Common.Symbol.DocumentJava;
                    case ".html":
                    case ".ts":
                    case ".json":
                    case ".xml":
                    case ".yml":
                    case ".yaml":
                    case ".cs":
                    case ".py":
                    case ".cpp":
                    case ".c":
                    case ".h":
                    case ".php":
                    case ".rb":
                    case ".swift":
                    case ".go":
                    case ".rs":
                        return FluentIcons.Common.Symbol.Code;

                    // Disk image files
                    case ".iso":
                    case ".img":
                    case ".dmg":
                        return FluentIcons.Common.Symbol.DocumentData;

                    default:
                        return FluentIcons.Common.Symbol.Document;

                }
            }
        }


        public int Index
        {
            get => _torrentContent?.Index ?? -1;
            set
            {
                if (_torrentContent is not null && value != _torrentContent.Index)
                {
                    _torrentContent.Index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        public string recursiveGetIndexesForPriority(TorrentContentPriority priority)
        {
            List<int> indexes = new List<int>();

            foreach (var content in Contents)
            {
                if (content.IsFile && !content.Priority.Equals(priority) && content.Index is int ind)
                {
                    indexes.Add(ind);
                }
                else
                {
                    var childIndexes = content.recursiveGetIndexesForPriority(priority);
                    if (!string.IsNullOrEmpty(childIndexes))
                    {
                        indexes.AddRange(childIndexes.Split('|').Select(int.Parse));
                    }
                }
            }

            return string.Join('|', indexes);
        }


        public bool IsSeed
        {
            get => _torrentContent?.IsSeeding ?? false;
            set
            {
                if (_torrentContent is not null && value != _torrentContent.IsSeeding)
                {
                    _torrentContent.IsSeeding = value;
                    OnPropertyChanged(nameof(IsSeed));
                }
            }
        }

        private string _folderName = "";

        public string Name
        {
            get => _torrentContent?.Name ?? _folderName;
            set
            {
                if (_torrentContent is not null)
                {
                    if (value != _torrentContent.Name)
                    {
                        _torrentContent.Name = value;
                        OnPropertyChanged(nameof(Name));
                    }
                }
                else if (value != _folderName)
                {
                    _folderName = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public QBittorrent.Client.Range PieceRange
        {
            get => _torrentContent?.PieceRange ?? new QBittorrent.Client.Range();
            set
            {
                if (_torrentContent is not null && !_torrentContent.PieceRange.Equals(value))
                {
                    _torrentContent.PieceRange = value;
                    OnPropertyChanged(nameof(PieceRange));
                }
            }
        }

        public TorrentContentPriority folderPriority = TorrentContentPriority.Normal;
        /// <summary>
        /// <inheritdoc cref="TorrentContent"/>
        /// </summary>
        public TorrentContentPriority Priority
        {
            get => _torrentContent?.Priority ?? folderPriority;
            set
            {
                //File
                if (_torrentContent is not null)
                {
                    if (value != _torrentContent.Priority && Index != null)
                    {
                        _ = UpdatePriority(value);
                        _torrentContent.Priority = value;
                        OnPropertyChanged(nameof(Priority));
                    }
                }
                //Folder
                else if (_torrentContent is null && value != folderPriority)
                {
                    _ = UpdatePriority(value);
                    folderPriority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }

        /// <summary>
        /// Sets the priority and triggers OnPropertyChanged but does nothing else
        /// Useful for avoiding triggering all file async calls when setting priority on a directory
        /// </summary>
        public void SetPriority(TorrentContentPriority priority)
        {
            if (_torrentContent is TorrentContent torrentContent)
            {
                torrentContent.Priority = priority;
                OnPropertyChanged(nameof(Priority));
            }
            else // Is directory
            {
                folderPriority = priority;
                OnPropertyChanged(nameof(Priority));
            }
        }

        private async Task UpdatePriority(TorrentContentPriority priority)
        {
            RecursiveSetUpdating(true);
            if (IsFile)
            {
                await QBittorrentService.QBittorrentClient.SetFilePriorityAsync(_infoHash, Index, priority);
            }
            else
            {
                string indexes = recursiveGetIndexesForPriority(priority);
                if (indexes != "")
                    RecursiveSetPriority(priority);

                await SetMultipleFilePrioritiesAsync(_infoHash, indexes, priority);
            }
            RecursiveSetUpdating(false);
        }

        /// <summary>
        /// qbittorrent-net-client doesn't support updating multiple files in one go natively, but the QBittorrent WebUI API does.
        /// 
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="fileIds"></param>
        /// <param name="priority"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private async Task SetMultipleFilePrioritiesAsync(string hash, string fileIds, TorrentContentPriority priority, CancellationToken? token = null)
        {
            if (!Enum.GetValues(typeof(TorrentContentPriority)).Cast<TorrentContentPriority>().Contains(priority))
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }

            var baseUrl = QBittorrentService.GetUrl();

            var requestUri = new Uri(baseUrl, "api/v2/torrents/filePrio");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hash", hash),
                new KeyValuePair<string, string>("id", fileIds),
                new KeyValuePair<string, string>("priority", ((int)priority).ToString())
            });

            try
            {
                using (content)
                {
                    var client = QBittorrentService.GetHttpClient();

                    // Ensure the client has the necessary headers
                    client.DefaultRequestHeaders.Clear();
                    foreach (var header in QBittorrentService.QBittorrentClient.DefaultRequestHeaders)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    using HttpResponseMessage message =
                        await client.PostAsync(requestUri, content, token ?? CancellationToken.None)
                        .ConfigureAwait(false);
                    message.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HttpRequestException: {ex.Message}");
                Debug.WriteLine($"Status Code: {ex.StatusCode}");
                Debug.WriteLine($"Request URI: {requestUri} {fileIds}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
                throw;
            }
        }

        public void RecursiveSetUpdating(bool isUpdating)
        {
            if (IsFile)
                IsUpdating = isUpdating;
            foreach (var child in Contents)
            {
                child.RecursiveSetUpdating(isUpdating);
            }
        }

        public void RecursiveSetPriority(TorrentContentPriority pr)
        {
            SetPriority(pr);
            foreach(var child in Contents)
            {
                child.RecursiveSetPriority(pr);
            }
        }

        private double CalculateProgress()
        {
            double totalSize = 0;
            double completedSize = 0;

            foreach (TorrentContentViewModel tcvm in Contents)
            {
                totalSize += tcvm.Size;
                completedSize += tcvm.Size * tcvm.Progress;
            }

            if (totalSize == 0) return 0;

            return completedSize / totalSize;
        }

        public double Progress
        {
            get => _torrentContent?.Progress ?? CalculateProgress();
            set
            {
                if (_torrentContent is not null && value != _torrentContent.Progress)
                {
                    _torrentContent.Progress = value;
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(Remaining));
                }
            }
        }

        public long Size
        {
            get => _torrentContent?.Size ?? Contents.Sum<TorrentContentViewModel>(t => t.Size);
            set
            {
                if (_torrentContent is not null && value != _torrentContent.Size)
                {
                    _torrentContent.Size = value;
                    OnPropertyChanged(nameof(Size));
                    OnPropertyChanged(nameof(SizeHr));
                    OnPropertyChanged(nameof(Remaining));
                }
            }
        }

        private long CalculateRemaining()
        {
            long remaining = 0;

            foreach (TorrentContentViewModel tcvm in Contents)
            {
                remaining += tcvm.Remaining;
            }

            return remaining;
        }

        public long Remaining
        {
            get 
            {
                if (_torrentContent is not null)
                {
                    return Size - (long)(Size * Progress);
                }
                else
                {
                    return CalculateRemaining();
                }
            }

        }

        public string SizeHr => DataConverter.BytesToHumanReadable(Size);

        public string RemainingHr => DataConverter.BytesToHumanReadable(Remaining);

        public void Update(TorrentContent tc)
        {
            Availability = tc.Availability;
            Index = tc.Index ?? -1;
            IsSeed = tc.IsSeeding;
            Name = tc.Name;
            PieceRange = tc.PieceRange;
            Priority = tc.Priority;
            Progress = tc.Progress;
            Size = tc.Size;
        }

    }

}
