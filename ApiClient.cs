using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JCScanhubClockInSystem
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        public ApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");
        }

        public async Task<string> ProcessApiRequestAsync(string serverAddress, string resourcePath, string queryParameters)
        {
            try
            {

                string apiUrl1 = "http://192.168.1.108/cgi-bin/recordFinder.cgi?action=find&name=AccessControlCardRec";
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl1);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        AuthenticationHeaderValue authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();

                        if (authHeader != null)
                        {
                            var authParameters = ParseAuthParameters(authHeader.Parameter);

                            // Now you have realm, qop, nonce, and opaque for Digest authentication
                            Console.WriteLine($"realm= {authParameters["realm"]}, qop= {authParameters["qop"]}, nonce: {authParameters["nonce"]}, opaque: {authParameters["opaque"]}");
                        }
                    }
                }
                
                // Process the JSON response as needed
                return apiUrl1;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
                return null; // Or handle the error in an appropriate way
            }
           
        }
        
        // Helper method to parse authentication parameters from the header
        private static Dictionary<string, string> ParseAuthParameters(string header)
        {
            var parameters = new Dictionary<string, string>();
            string pattern = @"(\w+)=""([^""]*)""";
            MatchCollection matches = Regex.Matches(header, pattern);

            foreach (Match match in matches)
            {
                parameters.Add(match.Groups[1].Value, match.Groups[2].Value);
            }

            return parameters;
        }

        public async Task<string> ProcessApiRequestWithDigestAuthAsync(string serverAddress, string resourcePath, string username, string password)
        {
            try
            {
                // Build the complete URL
                string apiUrl = $"{serverAddress}{resourcePath}";

                // Make the first unauthenticated request
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // Parse WWW-Authenticate header to get required values
                        AuthenticationHeaderValue authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();
                        if (authHeader != null)
                        {
                            var authParameters = ParseAuthParameters(authHeader.Parameter);

                            // Prepare values for Digest Authentication
                            string method = "GET"; // Replace with your HTTP method (GET, POST, etc.)
                            string uri = $"{serverAddress}{resourcePath}";
                            string cnonce = GenerateCnonce();
                            int nonceCounter = 1; // Increment for each request

                            // Generate Authorization header for the second request
                            string authorizationHeader = GenerateAuthorizationHeader(username, password, method, resourcePath, authParameters, cnonce);

                            // Use the authorizationHeader in the actual request
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Digest", authorizationHeader);

                            // Make the actual request
                            var json = await _httpClient.GetStringAsync(apiUrl);

                            // Process the JSON response as needed
                            return json;
                        }
                    }

                    // Handle the case where WWW-Authenticate header is not present or authentication is not required
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
                return null; // Or handle the error in an appropriate way
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return null; // Or handle the error in an appropriate way
            }
        }
        // Helper method to generate a random cnonce
        private static string GenerateCnonce()
        {
            // Generate an 8-character nonce
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private static string GenerateAuthorizationHeader(string username, string password, string method, string uri, Dictionary<string, string> authParameters, string cnonce)
        {
            string response = CalculateDigestResponse(username, password, method, uri, authParameters["nonce"], 1, cnonce, authParameters["qop"], authParameters["realm"]);

            // Include algorithm, nc, and opaque in the header
            return $"Digest username=\"{username}\", realm=\"{authParameters["realm"]}\", nonce=\"{authParameters["nonce"]}\", uri=\"{uri}\", cnonce=\"{cnonce}\", qop=\"{authParameters["qop"]}\", nc=00000001, response=\"{response}\", opaque=\"{authParameters["opaque"]}\", algorithm=\"MD5\"";
        }
        // Helper method to calculate Digest response using MD5
        private static string CalculateDigestResponse(string username, string password, string method, string uri, string nonce, int nonceCounter, string cnonce, string qop, string realm)
        {
            // Calculate MD5 hashes
            string hash1 = CalculateMD5($"{username}:{realm}:{password}");
            string hash2 = CalculateMD5($"{method}:{uri}");

            // Calculate response hash
            string response = CalculateMD5($"{hash1}:{nonce:D8}:{nonceCounter:D8}:{cnonce}:{qop}:{hash2}");
            return response;
        }

        // Helper method to calculate MD5 hash of a string
        private static string CalculateMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }


                return sb.ToString();
            }
        }

    }
}
