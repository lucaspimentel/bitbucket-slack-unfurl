using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace cc_slack_api.Controllers
{
    public class BitbucketServerClient
    {
        public static async Task<HttpResponseMessage> GetPullRequest(string bitbucketUsername, string bitbucketPassword, string projectKey, string repositorySlug, string pullRequestId)
        {
            byte[] authenticationBytes = Encoding.UTF8.GetBytes($"{bitbucketUsername}:{bitbucketPassword}");
            string authenticationString = Convert.ToBase64String(authenticationBytes);

            using (var bitbucketClient = new HttpClient())
            {
                bitbucketClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authenticationString);

                HttpResponseMessage responseMessage = await bitbucketClient.GetAsync($@"https://codebase-aws.clearcompany.com/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/pull-requests/{pullRequestId}");
                responseMessage.EnsureSuccessStatusCode();
                return responseMessage;
            }
        }
    }
}