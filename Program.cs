using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Text;
using static ArchidektCollectionQueryProject.Program;

namespace ArchidektCollectionQueryProject
{
    class Program
    {
		// CommandLineArgs
		static string collectionFile = "";
		static string cardFile = "";
		static string collectionList = "";
		static string cardList = "";
		static string logFile = "outputLog.txt";
		static string outputFile = "output.txt";
		static bool allowPartialMatches = false;
		static bool includeDeckInfo = true;
		static bool logToConsole = true;
		static bool outputToConsole = true;
		// Conststrings
		const string profileEndpoint = "u/";
		const string collectionEndpoint = "collection/v2/";
		const string deckSearchEndpoint = "search/decks";
		const string deckEndpoint = "decks/";
		const string archidektDataId = "__NEXT_DATA__";
		const string userIdJsonIndicator = "user.id";
		const string archidektSelectStatement = $"//script[contains(@id, '{archidektDataId}')]";
		const string totalPageIndicator = "pageProps.totalPages";
		const string collectionCardsIndicator = "collectionV2.collectionCards";
		const string deckPageIndicator = "results";
		// Card fields
		const string cardNameIndicator = "card.name";
		const string cardQuantityIndicator = "quantity";
		const string foilIndicator = "foil";
		const string nonfoilPriceIndicator = "prices.tcg";
		const string foilPriceIndicator = "prices.tcgFoil";
		const string tcgPlayerIdIndicator = "ids.tcgId";

		static HttpClient Client = new HttpClient()
		{
			BaseAddress = new Uri("https://www.archidekt.com/")
		};

		static async Task Main(string[] args)
        {
			// Gathering required information for queries (ArchidektIDs, card names)
			Task<List<KeyValuePair<string, string>>> collectionTask = GetArchidektUserIDs("../../../collections.txt");
			Task<List<QueryCardInfo>> cards = GetCardNamesFromFile("../../../cards.txt");
			Task.WaitAll(collectionTask, cards);
			// Query archidekt for information (cards & deck information)
            List<Task<KeyValuePair<string, List<CollectionCardInfo>>>> cardQueryTaskList = new();
			List<Task<KeyValuePair<string, List<DeckInfo>>>> deckQueryTaskList = new();
			foreach (KeyValuePair<string,string> collection in collectionTask.Result)
			{
				if (includeDeckInfo)
				{
					//deckQueryTaskList.Add(LoadAllDeckInfoForUser(collection.Key));
				}
				foreach (QueryCardInfo card in cards.Result)
				{
					cardQueryTaskList.Add(QueryCollectionForCard(collection.Key, collection.Value, card, allowPartialMatches));
				}
            }
			Task.WaitAll(cardQueryTaskList.ToArray());
			Task.WaitAll(deckQueryTaskList.ToArray());
			// Take the query results and create the output
			// Have to filter down the card results to a Dictionary so the same entries aren't repeated (even if they were present in the request) - is this a safe assumption?
			Dictionary<string, Dictionary<string, CollectionCardInfo>> userToCardsDict = new Dictionary<string, Dictionary<string, CollectionCardInfo>>();
			foreach(KeyValuePair<string, List<CollectionCardInfo>> key in cardQueryTaskList.Select(c => c.Result))
			{ 

			}
			Dictionary<string, List<DeckInfo>> userToDecksDict = new Dictionary<string, List<DeckInfo>>();

			//string output = CreateOutput(new List<string>(), cardQueryTaskList.Select(c => c.Result).ToDictionary(), includeDeckInfo ? deckQueryTaskList.Select(d => d.Result).ToDictionary() : null);
			//Console.WriteLine(output);
		}

		/* TODO 
		 * Logging
		 */
		static public async Task<List<KeyValuePair<string, string>>> GetArchidektUserIDs(string path)
		{
			List<string> archidektUsernames = File.ReadAllLines(path).Select(line => line.Trim()).Where(line => !string.IsNullOrWhiteSpace(line)).Distinct().ToList();
			List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
			// Associate username with the task incase of a failure
			Dictionary<string, Task<string?>> archidektUserIdTaskMap = new Dictionary<string, Task<string?>>();
			foreach (string username in archidektUsernames)
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
					//Logging
				}
			}
			return result;
		}

		// TODO: Support staggering retries in case of 403 error?
		static async public Task<string?> GetArchidektUserID(string username)
		{
			// TODO add staggering and retries to the HTTP Client
			var response = await Client.GetAsync(profileEndpoint + username);
			if (response.StatusCode != System.Net.HttpStatusCode.OK)
			{
				// TODO: Logging here?
				return null;
			}

			var responseContent = await response.Content.ReadAsStringAsync();
			var html = new HtmlDocument();
			html.LoadHtml(responseContent);
			HtmlNode? node = html.DocumentNode.SelectSingleNode(archidektSelectStatement);
			string? userId = JToken.Parse(node.InnerHtml)?.SelectToken($"$..{userIdJsonIndicator}")?.Value<string>();
			return userId;
		}

		// TODO: Text box input?
		// TODO: Cancellation logic
		static public Task<List<QueryCardInfo>> GetCardNamesFromFile(string path)
		{
			List<string> lines = File.ReadAllLines(path).ToList();
			List<QueryCardInfo> result = new List<QueryCardInfo>();

			foreach (string line in lines)
			{
				QueryCardInfo? cardInfo = GetCardInfoFromString(line);
				if (cardInfo.HasValue)
				{
					result.Add(cardInfo.Value);
				}
				else
				{
					// Log failure to parse line here!
				}
			}
			return Task.FromResult(result);
		}

		static public QueryCardInfo? GetCardInfoFromString(string text)
		{
			QueryCardInfo? card = null;
			if(string.IsNullOrEmpty(text))
			{
				return card;
			}

			int quantity = 0;
			// Grab the quantity (if it exists)
			if (Char.IsDigit(text[0]))
			{
				int spaceIndex = text.IndexOf(" ");
				string number = new string(text.Substring(0, spaceIndex).Where(Char.IsDigit).ToArray());
				quantity = int.Parse(number);
			}

			int firstAlphabeticCharIndex = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (Char.IsLetter(text[i]))
				{
					firstAlphabeticCharIndex = i;
					break;
				}
			}

			text = text.Substring(firstAlphabeticCharIndex);
			if (!string.IsNullOrEmpty(text))
			{
				card = new QueryCardInfo() { Name = text, Quantity = quantity };
			}

			return card;
		}

		static public async Task<KeyValuePair<string, List<CollectionCardInfo>>> QueryCollectionForCard(string username, string collectionId, QueryCardInfo queryCardInfo, bool allowPartialMatches)
		{
			List<CollectionCardInfo> foundCards = new List<CollectionCardInfo>();
			try
			{
				// For whatever reason, spaces breaks the query so we have to only search by the first word and filter down from there
				string queryCardName = queryCardInfo.Name;
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
					var response = await Client.GetAsync(query);
					// Support backing off incase of 403
					if (response.StatusCode != System.Net.HttpStatusCode.OK)
					{
						// TODO: Error log here
					}

					string responseContent = await response.Content.ReadAsStringAsync();
					HtmlDocument html = new HtmlDocument();
					html.LoadHtml(responseContent);
					HtmlNode node = html.DocumentNode.SelectSingleNode(archidektSelectStatement);
					if (node == null)
					{
						throw new Exception();
					}
					JToken json = JToken.Parse(node.InnerHtml);

					// If this is the first page we are parsing, parse the data for the total records / pages
					if (currentPage == 1)
					{
						JToken? pageToken = json.SelectToken($"$..{totalPageIndicator}");
						if (pageToken == null)
						{
							throw new Exception($"Failed to find the total page token");
						}

						// Save the total page count
						totalPages = pageToken.Value<int>();
					}

					// Parse the collection of cards
					JToken? collectionToken = json.SelectToken($"$..{collectionCardsIndicator}");
					if (collectionToken == null)
					{
						throw new Exception($"Failed to find card collection for card: {queryCardInfo.Name}");
					}
					
					foreach(JToken card in collectionToken.Children())
					{
						JToken? cardNameToken = card.SelectToken($"$..{cardNameIndicator}");
						if (cardNameToken != null)
						{
							string? cardName = cardNameToken.Value<string>();
							if (cardName == null)
							{
								Console.WriteLine($"CardNameToken's value was null");
								continue;
							}
							if ((!allowPartialMatches && cardName == queryCardInfo.Name) || (allowPartialMatches && cardName.Contains(queryCardInfo.Name)))
							{
								// Found a match, add the info!
								CollectionCardInfo cardInfo = new CollectionCardInfo();
								cardInfo.Name = cardName;
								cardInfo.Errors = new List<string>();
								cardInfo.CollectionId = collectionId;
								// Get the Quantity
								JToken? quantityToken = card.SelectToken($"$..{cardQuantityIndicator}");
								if (quantityToken != null)
								{
									cardInfo.Quantity = quantityToken.Value<int>();
								}
								else
								{
									cardInfo.Errors.Add("Failed to find token for quantity");
								}
								// Get the Foil status
								JToken? foilToken = card.SelectToken($"$..{foilIndicator}");
								if (foilToken != null)
								{
									cardInfo.Foil = foilToken.Value<bool>();
								}
								else
								{
									cardInfo.Errors.Add("Failed to find token for Foil");
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
									cardInfo.Errors.Add($"Failed to find price token for foil status: {cardInfo.Foil}");
								}

								// Get the TcgPlayerId
								JToken? tcgIdToken = card.SelectToken($"$..{tcgPlayerIdIndicator}");
								if (tcgIdToken != null)
								{
									cardInfo.TcgPlayerId = tcgIdToken.Value<int>();
								}
								else
								{
									cardInfo.Errors.Add("Failed to find TCGPlayerId");
								}
								foundCards.Add(cardInfo);
							}
						}
						else
						{
							// log something here?
							Console.WriteLine($"Failed to find token for cardname: {queryCardInfo.Name}");
						}
					}

					currentPage++;
				}
				while (currentPage <= totalPages);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return new KeyValuePair<string, List<CollectionCardInfo>>(username, foundCards);
		}

		static public async Task<KeyValuePair<string, List<DeckInfo>>> LoadAllDeckInfoForUser(string username)
		{
			List<DeckInfo> decks = new List<DeckInfo>();
			string query = $"{deckSearchEndpoint}?ownerUsername={username}";
			JToken json = await QueryAchidektPageForData(query);
			JToken? resultsToken = json.SelectToken($"$..{deckPageIndicator}");
			List<Task<DeckInfo>> deckQueryTasks = new List<Task<DeckInfo>>();
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
							Console.WriteLine("DeckId was NULL");
						}
					}
					else
					{
						Console.WriteLine("DeckIdToken was NULL");
					}
				}
			}
			else
			{
				Console.WriteLine("Deck results was NULL");
			}
			DeckInfo[]? deckInfo = await Task.WhenAll(deckQueryTasks.ToArray());
			if (deckInfo != null)
			{
				decks = deckInfo.ToList();
			}
			else
			{
				// Log here
			}
			return new KeyValuePair<string, List<DeckInfo>>(username, decks);
		}

		static public async Task<DeckInfo> LoadDeckInfo(string deckName, string deckId)
		{
			DeckInfo result = new DeckInfo();
			result.DeckName = deckName;
			string query = $"{deckEndpoint}{deckId}/";
			JToken deckJson = await QueryAchidektPageForData(query);

			JToken? categoryToken = deckJson.SelectToken("$..deck.categories");
			if (categoryToken != null)
			{
				result.CategoryInfo = GetCategoryInformationFromDeck(categoryToken);
			}
			else
			{
				// Log here
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
						Console.WriteLine($"Failed to find name of card for deckId: {deckId}");
						continue;
					}
					int? tcgId = token.SelectToken("$..ids.tcgId")?.Value<int>();
					if (tcgId != null)
					{
						cardInfo.TcgPlayerId = tcgId.Value;
					}
					else
					{
						Console.WriteLine($"Failed to find tcgId for card: {name}");
						continue;
					}
					IEnumerable<string?>? cardCategories = token.SelectToken("$..categories")?.Values<string>();
					if (cardCategories != null)
					{
						cardInfo.Categories = cardCategories.Where(s => s != null).Select(s => s!).ToList();
					}
					else
					{
						Console.WriteLine($"Failed to find categories for card: {name}");
					}
					int? qty = token.SelectToken("$..qty")?.Value<int>();
					if (qty != null)
					{
						cardInfo.Quantity = qty.Value;
					}
					else
					{
						Console.WriteLine($"Failed to find quantity for card name: {name}");
					}

					cards.Add(tcgId.Value, cardInfo);
				}
			}
			result.CardsByTcgId = cards;
			
			return result;
		}

		static public Dictionary<string, bool> GetCategoryInformationFromDeck(JToken categoryToken)
		{
			Dictionary<string, bool> result = new Dictionary<string, bool>();
			foreach (JToken category in categoryToken.Children())
			{
				string? categoryName = category.SelectToken("$..name")?.Value<string>();
				if (categoryName == null)
				{
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
					// Log here
				}
			}
			return result;
		}

		static public async Task<JToken> QueryAchidektPageForData(string query)
		{
			var response = await Client.GetAsync(query);
			// Support backing off incase of 403
			if (response.StatusCode != System.Net.HttpStatusCode.OK)
			{
				// TODO: Error log here
			}

			string responseContent = await response.Content.ReadAsStringAsync();
			HtmlDocument html = new HtmlDocument();
			html.LoadHtml(responseContent);
			HtmlNode node = html.DocumentNode.SelectSingleNode(archidektSelectStatement);
			if (node == null)
			{
				throw new Exception();
			}
			return JToken.Parse(node.InnerHtml);
		}

		static string CreateOutput(List<string> queriedCardNames, Dictionary<string, List<CollectionCardInfo>> cardsByUser, Dictionary<string, List<DeckInfo>>? deckInfoByUser)
		{
            StringBuilder sb = new StringBuilder();
            foreach (string user in cardsByUser.Keys)
			{
				sb.Clear();
				sb.AppendLine($"From collection: {user}");

                List<CollectionCardInfo> cards = cardsByUser[user];
				if (cards.Count == 0)
				{
					sb.AppendLine("\tNo cards that were queried were found.");
					Console.WriteLine(sb.ToString());
					continue;
				}

				List<DeckInfo>? decks = null;
				if (includeDeckInfo && deckInfoByUser != null)
				{
					if (!deckInfoByUser.TryGetValue(user, out decks))
					{
						// Log failure here
					}
				}
				
				foreach(CollectionCardInfo card in cards)
				{
					sb.AppendLine($"\t- {card.Quantity}x {card.Name} | TcgPlayer Price: {card.Price:C2} | Foil: {card.Foil}");
					if (decks != null && decks.Count > 0)
					{
						bool foundInDecks = false;
						foreach (DeckInfo deck in decks)
						{
							if(deck.CardsByTcgId.TryGetValue(card.TcgPlayerId, out CollectionCardInfo deckCard))
							{
								if (!foundInDecks)
								{
									sb.AppendLine($"\t\tFound in the following {user}'s decks:");
									foundInDecks = true;
								}
								bool inDeck = true;
								string categories = "";
								foreach(string cardCategory in deckCard.Categories)
								{
									if (deck.CategoryInfo.TryGetValue(cardCategory, out bool includedInDeck))
									{
										inDeck = includedInDeck && inDeck;
									}
									categories += $"{cardCategory}, ";
								}
								sb.AppendLine($"\t\t{deck.DeckName} | Deck Quantity: {deckCard.Quantity} | In main deck: {inDeck} | In categories: {categories}");
							}
						}
					}
				}
			}
			// Cards not found in anyone's collection section

			return sb.ToString();
		}

		public struct QueryCardInfo
		{
			public string Name;
			public int Quantity;
		}

		public struct CollectionCardInfo
		{
			public string CollectionId;
			public string Name;
			public int Quantity;
			public bool Foil;
			public float Price;
			public int TcgPlayerId;
			public List<string> Errors;
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
