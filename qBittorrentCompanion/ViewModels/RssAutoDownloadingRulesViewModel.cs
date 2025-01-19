﻿using Avalonia.Controls;
using QBittorrent.Client;
using qBittorrentCompanion.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace qBittorrentCompanion.ViewModels
{
    public class RssAutoDownloadingRulesViewModel : AutoUpdateViewModelBase
    {
        private bool _showExpandedControls = Design.IsDesignMode
            ? false
            : ConfigService.ShowRssExpandedControls;
        public bool ShowExpandedControls
        {
            get => _showExpandedControls;
            set
            {
                if (_showExpandedControls != value)
                {
                    _showExpandedControls = value;
                    ConfigService.ShowRssExpandedControls = value;
                    OnPropertyChanged(nameof(ShowExpandedControls));
                }
            }
        }

        private bool _showTestData = Design.IsDesignMode || ConfigService.ShowRssTestData;
        public bool ShowTestData
        {
            get => _showTestData;
            set
            {
                if (_showTestData != value)
                {
                    _showTestData = value;
                    ConfigService.ShowRssTestData = value;
                    OnPropertyChanged(nameof(ShowTestData));
                }
            }
        }

        private ObservableCollection<RssAutoDownloadingRuleViewModel> _rssRules = [];
        public ObservableCollection<RssAutoDownloadingRuleViewModel> RssRules
        {
            get => _rssRules;
            set
            {
                if (value != _rssRules)
                {
                    _rssRules = value;
                    OnPropertyChanged(nameof(RssRules));
                }
            }
        }

        private RssAutoDownloadingRuleViewModel _selectedRssRule;
        public RssAutoDownloadingRuleViewModel SelectedRssRule
        {
            get => _selectedRssRule;
            set
            {
                if (value != _selectedRssRule)
                {
                    _selectedRssRule = value;
                    if (_selectedRssRule is not null)
                    {
                        _selectedRssRule.Categories = new ReadOnlyCollection<string>(Categories.ToList());
                        _selectedRssRule.Filter();
                    }
                    OnPropertyChanged(nameof(SelectedRssRule));
                }
            }
        }

        private List<RssAutoDownloadingRuleViewModel> _selectedRssRules = [];
        public List<RssAutoDownloadingRuleViewModel> SelectedRssRules
        {
            get => _selectedRssRules;
            set
            {
                if (value != _selectedRssRules)
                {
                    _selectedRssRules = value;
                    OnPropertyChanged(nameof(SelectedRssRules));
                }
            }
        }

        public ReactiveCommand<Unit, Unit> DeleteSelectedRulesCommand { get; }
        public RssAutoDownloadingRulesViewModel(int intervalInMs = 1500)
        {
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(intervalInMs);
            AddNewRow();

            DeleteSelectedRulesCommand = ReactiveCommand.CreateFromTask(DeleteSelectedRuleAsync);
        }

        private void DataRow_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e!.PropertyName == nameof(MatchTestRowViewModel.MatchTest) && sender is MatchTestRowViewModel row)
            {
                if (!string.IsNullOrEmpty(row.MatchTest))
                {
                    if (SelectedRssRule?.Rows.Last() == row)
                    {
                        AddNewRow();
                    }
                }
            }
        }

        public void AddNewRow()
        {
            SelectedRssRule?.Rows.Add(new MatchTestRowViewModel());
        }

        private ObservableCollection<RssFeedViewModel> _rssFeeds = [];
        public ObservableCollection<RssFeedViewModel> RssFeeds
        {
            get => _rssFeeds;
            set
            {
                if (value != _rssFeeds)
                {
                    _rssFeeds = value;
                    OnPropertyChanged(nameof(RssFeeds));
                }
            }
        }

        private ObservableCollection<string> _categories = [];
        public ObservableCollection<string> Categories
        {
            get => _categories;
            set
            {
                if (value != _categories)
                {
                    _categories = value;
                    OnPropertyChanged(nameof(Categories));
                }
            }
        }

        protected async override Task FetchDataAsync()
        {
            // First get feeds
            RssFeeds.Clear();
            var rssFolder = await QBittorrentService.QBittorrentClient.GetRssItemsAsync(true);
            foreach (var rssFeed in rssFolder.Feeds)
                RssFeeds.Add(new RssFeedViewModel(rssFeed));

            // Then get categories.
            Categories.Clear();
            Categories.Add("");
            IReadOnlyDictionary<string, Category> categories = await QBittorrentService.QBittorrentClient.GetCategoriesAsync();
            foreach (var categoryKvp in categories)
                Categories.Add(categoryKvp.Key);

            // Then get rules (which get populated with data from feeds and categories)
            await RefreshRules();
        }

        public async Task RefreshRules()
        {
            RssRules.Clear();
            IReadOnlyDictionary<string, RssAutoDownloadingRule> rules = await QBittorrentService.QBittorrentClient.GetRssAutoDownloadingRulesAsync();
            foreach (KeyValuePair<string, RssAutoDownloadingRule> rule in rules)
            {
                RssRules.Add(
                    new RssAutoDownloadingRuleViewModel(
                        rule.Value, 
                        rule.Key, 
                        RssFeeds, 
                        rule.Value.AffectedFeeds
                    ));
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task UpdateDataAsync(object? sender, EventArgs e)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {

        }

        public void Initialise()
        {
            _ = FetchDataAsync();
        }

        public async void AddRule(string name)
        {
            await QBittorrentService.QBittorrentClient.SetRssAutoDownloadingRuleAsync(name, new RssAutoDownloadingRule());
            await RefreshRules();
            foreach (var rule in RssRules)
            {
                if (rule.Title == name)
                {
                    SelectedRssRule = rule;
                }
            }
        }

        public async Task DeleteSelectedRuleAsync()
        {
            if (SelectedRssRule != null)
            {
                await DeleteRules([SelectedRssRule]);
            }
        }

        /// <summary>
        /// Deletes the rules one by one and then refreshes <see cref="RssRules"/>
        /// </summary>
        /// <param name="rules"></param>
        public async Task DeleteRules(IEnumerable<RssAutoDownloadingRuleViewModel> rules)
        {
            foreach (var rule in rules)
            {
                try
                {
                    await QBittorrentService.QBittorrentClient.DeleteRssAutoDownloadingRuleAsync(rule.Title);
                }
                catch(Exception e) { Debug.WriteLine(e.Message); }
            }
            await RefreshRules();
        }

        private int _articleCount = 0;
        public int ArticleCount
        {
            get => _articleCount;
            set
            {
                if (value != _articleCount)
                {
                    _articleCount = value;
                    OnPropertyChanged(nameof(ArticleCount));
                }
            }
        }
    }
}