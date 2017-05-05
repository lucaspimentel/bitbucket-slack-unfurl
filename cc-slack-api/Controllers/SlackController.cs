using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;

namespace cc_slack_api.Controllers
{
    public class SlackController : ApiController
    {
        [Route("slack/event")]
        [HttpPost]
        public IHttpActionResult PostEvent(dynamic data)
        {
            if (data == null)
            {
                return BadRequest("No data");
            }

            string token = data.token;
            string type = data.type;
            string challenge = data.challenge;

            if (token == null)
            {
                return BadRequest("No token");
            }

            if (token != ConfigurationManager.AppSettings["slack_verification_token"])
            {
                return BadRequest("Wrong token");
            }

            if (type == "url_verification")
            {
                if (string.IsNullOrWhiteSpace(challenge))
                {
                    return BadRequest("No challenge");
                }

                return Ok(challenge);
            }

            if (type == "event_callback")
            {
                Task.Run(() => HandleEvent((object)data));
            }

            return Ok();
        }

        private async void HandleEvent(dynamic data)
        {
            string eventType = data.@event.type;

            if (eventType == "link_shared")
            {
                if ((string) data.team_id != ConfigurationManager.AppSettings["slack_team_id"])
                {
                    return;
                }

                if ((string) data.api_app_id != ConfigurationManager.AppSettings["slack_app_id"])
                {
                    return;
                }

                var unfurls = new Dictionary<string, object>();

                foreach (var link in data.@event.links)
                {
                    if (link.domain == "codebase-aws.clearcompany.com")
                    {
                        string linkUrl = (string) link.url;
                        Match match = Regex.Match(linkUrl, @"https://codebase-aws\.clearcompany\.com/projects/(\w+)/repos/(\w+)/pull-requests/(\d+).*");

                        if (match.Success)
                        {
                            string projectKey = match.Groups[1].Value;
                            string repositorySlug = match.Groups[2].Value;
                            string pullRequestId = match.Groups[3].Value;

                            string bitbucketUsername = ConfigurationManager.AppSettings["bitbucket_username"];
                            string bitbucketPassword = ConfigurationManager.AppSettings["bitbucket_password"];
                            HttpResponseMessage responseMessage = await GetBitbucketPullRequest(bitbucketUsername, bitbucketPassword, projectKey, repositorySlug, pullRequestId);
                            dynamic response = await responseMessage.Content.ReadAsAsync<dynamic>();

                            string sourceBranch = ((string) response.fromRef.id).Replace("refs/heads/", "");
                            string destinationBranch = ((string) response.toRef.id).Replace("refs/heads/", "");

                            string originalDescription = response.description;
                            string[] descriptionLines = originalDescription.Split(new[] {"\r\n", "\n", "\r"}, StringSplitOptions.None);

                            string description = string.Join("\n", descriptionLines.Take(3))
                                                       .Replace("&", "&amp;")
                                                       .Replace("<", "&lt;")
                                                       .Replace(">", "&gt;");

                            var attachment = new
                                             {
                                                 //fallback = "test fallback text",
                                                 color = "#36a64f",
                                                 pretext = $"From `{sourceBranch}` to `{destinationBranch}",
                                                 author_name = (string) response.author.user.displayName,
                                                 author_link = $"https://codebase-aws.clearcompany.com/users/{response.author.user.slug}",
                                                 //author_icon = "",
                                                 title = (string) response.title,
                                                 title_link = (string) response.links.self[0].href,
                                                 text = description,
                                                 fields = new[]
                                                          {
                                                              new
                                                              {
                                                                  title = "State",
                                                                  value = $"{response.state}",
                                                                  @short = true
                                                              }
                                                          },
                                                 //image_url = "http=//my-website.com/path/to/image.jpg",
                                                 //thumb_url = "http=//example.com/path/to/thumb.png",
                                                 footer = "Bitbucket Server",
                                                 //footer_icon = "https=//platform.slack-edge.com/img/default_application_icon.png",
                                                 //ts = 123456789,
                                                 mrkdwn_in = new[] {"text", "pretext"}
                                             };

                            unfurls.Add(linkUrl, attachment);
                        }
                    }
                }

                if (unfurls.Count > 0)
                {
                    var postData = new Dictionary<string, string>
                                   {
                                       {"token", ConfigurationManager.AppSettings["slack_oauth_token"]},
                                       {"channel", (string) data.@event.channel},
                                       {"ts", (string) data.@event.message_ts},
                                       {"unfurls", JsonConvert.SerializeObject(unfurls)}
                                   };

                    await PostToSlack("chat.unfurl", postData);
                }
            }
        }

        private static async Task<HttpResponseMessage> GetBitbucketPullRequest(string bitbucketUsername, string bitbucketPassword, string projectKey, string repositorySlug, string pullRequestId)
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

        private static async Task PostToSlack(string endpoint, IDictionary<string, string> data)
        {
            string urlEncodedData = UrlEncode(data);

            using (var slackClient = new HttpClient())
            {
                var stringContent = new StringContent(urlEncodedData, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage postResponse = await slackClient.PostAsync($"https://slack.com/api/{endpoint}", stringContent);
                postResponse.EnsureSuccessStatusCode();
                dynamic result = await postResponse.Content.ReadAsAsync<dynamic>();
            }
        }

        private static string UrlEncode(IEnumerable<KeyValuePair<string, string>> data)
        {
            return string.Join("&", data.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
        }
    }
}