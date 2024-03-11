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
    public class DigestAuthFixer
    {
        private readonly string _host;
        private readonly string _user;
        private readonly string _password;
        private string _realm;
        private string _nonce;
        private string _qop;
        private string _cnonce;
        private DateTime _cnonceDate;
        private int _nc;

        public DigestAuthFixer(string host, string user, string password)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _password = password ?? throw new ArgumentNullException(nameof(password));
        }

        private string CalculateMd5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string GrabHeaderVar(string varName, string header)
        {
            var regHeader = new Regex(string.Format(@"{0}=""([^""]*)""", varName));
            var matchHeader = regHeader.Match(header);
            if (matchHeader.Success)
            {
                return matchHeader.Groups[1].Value;
            }
            throw new ApplicationException(string.Format("Header {0} not found", varName));
        }

        private string GetDigestHeader(string dir)
        {
            _nc++;

            var ha1 = CalculateMd5Hash($"{_user}:{_realm}:{_password}");
            var ha2 = CalculateMd5Hash($"GET:{dir}");
            var digestResponse = CalculateMd5Hash($"{ha1}:{_nonce}:{_nc:D8}:{_cnonce}:{_qop}:{ha2}");

            return $"Digest username=\"{_user}\", realm=\"{_realm}\", nonce=\"{_nonce}\", uri=\"{dir}\", " +
                $"algorithm=MD5, response=\"{digestResponse}\", qop={_qop}, nc={_nc:D8}, cnonce=\"{_cnonce}\"";
        }

        public string GrabResponse(
       string dir)
        {
            var url = _host + dir;
            var uri = new Uri(url);

            var request = (HttpWebRequest)WebRequest.Create(uri);

            // If we've got a recent Auth header, re-use it!
            if (!string.IsNullOrEmpty(_cnonce) &&
                DateTime.Now.Subtract(_cnonceDate).TotalHours < 1.0)
            {
                request.Headers.Add("Authorization", GetDigestHeader(dir));
            }

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                // Try to fix a 401 exception by adding a Authorization header
                if (ex.Response == null || ((HttpWebResponse)ex.Response).StatusCode != HttpStatusCode.Unauthorized)
                    throw;

                var wwwAuthenticateHeader = ex.Response.Headers["WWW-Authenticate"];
                _realm = GrabHeaderVar("realm", wwwAuthenticateHeader);
                _nonce = GrabHeaderVar("nonce", wwwAuthenticateHeader);
                _qop = GrabHeaderVar("qop", wwwAuthenticateHeader);

                _nc = 0;
                _cnonce = new Random().Next(123400, 9999999).ToString();
                _cnonceDate = DateTime.Now;

                var request2 = (HttpWebRequest)WebRequest.Create(uri);
                request2.Headers.Add("Authorization", GetDigestHeader(dir));
                response = (HttpWebResponse)request2.GetResponse();
            }
            var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();
        }
    }
}




