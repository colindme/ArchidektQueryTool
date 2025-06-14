using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace QueryLibrary
{
	public class ArchidektQueryTool
	{
		readonly JTokenHttpClient _httpClient;
		readonly Logger _logger;
		readonly Config _config;

		// Archidekt endpoints
		const string baseArchidektUri = "https://www.archidekt.com/";
		const string profileEndpoint = "u/";
		const string collectionEndpoint = "collection/v2/";
		const string deckSearchEndpoint = "search/decks";
		const string deckEndpoint = "decks/";
		// Html & Json identifiers
		const string archidektDataId = "__NEXT_DATA__";
		const string userIdJsonIndicator = "user.id";
		const string archidektSelectStatement = $"//script[contains(@id, '{archidektDataId}')]";
		const string totalPageIndicator = "pageProps.totalPages";
		const string collectionCardsIndicator = "collectionV2.collectionCards";
		const string deckPageIndicator = "results";
		const string cardNameIndicator = "card.name";
		const string cardQuantityIndicator = "quantity";
		const string foilIndicator = "foil";
		const string nonfoilPriceIndicator = "prices.tcg";
		const string foilPriceIndicator = "prices.tcgFoil";
		const string tcgPlayerIdIndicator = "ids.tcgId";


		public ArchidektQueryTool(Config config) 
		{
			_logger = new Logger(false, false);
			_httpClient = new JTokenHttpClient(baseArchidektUri, _logger, 0);
			_config = config;
		}		

		// TODO: Add callback func to report progress to callers (enum QueryStage?)
		public void Run(string fullUsernamesInput, string fullCardsInput, Action<QueryProgress> progressCallback)
		{
			// Gathering required information for queries (ArchidektIDs, card names)
			progressCallback?.Invoke(QueryProgress.GatheringQueryInfo);
			Task<List<KeyValuePair<string, string>>> collectionTask = GetArchidektUserIDs(GetUsernamesToQueryFromString(fullUsernamesInput));
			HashSet<string> cards = CreateCardQueryInputFromString(fullCardsInput);
			collectionTask.Wait();

			// Query archidekt for information (cards & deck information)
			progressCallback?.Invoke(QueryProgress.StartingQuery);
			List<Task> cardQueryTaskList = new();
			List<Task<KeyValuePair<string, List<DeckInfo>>?>> deckQueryTaskList = new();
			List<KeyValuePair<string, ConcurrentDictionary<int, CollectionCardInfo>>> cardCollections = new List<KeyValuePair<string, ConcurrentDictionary<int, CollectionCardInfo>>>();
			foreach (KeyValuePair<string, string> collection in collectionTask.Result)
			{
				ConcurrentDictionary<int, CollectionCardInfo> cardCollection = new ConcurrentDictionary<int, CollectionCardInfo>();
				cardCollections.Add(KeyValuePair.Create(collection.Key, cardCollection));
				if (_config.IncludeDeckInfo)
				{
					deckQueryTaskList.Add(LoadAllDeckInfoForUser(collection.Key));
				}
				foreach (string card in cards)
				{
					cardQueryTaskList.Add(QueryCollectionForCard(cardCollection, collection.Value, card, _config.AllowPartialMatches));
				}
			}
			Task.WaitAll(cardQueryTaskList.ToArray());
			Task.WaitAll(deckQueryTaskList.ToArray());

			// Output section
			progressCallback?.Invoke(QueryProgress.CreatingOutput);
			Dictionary<string, List<CollectionCardInfo>> userToCardsDict = new Dictionary<string, List<CollectionCardInfo>>();
			foreach (KeyValuePair<string, ConcurrentDictionary<int, CollectionCardInfo>> pair in cardCollections)
			{
				userToCardsDict.Add(pair.Key, pair.Value.Values.ToList());
			}

			string output = CreateOutput(cards, userToCardsDict, _config.IncludeDeckInfo ? deckQueryTaskList.Where(d => d.Result.HasValue).Select(d => d.Result!.Value).ToDictionary() : null);
			if (_config.OutputToConsole)
			{
				Console.WriteLine(output);
			}
			if (_config.OutputToFile)
			{
				// TODO: Logic here
			}

			progressCallback?.Invoke(QueryProgress.Done);
		}
		#region UserID functions
		private HashSet<string> GetUsernamesToQueryFromString(string fullString)
		{
			HashSet<string> usernameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(fullString) || string.IsNullOrWhiteSpace(fullString)) return usernameSet;
			try
			{
				usernameSet = fullString.Split('\n').Select(line => line.Trim()).Select(line => line.TrimEnd(',')).Where(line => !(string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))).ToHashSet();
			}
			catch (Exception ex)
			{
				_logger.Log($"Failed to get usernames to query from string due to exception: {ex}");
			}
			return usernameSet;
		}

		private async Task<List<KeyValuePair<string, string>>> GetArchidektUserIDs(HashSet<string> usernames)
		{
			List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
			// Associate username with the task incase of a failure
			Dictionary<string, Task<string?>> archidektUserIdTaskMap = new Dictionary<string, Task<string?>>();
			foreach (string username in usernames)
			{
				archidektUserIdTaskMap.Add(username, GetArchidektUserID(username));
			}
			await Task.WhenAll(archidektUserIdTaskMap.Values);
			foreach (string username in archidektUserIdTaskMap.Keys)
			{
				string? userId = archidektUserIdTaskMap[username].Result;
				if (userId != null)
				{
					result.Add(KeyValuePair.Create(username, userId));
				}
				else
				{
					_logger.Log($"Failed to find userId for username: {username} | userId was null");
				}
			}
			return result;
		}

		async private Task<string?> GetArchidektUserID(string username)
		{
			JToken? response = await _httpClient.QueryPageForHTMLNode(profileEndpoint + username, archidektSelectStatement);
			string? userId = response?.SelectToken($"$..{userIdJsonIndicator}")?.Value<string>();
			return userId;
		}
		#endregion
		#region Card functions
		private HashSet<string> CreateCardQueryInputFromString(string input)
		{
			HashSet<string> result = new HashSet<string>();
			foreach (string inputLine in input.Split('\n'))
			{
				if (string.IsNullOrWhiteSpace(inputLine) || string.IsNullOrEmpty(inputLine)) continue;
				string sanitizedLine = SanitizeCardInputLine(inputLine);
				if (string.IsNullOrWhiteSpace(sanitizedLine) || string.IsNullOrEmpty(sanitizedLine)) continue;
				result.Add(sanitizedLine);
			}
			return result;
		}

		private string SanitizeCardInputLine(string line)
		{
			string result = line;
			result = result.Trim();
			// Remove any quantity (if the first word is a number)
			if (Char.IsDigit(result[0]))
			{
				int firstSpace = result.IndexOf(' ');
				if (firstSpace != -1)
				{
					result = result.Substring(firstSpace + 1);
				}
			}

			int firstAlphabeticCharIndex = 0;
			for (int i = 0; i < result.Length; i++)
			{
				if (Char.IsLetter(result[i]))
				{
					firstAlphabeticCharIndex = i;
					break;
				}
			}

			result = result.Substring(firstAlphabeticCharIndex);
			result = result.TrimEnd(',');
			return result;
		}

		private async Task QueryCollectionForCard(ConcurrentDictionary<int, CollectionCardInfo> cardCollection, string collectionId, string cardName, bool allowPartialMatches)
		{
			try
			{
				// For whatever reason, spaces breaks the query so we have to only search by the first word and filter down from there
				string queryCardName = cardName;
				int firstSpaceIndex = queryCardName.IndexOf(' ');
				if (firstSpaceIndex != -1)
				{
					queryCardName = queryCardName.Substring(0, firstSpaceIndex);
				}
				string escapedCardName = Uri.EscapeDataString(queryCardName);
				// Page Querying section
				int currentPage = 1;
				int totalPages = 0;
				do
				{
					string query = $"{collectionEndpoint}{collectionId}?syntaxQuery={escapedCardName}&page={currentPage}";
					JToken? json = await _httpClient.QueryPageForHTMLNode(query, archidektSelectStatement);
					if (json == null)
					{
						_logger?.Log($"Failed to get Archidekt page collection page JSON for card: {cardName}. Skipping this page of search results.");
						currentPage++;
						continue;
					}

					// If this is the first page we are parsing, parse the data for the total records / pages
					if (currentPage == 1)
					{
						JToken? pageToken = json.SelectToken($"$..{totalPageIndicator}");
						if (pageToken == null)
						{
							_logger?.Log($"Failed to find total page token for card {cardName}. Will finish searching this page but other pages won't be queried.");
						}
						else
						{
							// Save the total page count
							totalPages = pageToken.Value<int>();
						}
					}

					// Parse the collection of cards
					JToken? collectionToken = json.SelectToken($"$..{collectionCardsIndicator}");
					if (collectionToken == null)
					{
						_logger?.Log($"Failed to find the JSON for collection card records for card: {cardName} Skipping this page of search results.");
						continue;
					}

					foreach (JToken card in collectionToken.Children())
					{
						JToken? cardNameToken = card.SelectToken($"$..{cardNameIndicator}");
						if (cardNameToken != null)
						{
							string? foundCardName = cardNameToken.Value<string>();
							if (foundCardName == null)
							{
								_logger?.Log($"Collection card record's name was null for card name: {cardName}. Moving onto next collection record.");
								continue;
							}
							if ((!allowPartialMatches && string.Equals(foundCardName, cardName, StringComparison.OrdinalIgnoreCase)) 
								|| (allowPartialMatches && foundCardName.Contains(cardName, StringComparison.OrdinalIgnoreCase)))
							{
								int recordId;
								if (card is JProperty property)
								{
									recordId = int.Parse(property.Name);
								}
								else
								{
									_logger?.Log($"Failed to find recordId for card: {foundCardName} | Card was not a JProperty so continuing onto next card record.");
									continue;
								}

								// Found a match, add the info!
								CollectionCardInfo cardInfo = new CollectionCardInfo();
								cardInfo.Name = foundCardName;
								cardInfo.CollectionId = collectionId;
								// Get the Quantity
								JToken? quantityToken = card.SelectToken($"$..{cardQuantityIndicator}");
								if (quantityToken != null)
								{
									cardInfo.Quantity = quantityToken.Value<int>();
								}
								else
								{
									_logger?.Log($"Failed to find token for quantity for found card: {foundCardName}. Record will be missing quantity info.");
								}
								// Get the Foil status
								JToken? foilToken = card.SelectToken($"$..{foilIndicator}");
								if (foilToken != null)
								{
									cardInfo.Foil = foilToken.Value<bool>();
								}
								else
								{
									_logger?.Log($"Failed to find token for foil for found card: {foundCardName}. Record will assume nonfoil.");
								}
								// Get the price
								JToken? priceToken;
								if (cardInfo.Foil)
								{
									priceToken = card.SelectToken($"$..{foilPriceIndicator}");
								}
								else
								{
									priceToken = card.SelectToken($"$..{nonfoilPriceIndicator}");
								}
								if (priceToken != null)
								{
									cardInfo.Price = priceToken.Value<float>();
								}
								else
								{
									_logger?.Log($"Failed to find TCGPlayer Price token for found card: {foundCardName}. Record will be missing price info.");
								}

								// Get the TcgPlayerId
								JToken? tcgIdToken = card.SelectToken($"$..{tcgPlayerIdIndicator}");
								if (tcgIdToken != null)
								{
									cardInfo.TcgPlayerId = tcgIdToken.Value<int>();
								}
								else
								{
									_logger?.Log($"Failed to find TCGPlayer ID token for found card: {foundCardName}. Record will be missing TCGPlayer ID info.");
								}

								cardCollection.TryAdd(recordId, cardInfo);
							}
						}
						else
						{
							_logger?.Log($"Failed to find card name token for record in query for {cardName}. Skipping any further parsing for this collection record");
						}
					}

					currentPage++;
				}
				while (currentPage <= totalPages);
			}
			catch (Exception ex)
			{
				_logger?.Log($"Encountered unknown exception while querying collection for {cardName}: {ex}. Returning immediately from this query");
			}
		}
		#endregion 
		#region Deck functions
		private async Task<KeyValuePair<string, List<DeckInfo>>?> LoadAllDeckInfoForUser(string username)
		{
			List<DeckInfo> decks = new List<DeckInfo>();
			string query = $"{deckSearchEndpoint}?ownerUsername={username}";
			JToken? json = await _httpClient.QueryPageForHTMLNode(query, archidektSelectStatement);
			if (json == null)
			{
				_logger?.Log($"Failed to find Archidekt page JSON data for user {username}. Their DeckInfo list will be null");
				return null;
			}

			JToken? resultsToken = json.SelectToken($"$..{deckPageIndicator}");

			List<Task<DeckInfo?>> deckQueryTasks = new List<Task<DeckInfo?>>();
			if (resultsToken != null)
			{
				foreach (JToken result in resultsToken.Children())
				{
					JToken? deckIdToken = result.SelectToken("id");
					JToken? deckNameToken = result.SelectToken("name");
					if (deckIdToken != null && deckNameToken != null)
					{
						string? deckId = deckIdToken.Value<string>();
						string? deckName = deckNameToken.Value<string>();
						if (deckId != null && deckName != null)
						{
							deckQueryTasks.Add(LoadDeckInfo(deckName, deckId));
						}
						else
						{
							_logger?.Log($"Failed to get a string value for deck ID or deck name for user {username}. Not attempting to load this particular DeckInfo");
						}
					}
					else
					{
						_logger?.Log($"Failed to find JSON token for deck ID or deck name for user {username}. Not attempting to load this particular DeckInfo");
					}
				}
			}
			else
			{
				_logger?.Log($"Failed to find deck info JSON container for user {username}. Empty DeckInfo array will be returned");
			}
			DeckInfo?[] deckInfo = await Task.WhenAll(deckQueryTasks.ToArray());
			decks = deckInfo.Where(d => d.HasValue).Select(d => d!.Value).ToList();

			return new KeyValuePair<string, List<DeckInfo>>(username, decks);
		}

		private async Task<DeckInfo?> LoadDeckInfo(string deckName, string deckId)
		{
			DeckInfo result = new DeckInfo();
			result.DeckName = deckName;
			string query = $"{deckEndpoint}{deckId}/";
			JToken? deckJson = await _httpClient.QueryPageForHTMLNode(query, archidektSelectStatement);
			if (deckJson == null)
			{
				_logger?.Log($"Failed to find Archidekt page JSON data for deckName: {deckName}. Will return null DeckInfo");
				return null;
			}

			JToken? categoryToken = deckJson.SelectToken("$..deck.categories");
			if (categoryToken != null)
			{
				result.CategoryInfo = GetCategoryInformationFromDeck(categoryToken);
			}
			else
			{
				_logger?.Log($"Failed to find category JSON token for deck {deckName}. CategoryInfo for this deck will be null");
			}

			Dictionary<int, CollectionCardInfo> cards = new Dictionary<int, CollectionCardInfo>();
			JToken? cardMapToken = deckJson.SelectToken("$..cardMap");
			if (cardMapToken != null)
			{
				foreach (JToken token in cardMapToken)
				{
					CollectionCardInfo cardInfo = new CollectionCardInfo();

					string? name = token.SelectTokens("$..name")?.Select(t => t.Value<string>()).FirstOrDefault(s => !string.IsNullOrEmpty(s));
					if (!string.IsNullOrEmpty(name))
					{
						cardInfo.Name = name;
					}
					else
					{
						_logger?.Log($"Failed to find name of card for deck: {deckName}. This card will not be added to current deck info");
						continue;
					}
					int? tcgId = token.SelectToken("$..ids.tcgId")?.Value<int>();
					if (tcgId != null)
					{
						cardInfo.TcgPlayerId = tcgId.Value;
					}
					else
					{
						_logger?.Log($"Failed to find TcgPlayerId of card {name} for deck: {deckName}. This card will not be added to current deck info");
						continue;
					}
					IEnumerable<string?>? cardCategories = token.SelectToken("$..categories")?.Values<string>();
					if (cardCategories != null)
					{
						cardInfo.Categories = cardCategories.Where(s => s != null).Select(s => s!).ToList();
					}
					else
					{
						_logger?.Log($"Failed to find categories for card: {name} for deck: {deckName}. Card will be missing category info");
					}
					int? qty = token.SelectToken("$..qty")?.Value<int>();
					if (qty != null)
					{
						cardInfo.Quantity = qty.Value;
					}
					else
					{
						_logger?.Log($"Failed to find quantity for card: {name} for deck: {deckName}. Card will be missing quantity");
					}

					cards.Add(tcgId.Value, cardInfo);
				}
			}
			result.CardsByTcgId = cards;

			return result;
		}

		private Dictionary<string, bool> GetCategoryInformationFromDeck(JToken categoryToken)
		{
			Dictionary<string, bool> result = new Dictionary<string, bool>();
			foreach (JToken category in categoryToken.Children())
			{
				string? categoryName = category.SelectToken("$..name")?.Value<string>();
				if (categoryName == null)
				{
					_logger?.Log("Failed to find category name... Skipping adding this category");
					continue;
				}

				bool? inDeck = category.SelectToken("$..includedInDeck")?.Value<bool>();
				if (inDeck != null)
				{
					// Special condition for Sideboard to not be included in the deck
					if (categoryName == "Sideboard") inDeck = false;

					result.Add(categoryName, inDeck.Value);
				}
				else
				{
					_logger?.Log($"Failed to find {categoryName}'s InDeck status... Skipping adding this category");
				}
			}
			return result;
		}
		#endregion

		private string CreateOutput(HashSet<string> queriedCardNames, Dictionary<string, List<CollectionCardInfo>> cardsByUser, Dictionary<string, List<DeckInfo>>? deckInfoByUser)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string user in cardsByUser.Keys)
			{
				sb.AppendLine($"From collection: {user}");

				List<CollectionCardInfo> cards = cardsByUser[user];
				if (cards.Count == 0)
				{
					sb.AppendLine("\tNo cards that were queried were found.");
					continue;
				}

				List<DeckInfo>? decks = null;
				if (deckInfoByUser != null)
				{
					if (!deckInfoByUser.TryGetValue(user, out decks))
					{
						sb.AppendLine("\tFailed to load decks for user.");
					}
				}

				foreach (CollectionCardInfo card in cards)
				{
					if (_config.AllowPartialMatches)
					{
						foreach(string queriedCard in queriedCardNames)
						{
							if (card.Name.Contains(queriedCard, StringComparison.OrdinalIgnoreCase)) queriedCardNames.Remove(queriedCard);
						}
					}
					else
					{
						queriedCardNames.Remove(card.Name);
					}
					sb.AppendLine($"\t- {card.Quantity}x {card.Name} | TcgPlayer Price: {card.Price:C2} | Foil: {card.Foil}");
					if (decks != null && decks.Count > 0)
					{
						bool foundInDecks = false;
						foreach (DeckInfo deck in decks)
						{
							if (deck.CardsByTcgId.TryGetValue(card.TcgPlayerId, out CollectionCardInfo deckCard))
							{
								if (!foundInDecks)
								{
									sb.AppendLine($"\t\tFound in the following decks by {user}:");
									foundInDecks = true;
								}
								bool inDeck = true;
								string categories = "";
								foreach (string cardCategory in deckCard.Categories)
								{
									if (deck.CategoryInfo.TryGetValue(cardCategory, out bool includedInDeck))
									{
										inDeck = includedInDeck && inDeck;
									}
									categories += $"{cardCategory}, ";
								}
								sb.AppendLine($"\t\t\t{deck.DeckName} | Deck Quantity: {deckCard.Quantity} | In main deck: {inDeck} | In categories: {categories}");
							}
						}
					}
				}
			}
			// Cards not found section
			sb.AppendLine("\nCards not found in any collection:");
			foreach (string card in queriedCardNames)
			{
				sb.AppendLine(card);
			}

			return sb.ToString();
		}

		public struct CollectionCardInfo
		{
			public string CollectionId;
			public string Name;
			public int Quantity;
			public bool Foil;
			public float Price;
			public int TcgPlayerId;
			public List<string> Categories;

			public override string ToString()
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"Card Name: {Name}");
				sb.AppendLine($"Quantity: {Quantity}");
				sb.AppendLine($"Foil: {Foil}");
				sb.AppendLine($"Price: {Price}");
				sb.AppendLine($"TcgPlayerId: {TcgPlayerId}");
				if (Categories != null)
				{
					sb.AppendLine($"Categories:");
					foreach (string category in Categories)
					{
						sb.AppendLine($"Category: {category}");
					}
				}
				return sb.ToString();
			}
		}

		public struct DeckInfo
		{
			public string DeckName;
			public Dictionary<string, bool> CategoryInfo;
			public Dictionary<int, CollectionCardInfo> CardsByTcgId;

			public override string ToString()
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"********** {DeckName} **********");
				sb.AppendLine($"Categories:");
				foreach (string category in CategoryInfo.Keys)
				{
					sb.AppendLine(category);
					sb.AppendLine(CategoryInfo[category].ToString());
				}
				sb.AppendLine("--------------");
				sb.AppendLine($"Cards:");
				foreach (int card in CardsByTcgId.Keys)
				{
					sb.AppendLine(CardsByTcgId[card].ToString());
				}
				sb.AppendLine("*******************************************");
				return sb.ToString();
			}
		}
	}
}
