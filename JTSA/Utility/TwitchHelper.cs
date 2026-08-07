using JTSA.Forms.TwitchIF;
using JTSA.TwitchIF;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;
using TwitchLib.Client;

namespace JTSA.Utility
{
    static class TwitchHelper
    {
        public static string ClientID { get; } = "tbpy1q9lh9pkyrqhde6o4f4dkq9rj0";

        public static string RedirectUri = @"http://localhost:8080/";
        public static string BroadcasterId = "";

        public static string AccessToken { get { return api.Settings.AccessToken; } set { api.Settings.AccessToken = value; }}

        public static readonly TwitchAPI api;

        static TwitchHelper()
        {
            api = new TwitchAPI();
            api.Settings.ClientId = ClientID;
        }

        public static async Task<DeviceCodeResponseIF> RequestDeviceCodeAsync()
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientID),
                new KeyValuePair<string, string>("scope", "user:edit:broadcast user:read:broadcast channel:manage:redemptions user:read:follows channel:manage:raids user:write:chat moderator:manage:chat_messages")
            });
            var response = await client.PostAsync("https://id.twitch.tv/oauth2/device", content);
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<DeviceCodeResponseIF>(json);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceCode"></param>
        /// <param name="interval"></param>
        /// <param name="expiresIn"></param>
        /// <returns></returns>
        public static async Task<AccessTokenResponseIF> PollDeviceTokenAsync(string deviceCode, int interval, int expiresIn)
        {
            using var client = new HttpClient();
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < expiresIn)
            {
                await Task.Delay(interval * 1000);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientID),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                });
                var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tokenElem))
                {
                    AccessTokenResponseIF elem = new()
                    {
                        accessToken = tokenElem.GetString() ?? "",
                        refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? "",
                        expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32()
                    };

                    return elem;
                }
                else if (doc.RootElement.TryGetProperty("error", out var errorElem))
                {
                    var error = errorElem.GetString();
                    if (error == "authorization_pending" || error == "slow_down")
                    {
                        // 認証待ち or ポーリング間隔を守る
                        continue; 
                    }
                    else
                    {
                        // その他のエラー
                        break; 
                    }
                }
            }
            return new() {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        public static async Task<AccessTokenResponseIF> RefreshAccessTokenAsync(string refreshToken)
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientID),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);
            if (!response.IsSuccessStatusCode) return new()
            {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("access_token", out var tokenElem))
            {
                return new AccessTokenResponseIF
                {
                    accessToken = tokenElem.GetString() ?? "",
                    refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? "",
                    expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32()
                };
            }
            return new()
            {
                accessToken = "",
                refreshToken = "",
                expiresIn = 0
            };
        }


        /// <summary>
        /// 配信者情報取得処理
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public static async Task<TwitchUserIF?> GetBroadcasterIdAsync(string userName)
        {
            TwitchUserIF result = null;
            try
            {
                var apiResponse = await api.Helix.Users.GetUsersAsync(logins: new List<string>() { userName });

                if (apiResponse?.Users != null)
                {
                    var responseData = apiResponse.Users.FirstOrDefault();

                    result = new TwitchUserIF()
                    {
                        UserId = responseData.Id,
                        Login = responseData.Login,
                        BroadcasterType = responseData.BroadcasterType, 
                        CreatedAt = responseData.CreatedAt,
                        DisplayName = responseData.DisplayName,
                        Description = responseData.Description,
                        OfflineImageUrl = responseData.OfflineImageUrl,
                        ProfileImageUrl = responseData.ProfileImageUrl,
                        UserType = responseData.Type, 
                    };
                }
            }
            catch (Exception ex)
            {
            }

            return result;
        }


        /// <summary>
        /// 配信者情報取得処理
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public static async Task<List<TwitchStreamIF>?> GetStreamingFollowUserAsync()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "フォロー中配信中チャンネル取得");

            List<TwitchStreamIF> results = [];
            try
            {
                var apiResponse = await api.Helix.Streams.GetFollowedStreamsAsync(BroadcasterId);

                if (apiResponse?.Data != null)
                {
                    var responseData = apiResponse.Data;

                    foreach (var data in responseData)
                    {
                        results.Add(new TwitchStreamIF()
                        {
                            UserId = data.Id,
                            Title = data.Title,
                            UserName = data.UserName,
                            UserLogin = data.UserLogin,
                            GameId = data.GameId,
                            StartedAt = data.StartedAt,
                            ThumbnailUrl = data.ThumbnailUrl
                        });
                    }
                }

                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return results;
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), appLogProcessName + "：" + ex.Message);
            }

            return results;
        }


        /// <summary>
        /// カテゴリの取得
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        public static async Task<TwitchCategoryIF> GetCategoryByGameId(string gameId)
        {
            TwitchCategoryIF result = null;
            try
            {
                var apiResponse = await api.Helix.Games.GetGamesAsync(gameIds: [gameId]);

                if (apiResponse?.Data != null)
                {
                    var responseData = apiResponse.Data.FirstOrDefault();

                    result = new TwitchCategoryIF()
                    {
                        Id = responseData.Id,
                        Name = responseData.Name,
                        BoxArtUrl = responseData.BoxArtUrl
                    };

                    result.BoxArtUrl = result.BoxArtUrl.Replace("{width}", "128").Replace("{height}", "192");
                }
            }
            catch (Exception ex)
            {
                
            }

            return result;
        }


        /// <summary>
        /// 配カテゴリの設定
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        public static async Task<bool> SetCategoryAsync(string _gameId)
        {

            var channelUpdateInfo = new TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation.ModifyChannelInformationRequest()
            {
                GameId = _gameId
            };

            try
            {
                var apiResponse = await api.Helix.Channels.ModifyChannelInformationAsync(
                                            broadcasterId: BroadcasterId,
                                            request: channelUpdateInfo);
            }
            catch (Exception ex)
            {
            }

            return true;
        }


        /// <summary>
        /// カテゴリ検索
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public static async Task<List<TwitchCategoryIF>>? SearchCategoriesByGameNameAsync(string categoryName)
        {
            List<TwitchCategoryIF> list = [];
            try
            {
                var apiResponse = await api.Helix.Search.SearchCategoriesAsync(categoryName);

                foreach (var responseItem in apiResponse.Games)
                {
                    list.Add(new TwitchCategoryIF
                    {
                        Id = responseItem.Id ?? "",
                        Name = responseItem.Name ?? "",
                        BoxArtUrl = responseItem.BoxArtUrl ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
            }

            return list;
        }


        /// <summary>
        /// タイトルの取得
        /// API：https://api.twitch.tv/helix/channels?broadcaster_id=
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static async Task<TwitchModifyChannelInformationIF> GetTwitchStreamInfo(string broadcasterId)
        {
            TwitchModifyChannelInformationIF result = null;
            try
            {
                var apiResponse = await api.Helix.Channels.GetChannelInformationAsync(broadcasterId);

                if (apiResponse?.Data != null)
                {
                    var responseData = apiResponse.Data.FirstOrDefault();

                    result = new TwitchModifyChannelInformationIF()
                    {
                        title = responseData.Title,
                        gameId = responseData.GameId,
                        broadcasterLanguage = responseData.BroadcasterLanguage,
                        delay = responseData.Delay
                    };
                }
            }
            catch (Exception ex)
            {
            }

            return result;
        }

        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬リストを取得する
        /// API: GET https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:read:redemptions
        /// </summary>
        /// <returns>TwitchLibのCustomReward型のリスト。失敗した場合はnull。</returns>
        public static async Task<List<CustomReward>?> GetCustomRewardsAsync()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイントリスト取得");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイントリスト取得中断" + "broadcaster_id不詳");
                return null;
            }
            api.Settings.AccessToken = TwitchHelper.api.Settings.AccessToken;
            try
            {
                var response = await api.Helix.ChannelPoints.GetCustomRewardAsync(
                    broadcasterId: TwitchHelper.BroadcasterId,
                    onlyManageableRewards: false
                );

                if (response?.Data != null)
                {
                    return response.Data.ToList();
                }
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイントリスト取得失敗" + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬を修正する
        /// API: PATCH https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:manage:redemptions
        /// </summary>
        /// <param name="CustomRewardId">修正対象のカスタムリワードID</param>
        /// <param name="updateCustomRewardRequest">修正内容</param>
        /// <returns>修正後のカスタム報酬リスト(修正したものだけ)。失敗した場合はnull。</returns>
        public static async Task<List<CustomReward>?> UpdateCustomRewardAsync(
            string CustomRewardId, 
            UpdateCustomRewardRequest updateCustomRewardRequest)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新中断:broadcaster_id 不詳");
                return null;
            }
            api.Settings.AccessToken = TwitchHelper.api.Settings.AccessToken;
            api.Settings.ClientId = TwitchHelper.ClientID; // ClientIdを明示的に再セット

            try
            {
                var response = await api.Helix.ChannelPoints.UpdateCustomRewardAsync(
                    broadcasterId: TwitchHelper.BroadcasterId,
                    rewardId: CustomRewardId,
                    request: updateCustomRewardRequest
                );

                if (response?.Data != null)
                {
                    mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新");
                    return response.Data.ToList();
                }
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新失敗:" + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬を新規作成する
        /// API: POST https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:manage:redemptions
        /// </summary>
        /// <param name="createCustomRewardRequest">作成内容</param>
        /// <returns>作成後のカスタム報酬リスト（作成したものだけ）。失敗した場合はnull。</returns>
        public static async Task<List<CustomReward>?> CreateCustomRewardAsync(CreateCustomRewardsRequest createCustomRewardRequest)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬作成");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "broadcaster_id 不詳");
                return null;
            }
            api.Settings.AccessToken = TwitchHelper.api.Settings.AccessToken;
            api.Settings.ClientId = TwitchHelper.ClientID; // ClientIdを明示的に再セット

            try
            {
                var response = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(
                    broadcasterId: TwitchHelper.BroadcasterId,
                    request: createCustomRewardRequest
                );

                if (response?.Data != null)
                {
                    mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                    return response.Data.ToList();
                }
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬作成失敗：" + ex.Message);
            }

            return null;
        }


        public static async Task<DateTime?> StreamRaid(string toRaidBroadcasterId)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "レイド実行");

            try
            {
                var apiResponse = await api.Helix.Raids.StartRaidAsync(BroadcasterId, toRaidBroadcasterId);

                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return apiResponse.Data.FirstOrDefault().CreatedAt;
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), appLogProcessName + "：" + ex.Message);
            }

            return null;
        }



        public static async Task<string?> SendChat(string chatContent)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "チャット送信");

            try
            {
                var sendChatMessageRequest = new SendChatMessageRequest
                {
                    BroadcasterId = BroadcasterId,
                    SenderId = BroadcasterId,
                    Message = chatContent
                };
                var apiResponse = await api.Helix.Chat.SendChatMessage(sendChatMessageRequest);

                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return apiResponse.Data.FirstOrDefault().MessageId;
            }
            catch (Exception ex)
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), appLogProcessName + "：" + ex.Message);
            }

            return null;
        }


        public static async Task<bool?> PinedChat(string chatId)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "ピン止め処理");


            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var response = await client.PutAsync($"https://api.twitch.tv/helix/chat/pins" +
                                                $"?broadcaster_id={BroadcasterId}" +
                                                $"&moderator_id={BroadcasterId}" +
                                                $"&message_id={chatId}", null);
            
            if (!response.IsSuccessStatusCode)
            {
                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return false;
            }

            return true;
        }
    }
}

