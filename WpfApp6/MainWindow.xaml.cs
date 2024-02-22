using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DotNetBungieAPI.Models;
using System.Xml.Linq;
using Stopwatch.Utility;
using System.Text.RegularExpressions;
using DotNetBungieAPI.Models.Requests;
using System.Collections.Concurrent;
using WpfApp6;
using System.Windows.Threading;

namespace WpfApp6
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _searchTerm;
        private ObservableCollection<PlayerSearchResult> _suggestions;

        public MainWindow()
        {
            InitializeComponent();
            _suggestions = new ObservableCollection<PlayerSearchResult>();
            _suggestions.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasSuggestions));
            DataContext = this; // Set the DataContext to this instance


            _debounceTimer = new DispatcherTimer
            {
                Interval = _searchDelay
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            // Stop the timer
            _debounceTimer.Stop();

            // Perform the search
            SearchPlayers().ConfigureAwait(false);
        }

        public bool HasSuggestions
        {
            get => _suggestions.Count > 0;
        }
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                SearchPlayers().ConfigureAwait(false);
            }
        }
        public ObservableCollection<PlayerSearchResult> Suggestions
        {
            get => _suggestions;
            set
            {
                _suggestions = value;
                OnPropertyChanged();
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private CancellationTokenSource _searchCancellationTokenSource;
        private readonly TimeSpan _searchDelay = TimeSpan.FromSeconds(5);
        private DispatcherTimer _debounceTimer;


        private async Task SearchPlayers()
        {
            if (!string.IsNullOrWhiteSpace(_searchTerm))
            {
                // Clear the suggestions before starting new searches
                Suggestions.Clear();


                var validation = new Regex(@"(.+?)#(\d{1,4})");
                var match = validation.Match(_searchTerm);

                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource = new CancellationTokenSource();


                var resultsBag = new ConcurrentBag<PlayerSearchResult>();
                // Use Task.Run to execute the search operations on separate threads

                var searchByExactBungieNameTask = Task.Run(async () =>
                {
                    if (_searchCancellationTokenSource.Token.IsCancellationRequested) return;


                    if (match.Success)
                    {
                        var playerResult = await D2CharacterTracker.client.ApiAccess.Destiny2.SearchDestinyPlayerByBungieName(
                            BungieMembershipType.All,
                            new DotNetBungieAPI.Models.Requests.ExactSearchRequest()
                            {
                                DisplayName = match.Groups[1].Value,
                                DisplayNameCode = short.Parse(match.Groups[2].Value)
                            });
                        if (playerResult.Response != null && playerResult.Response.Count != 0)
                        {
                            resultsBag.Add(new PlayerSearchResult
                            {
                                DisplayName = playerResult.Response[0].BungieGlobalDisplayName + "#" + playerResult.Response[0].BungieGlobalDisplayNameCode,
                                // Set other properties as needed
                            });
                        }

                    }


                }, _searchCancellationTokenSource.Token);

                var searchByGlobalNamePostTask = Task.Run(async () =>
                {
                    if (_searchCancellationTokenSource.Token.IsCancellationRequested) return;



                    var searchRequest = new DotNetBungieAPI.Models.Requests.UserSearchPrefixRequest(_searchTerm);
                    var response = await D2CharacterTracker.client.ApiAccess.User.SearchByGlobalNamePost(searchRequest);
                    if (response != null && response.Response != null)
                    {
                        var results = response.Response.SearchResults;
                        foreach (var result in results)
                        {
                            resultsBag.Add(new PlayerSearchResult
                            {
                                DisplayName = result.BungieGlobalDisplayName + "#" + result.BungieGlobalDisplayNameCode,
                                // Set other properties as needed
                            });
                        }
                    }
                }, _searchCancellationTokenSource.Token);

                await Task.WhenAll(searchByExactBungieNameTask, searchByGlobalNamePostTask);

                var uniqueDisplayNames = new HashSet<string>();

                // Add all results from the thread-safe collection to the Suggestions collection
                foreach (var result in resultsBag)
                {
                    if (uniqueDisplayNames.Add(result.DisplayName))
                    {
                        // If not, add the result to the Suggestions collection
                        Suggestions.Add(result);
                    }
                }
            }
            else
            {
                Suggestions.Clear();
            }
        }


        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Cancel any previous searches that are still running
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            // Wait for the user to stop typing for the specified delay
            await Task.Delay(_searchDelay, _searchCancellationTokenSource.Token);

            // If the task was not canceled, start the search
            if (!_searchCancellationTokenSource.Token.IsCancellationRequested)
            {
                await SearchPlayers();
            }
        }
    }

    public class PlayerSearchResult
    {
        public string DisplayName { get; set; }
        public long MembershipType { get; set; }
        // Add other properties as needed
    }
}



