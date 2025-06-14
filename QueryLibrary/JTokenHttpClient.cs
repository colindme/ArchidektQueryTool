using System.Net;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace QueryLibrary
{
	internal class JTokenHttpClient
	{
		readonly private HttpClient _client = new HttpClient();
		readonly private Random _random = new Random();
		readonly private Logger _logger;

		private const int _baseBackOffTimeMs = 1000;
		private const int _maxBackoffJitterMs = 10000;
		private const float _backoffExponentialFactorScaling = 0.5f;
		readonly private float _maxRetries;
		public JTokenHttpClient(string baseAddress, Logger logger, int maxRetries)
		{
			_client.BaseAddress = new Uri(baseAddress);
			_logger = logger;
			_maxRetries = maxRetries;
		}

		// Todo: Cancellation logic
		// Todo: Infinite retries (except for cancellation)
		// LOG HERE
		public async Task<JToken?> QueryPageForHTMLNode(string query, string htmlSelectStatement)
		{
			int retries = 0;
			try
			{
				while (true)
				{
					var response = await _client.GetAsync(query);
					if (response.StatusCode != System.Net.HttpStatusCode.OK)
					{
						if (IsHttpStatusCodeTransient(response.StatusCode))
						{
							if (retries == _maxRetries)
							{
								// Failed due too many retries
								_logger.Log($"");
								throw new Exception();
							}
							retries++;

							// Back off exponentially with jitter
							float backoffMs = float.Pow(_baseBackOffTimeMs, float.Min(1.0f, retries * _backoffExponentialFactorScaling)) + _random.Next(_maxBackoffJitterMs);
							_logger.Log($"");
							await Task.Delay((int)backoffMs);
							continue;
						}
						else
						{
							// Failed
							throw new Exception($"Got status code: {response.StatusCode}");
						}
					}

					string responseContent = await response.Content.ReadAsStringAsync();
					HtmlDocument html = new HtmlDocument();
					html.LoadHtml(responseContent);

					HtmlNode node = html.DocumentNode.SelectSingleNode(htmlSelectStatement);
					if (node == null)
					{
						// Retry stuff here?
						throw new Exception($"Node was null");
					}
					return JToken.Parse(node.InnerHtml);
				}
			}
			catch (Exception ex)
			{
				_logger.Log("");
				Console.WriteLine($"Encountered exception for query: {query} | exception: {ex.Message}");
				return null;
			}
		}

		private bool IsHttpStatusCodeTransient(HttpStatusCode code)
		{
			return (code == HttpStatusCode.TooManyRequests
				|| code == HttpStatusCode.RequestTimeout
				|| code == HttpStatusCode.BadGateway
				|| code == HttpStatusCode.ServiceUnavailable
				|| code == HttpStatusCode.GatewayTimeout);
		}
	}
}
