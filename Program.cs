using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;

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
		static bool exactMatch = false;
		static bool includeDeckInfo = false;
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



			Task<List<KeyValuePair<string, string>>> collectionTask = Task.Run(() => GetArchidektUserIDs("D:\\ArchidektCollectionQueryTool\\ArchidektCollectionQueryProject\\collections.txt"));
			Task<List<QueryCardInfo>> cards = Task.Run(() => GetCardNamesFromFile("D:\\ArchidektCollectionQueryTool\\ArchidektCollectionQueryProject\\cards.txt"));

			Task.WaitAll(collectionTask, cards);

			List<Task<List<CollectionCardInfo>>> cardQueryTaskList = new List<Task<List<CollectionCardInfo>>>();
			List<Task> deckQueryTaskList = new List<Task>();
			foreach (KeyValuePair<string,string> collection in collectionTask.Result)
			{
				foreach(QueryCardInfo card in cards.Result)
				{
					// Query Archidekt
					//cardQueryTaskList.Add(Task.Run(() => QueryCollectionForCard(collection.Value, card, exactMatch)));
				}
				deckQueryTaskList.Add(LoadDeckInfoForUser(collection.Key));
			}
			Task.WaitAll(cardQueryTaskList.ToArray());
			Task.WaitAll(deckQueryTaskList.ToArray());
			foreach(Task<List<CollectionCardInfo>> returnedCardInfo in cardQueryTaskList)
			{
				List<CollectionCardInfo> result = returnedCardInfo.Result;
				foreach (CollectionCardInfo collectionCardInfo in result)
				{
					Console.WriteLine("**********");
					Console.WriteLine($"Found card info for collection: {collectionCardInfo.CollectionId} | Card: {collectionCardInfo.Name} | Price: {collectionCardInfo.Price:C2} | Quantity: {collectionCardInfo.OwnedQuantity} | IsFoil: {collectionCardInfo.Foil} | TcgPlayerId: {collectionCardInfo.TcgPlayerId}");
					if (collectionCardInfo.Errors.Count > 0)
					{
						Console.WriteLine("Found the following errors");
						foreach (string error in collectionCardInfo.Errors)
							{ Console.WriteLine(error); }
					}
				}
			}

			CreateOutput();
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
		static public List<QueryCardInfo> GetCardNamesFromFile(string path)
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
			return result;
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

		static public async Task<List<CollectionCardInfo>> QueryCollectionForCard(string collectionId, QueryCardInfo queryCardInfo, bool exactMatch)
		{
			List<CollectionCardInfo> result = new List<CollectionCardInfo>();
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
							if ((exactMatch && cardName == queryCardInfo.Name) || (!exactMatch && cardName.Contains(queryCardInfo.Name)))
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
									cardInfo.OwnedQuantity = quantityToken.Value<int>();
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
								result.Add(cardInfo);
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
			return result;
		}

		static public async Task LoadDeckInfoForUser(string username)
		{
			string query = $"{deckSearchEndpoint}?ownerUsername={username}";
			JToken json = await QueryAchidektPageForData(query);
			JToken? resultsToken = json.SelectToken($"$..{deckPageIndicator}");
			List<Task> deckQueryTasks = new List<Task>();
			if (resultsToken != null)
			{
				foreach (JToken result in resultsToken.Children())
				{
					JToken? deckIdToken = result.SelectToken("id");
					if (deckIdToken != null)
					{
						string? deckId = deckIdToken.Value<string>();
						if (deckId != null)
						{
							await(GetCardsInDeck(deckId));
							//deckQueryTasks.Add(GetCardsInDeck(deckId));
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
			await Task.WhenAll(deckQueryTasks.ToArray());
		}

		static public async Task<List<CollectionCardInfo>> GetCardsInDeck(string deckId)
		{
			List<CollectionCardInfo> cards = new List<CollectionCardInfo>();
			string query = $"{deckEndpoint}{deckId}/";
			JToken deckJson = await QueryAchidektPageForData(query);

			Dictionary<string, CategoryInfo> categories;
			JToken? categoryToken = deckJson.SelectToken("$..deck.categories");
			if (categoryToken != null)
			{
				categories = GetCategoryInformationFromDeck(categoryToken);
			}

			JToken? cardMapToken = deckJson.SelectToken("$..cardMap");
			if (cardMapToken != null)
			{
				Console.WriteLine($"Cards: {cardMapToken.Children().Count()}");
				foreach (JToken token in cardMapToken)
				{
					CollectionCardInfo cardInfo = new CollectionCardInfo();

					string? name = token.SelectToken("$..name")?.Value<string>();
					if (name != null)
					{
						Console.WriteLine($"DeckId: {deckId} | Found card named: {name}");
					}
					else
					{
						Console.WriteLine($"");
					}
					IEnumerable<string?>? cardCategories = token.SelectToken("$..categories")?.Values<string>();
					if (cardCategories != null)
					{

					}
					else
					{

					}
					int? tcgId = token.SelectToken("$..ids.tcgId")?.Value<int>();
					if (tcgId != null)
					{

					}
					else
					{

					}
					int? qty = token.SelectToken("$..qty")?.Value<int>();
					if (qty != null)
					{

					}
					else
					{

					}

					cards.Add(cardInfo);
				}
			}
			
			return cards;
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

		static public Dictionary<string, CategoryInfo> GetCategoryInformationFromDeck(JToken deckToken)
		{
			Dictionary<string, CategoryInfo> result = new Dictionary<string, CategoryInfo>();
			JToken? categoryHolder = deckToken.SelectToken("..deck.categories");
			if (categoryHolder != null)
			{
				foreach (JToken category in categoryHolder.Children())
				{
					string? categoryName = category.SelectToken("name")?.Value<string>();
					if (categoryName == null)
					{
						continue;
					}
					CategoryInfo categoryInfo = new CategoryInfo();
					categoryInfo.Name = categoryName;

					bool? inDeck =  category.SelectToken("includedInDeck")?.Value<bool>();
					if (inDeck != null)
					{
						categoryInfo.InDeck = inDeck.Value;
					}
					else
					{
					
					}
					bool? isPremier = category.SelectToken("isPremier")?.Value<bool>();
					if (isPremier != null)
					{
						categoryInfo.IsPremier = isPremier.Value;
					}
					else
					{

					}
					result.Add(categoryName, categoryInfo);
				}
			}
			return result;
		}

		static void CreateOutput()
		{

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
			public int OwnedQuantity;
			public bool Foil;
			public float Price;
			public int TcgPlayerId;
			public List<string> Errors;
		}

		public struct CategoryInfo
		{
			public string Name;
			public bool InDeck;
			public bool IsPremier;
		}
	}
}
