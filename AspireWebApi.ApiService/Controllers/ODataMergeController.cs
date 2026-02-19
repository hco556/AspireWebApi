using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AspireWebApi.ApiService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ODataMergeController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ODataMergeController> _logger;

        public ODataMergeController(HttpClient httpClient, ILogger<ODataMergeController> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Calls multiple OData APIs and merges the results into a single collection.
        /// </summary>
        /// <param name="request">Request containing the OData API endpoints and merge configuration</param>
        /// <returns>Merged collection of results</returns>
        /// GET /api/odatamerge/merge?apis=https://api1.com/odata/items,https://api2.com/odata/products
        [HttpGet("merge")]
        public async Task<IActionResult> GetMergedODataResults([FromQuery] string? apis = null)
        {
            try
            {
                if (string.IsNullOrEmpty(apis))
                {
                    return BadRequest("No APIs provided. Use ?apis=url1,url2,url3");
                }

                var apiUrls = apis.Split(',').Select(a => a.Trim()).ToList();
                var mergedResults = new List<Dictionary<string, object>>();

                foreach (var url in apiUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var results = ParseODataResponse(content);
                            mergedResults.AddRange(results);
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to call API {url}: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error calling API {url}: {ex.Message}");
                    }
                }

                return Ok(new
                {
                    Count = mergedResults.Count,
                    Data = mergedResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetMergedODataResults: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while merging OData results", Details = ex.Message });
            }
        }

        /// <summary>
        /// Generic method to merge OData results based on a key property.
        /// </summary>
        /// POST /api/odatamerge/merge-by-key
        ////{
        ////  "apis": ["https://api1.com/odata/users", "https://api2.com/odata/profiles"],
        ////  "mergeByKey": "id"
        ////}
        [HttpPost("merge-by-key")]
        public async Task<IActionResult> MergeODataByKey([FromBody] ODataMergeRequest request)
        {
            try
            {
                if (request?.Apis == null || request.Apis.Count == 0)
                {
                    return BadRequest("No APIs provided in the request");
                }

                var allResults = new List<Dictionary<string, object>>();

                // Fetch data from all APIs
                foreach (var apiUrl in request.Apis)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var results = ParseODataResponse(content);
                            allResults.AddRange(results);
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to call API {apiUrl}: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error calling API {apiUrl}: {ex.Message}");
                    }
                }

                // Merge results by key property if specified
                var mergedData = request.MergeByKey != null
                    ? MergeByKeyProperty(allResults, request.MergeByKey)
                    : allResults;

                return Ok(new
                {
                    Count = mergedData.Count,
                    Data = mergedData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in MergeODataByKey: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while merging OData results", Details = ex.Message });
            }
        }

        /// <summary>
        /// Generic method that accepts a type parameter to strongly type the results.
        /// </summary>
        [HttpPost("merge-typed")]
        public async Task<IActionResult> MergeODataTyped<T>([FromBody] ODataMergeRequest request) where T : class, new()
        {
            try
            {
                if (request?.Apis == null || request.Apis.Count == 0)
                {
                    return BadRequest("No APIs provided in the request");
                }

                var mergedResults = new List<T>();

                foreach (var apiUrl in request.Apis)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var typedResults = ParseODataResponseToType<T>(content);
                            mergedResults.AddRange(typedResults);
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to call API {apiUrl}: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error calling API {apiUrl}: {ex.Message}");
                    }
                }

                return Ok(new
                {
                    Count = mergedResults.Count,
                    Data = mergedResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in MergeODataTyped: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while merging OData results", Details = ex.Message });
            }
        }

        /// <summary>
        /// Parses OData response (assuming JSON format with 'value' property)
        /// </summary>
        private List<Dictionary<string, object>> ParseODataResponse(string jsonContent)
        {
            var results = new List<Dictionary<string, object>>();

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;

                // OData responses typically have a "value" property containing the array
                if (root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in valueElement.EnumerateArray())
                    {
                        var dict = JsonElementToDictionary(item);
                        results.Add(dict);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // Handle case where the root is directly an array
                    foreach (var item in root.EnumerateArray())
                    {
                        var dict = JsonElementToDictionary(item);
                        results.Add(dict);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Parses OData response to a specific type
        /// </summary>
        private List<T> ParseODataResponseToType<T>(string jsonContent) where T : class, new()
        {
            var results = new List<T>();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in valueElement.EnumerateArray())
                    {
                        var obj = JsonSerializer.Deserialize<T>(item.GetRawText(), options);
                        if (obj != null)
                        {
                            results.Add(obj);
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var obj = JsonSerializer.Deserialize<T>(item.GetRawText(), options);
                        if (obj != null)
                        {
                            results.Add(obj);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Converts a JsonElement to a Dictionary
        /// </summary>
        private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
        {
            var dict = new Dictionary<string, object>();

            foreach (var property in element.EnumerateObject())
            {
                dict[property.Name] = JsonElementToObject(property.Value);
            }

            return dict;
        }

        /// <summary>
        /// Converts a JsonElement to an object
        /// </summary>
        private object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? (object)intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
                JsonValueKind.Object => JsonElementToDictionary(element),
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// Merges results by grouping on a key property and combining properties
        /// </summary>
        private List<Dictionary<string, object>> MergeByKeyProperty(
            List<Dictionary<string, object>> allResults,
            string keyProperty)
        {
            var mergedData = new Dictionary<object, Dictionary<string, object>>();

            foreach (var item in allResults)
            {
                if (item.TryGetValue(keyProperty, out var keyValue))
                {
                    if (mergedData.ContainsKey(keyValue))
                    {
                        // Merge properties into existing entry
                        foreach (var kvp in item)
                        {
                            if (!mergedData[keyValue].ContainsKey(kvp.Key))
                            {
                                mergedData[keyValue][kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    else
                    {
                        mergedData[keyValue] = new Dictionary<string, object>(item);
                    }
                }
            }

            return mergedData.Values.ToList();
        }
    }

    /// <summary>
    /// Request model for OData merge operations
    /// </summary>
    public class ODataMergeRequest
    {
        public List<string> Apis { get; set; } = new();
        public string? MergeByKey { get; set; }
    }
}
