using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace cc_slack_api
{
    public class BitbucketServerClient
    {
        public static async Task<dynamic> GetPullRequestDetails(string bitbucketUsername, string bitbucketPassword, string projectKey, string repositorySlug, string pullRequestId)
        {
            byte[] authenticationBytes = Encoding.UTF8.GetBytes($"{bitbucketUsername}:{bitbucketPassword}");
            string authenticationString = Convert.ToBase64String(authenticationBytes);

            using (var bitbucketClient = new HttpClient())
            {
                bitbucketClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authenticationString);

                HttpResponseMessage responseMessage = await bitbucketClient.GetAsync($@"https://codebase-aws.clearcompany.com/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/pull-requests/{pullRequestId}");
                responseMessage.EnsureSuccessStatusCode();
                return await responseMessage.Content.ReadAsAsync<dynamic>();
            }
        }

        public static string GetGravatarImageUrl(string emailAddress)
        {
            byte[] emailAddressBytes = Encoding.UTF8.GetBytes(emailAddress.Trim().ToLowerInvariant());
            string gravatarHash;

            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(emailAddressBytes);
                gravatarHash = string.Concat(hashBytes.Select(b => b.ToString("x2")));
            }

            return $"https://www.gravatar.com/avatar/{gravatarHash}";
        }
    }
}