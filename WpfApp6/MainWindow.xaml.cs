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


        private async Task SearchPlayers()
        {
            if (!string.IsNullOrWhiteSpace(_searchTerm))
            {


                // Create an instance of UserSearchPrefixRequest with the search term
                var searchRequest = new DotNetBungieAPI.Models.Requests.UserSearchPrefixRequest(_searchTerm);

                // Call the Bungie API to search for players
                var response = await D2CharacterTracker.client.ApiAccess.User.SearchByGlobalNamePost(searchRequest);
                // var respons1 = await D2CharacterTracker.client.ApiAccess.User.GetBungieNetUserById();


                // Check if the response contains data and if so, access the collection property
                if (response != null && response.Response != null || _searchTerm.Contains('#'))
                {
                    var resultWithoutSharp = "";

                    Suggestions.Clear();
                    if (_searchTerm.Contains('#'))
                    {
                        StringBuilder lineBuilder = new StringBuilder();
                        foreach (char c in _searchTerm)
                        {
                            if (c != '#')
                            {
                                lineBuilder.Append(c);
                            }
                            else
                            {
                                break;
                            }

                        }
                        resultWithoutSharp = lineBuilder.ToString().Trim();

                        Regex validation = new Regex(@"(.+?)#(\d{1,4})");
                        var m = validation.Match(_searchTerm);
                        if (m.Success)
                        {
                            var playerResult = await D2CharacterTracker.client.ApiAccess.Destiny2.SearchDestinyPlayerByBungieName(BungieMembershipType.All, new DotNetBungieAPI.Models.Requests.ExactSearchRequest() { DisplayName = m.Groups[1].Value, DisplayNameCode = short.Parse(m.Groups[2].Value) });
                            if (playerResult.Response != null && playerResult.Response.Count != 0)
                            {
                                Suggestions.Add(new PlayerSearchResult
                                {
                                    DisplayName = playerResult.Response[0].BungieGlobalDisplayName + "#" + playerResult.Response[0].BungieGlobalDisplayNameCode,

                                    // Set other properties as needed
                                });
                            }
                            else
                            {
                                Suggestions.Clear();
                                var searchRequestNew = new DotNetBungieAPI.Models.Requests.UserSearchPrefixRequest(resultWithoutSharp);
                                response = await D2CharacterTracker.client.ApiAccess.User.SearchByGlobalNamePost(searchRequestNew);
                                if (response != null && response.Response != null)
                                {
                                    var results = response.Response.SearchResults;
                                    foreach (var result in results)
                                    {

                                        if (!Suggestions.Any(s => s.DisplayName == result.BungieGlobalDisplayName + "#" + result.BungieGlobalDisplayNameCode))
                                        {
                                            // If not, add the new result
                                            Suggestions.Add(new PlayerSearchResult
                                            {
                                                DisplayName = result.BungieGlobalDisplayName + "#" + result.BungieGlobalDisplayNameCode,
                                                // Set other properties as needed
                                            });
                                        }

                                    }
                                }
                            }
                        }
                    }
                    else if (response != null && response.Response != null)
                    {
                        var results = response.Response.SearchResults;
                        foreach (var result in results)
                        {

                            Suggestions.Add(new PlayerSearchResult
                            {
                                DisplayName = result.BungieGlobalDisplayName + "#" + result.BungieGlobalDisplayNameCode,

                                // Set other properties as needed

                            });

                        }
                    }

                    //Совместить два поиска, чтобы одновременно выводило через SearchByGlobalNamePost и SearchDestinyPlayerByBungieName
                    //Сделать нормальное оформление
                }
            }
            else
            {
                Suggestions.Clear();
            }
        }



        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // No need to do anything here since we're using two-way binding
        }
    }

    public class PlayerSearchResult
    {
        public string DisplayName { get; set; }
        public long MembershipType { get; set; }
        // Add other properties as needed
    }
}


