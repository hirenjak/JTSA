using JTSA.Forms;
using JTSA.Forms.TwitchIF;
using JTSA.Panels;
using JTSA.TwitchIF;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.Channels.SendChatMessage;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace JTSA.Utility
{
    static class TwitchHelper
    {
        public static readonly TwitchAPI api;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        static TwitchHelper()
        {
            api = new TwitchAPI();
            api.Settings.ClientId = ClientID;
        }


        /// <summary>
        /// アクセストークンの持ち主（＝このアプリを認証したユーザー本人）の情報を取得する。
        /// API: GET https://api.twitch.tv/helix/users （ids/loginsを指定しない場合はトークンの本人が返る）
        ///
        /// ユーザー名を手入力させずに配信者IDを特定するために使う。
        /// </summary>
        /// <returns>認証中ユーザーの情報。失敗した場合はnull。</returns>
        public static async Task<TwitchUserIF?> GetAuthenticatedUserAsync()
        {
            try
            {
                // ids/logins を指定しない場合、アクセストークンの持ち主が返る
                var apiResponse = await api.Helix.Users.GetUsersAsync();

                var responseData = apiResponse?.Users?.FirstOrDefault();
                if (responseData == null) return null;

                return new TwitchUserIF()
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
            catch (Exception ex)
            {
                MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "認証ユーザー情報の取得失敗：" + ex.Message);
            }

            return null;
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
                            UserId = data.UserId,
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


        #region ==================== 認証関連 ====================

        public static string ClientID { get; } = "tbpy1q9lh9pkyrqhde6o4f4dkq9rj0";

        public static string RedirectUri = @"http://localhost:8080/";
        public static string BroadcasterId = "";

        public static string AccessToken { get { return api.Settings.AccessToken; } set { api.Settings.AccessToken = value; } }

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
            return new()
            {
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

        #endregion


        #region ==================== カテゴリ取得関連 ====================

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

        #endregion


        #region ==================== チャンネルポイント関連 ====================

        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬リストを取得する
        /// API: GET https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:read:redemptions
        /// </summary>
        /// <param name="onlyManageableRewards">
        /// trueの場合、このアプリ（同一client_id）が作成した報酬のみを返す。
        /// falseの結果との差分がTwitchのWeb画面や他アプリで作成された「操作不可」な報酬になる。
        /// </param>
        /// <returns>TwitchLibのCustomReward型のリスト。失敗した場合はnull。</returns>
        public static async Task<List<CustomReward>?> GetCustomRewardsAsync(bool onlyManageableRewards = false)
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
                    onlyManageableRewards: onlyManageableRewards
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
        /// <returns>修正後のカスタム報酬リスト(修正したものだけ)。失敗理由付き。</returns>
        public static async Task<TwitchApiResult<List<CustomReward>>> UpdateCustomRewardAsync(
            string CustomRewardId,
            UpdateCustomRewardRequest updateCustomRewardRequest)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新中断:broadcaster_id 不詳");
                return TwitchApiResult<List<CustomReward>>.Failure(
                    TwitchApiErrorKind.NotConfigured, "broadcaster_id 不詳");
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
                    return TwitchApiResult<List<CustomReward>>.Success(response.Data.ToList());
                }

                return TwitchApiResult<List<CustomReward>>.Failure(
                    TwitchApiErrorKind.Unknown, "レスポンスが空でした");
            }
            catch (Exception ex)
            {
                var result = TwitchApiResult<List<CustomReward>>.FromException(ex);
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬更新失敗:" + result.ErrorMessage);
                return result;
            }
        }


        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬を削除する
        /// API: DELETE https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:manage:redemptions
        ///
        /// このアプリ（同一client_id）が作成した報酬しか削除できない点に注意。
        /// </summary>
        /// <param name="customRewardId">削除対象のカスタムリワードID</param>
        /// <returns>成否と失敗理由</returns>
        public static async Task<TwitchApiResult<bool>> DeleteCustomRewardAsync(string customRewardId)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬削除");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬削除中断:broadcaster_id 不詳");
                return TwitchApiResult<bool>.Failure(TwitchApiErrorKind.NotConfigured, "broadcaster_id 不詳");
            }
            api.Settings.AccessToken = TwitchHelper.api.Settings.AccessToken;
            api.Settings.ClientId = TwitchHelper.ClientID; // ClientIdを明示的に再セット

            try
            {
                await api.Helix.ChannelPoints.DeleteCustomRewardAsync(
                    broadcasterId: TwitchHelper.BroadcasterId,
                    rewardId: customRewardId
                );

                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return TwitchApiResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                var result = TwitchApiResult<bool>.FromException(ex);
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬削除失敗:" + result.ErrorMessage);
                return result;
            }
        }


        /// <summary>
        /// TwitchLibを使用してチャンネルポイントのカスタム報酬を新規作成する
        /// API: POST https://api.twitch.tv/helix/channel_points/custom_rewards
        /// Scope: channel:manage:redemptions
        /// </summary>
        /// <param name="createCustomRewardRequest">作成内容</param>
        /// <returns>作成後のカスタム報酬リスト（作成したものだけ）。失敗理由付き。</returns>
        public static async Task<TwitchApiResult<List<CustomReward>>> CreateCustomRewardAsync(CreateCustomRewardsRequest createCustomRewardRequest)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬作成");

            if (string.IsNullOrEmpty(TwitchHelper.BroadcasterId))
            {
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "broadcaster_id 不詳");
                return TwitchApiResult<List<CustomReward>>.Failure(
                    TwitchApiErrorKind.NotConfigured, "broadcaster_id 不詳");
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
                    return TwitchApiResult<List<CustomReward>>.Success(response.Data.ToList());
                }

                return TwitchApiResult<List<CustomReward>>.Failure(
                    TwitchApiErrorKind.Unknown, "レスポンスが空でした");
            }
            catch (Exception ex)
            {
                var result = TwitchApiResult<List<CustomReward>>.FromException(ex);
                mainWindow.AppLogPanel.Error(nameof(TwitchHelper), "TwitchLibでチャンネルポイント報酬作成失敗：" + result.ErrorMessage);
                return result;
            }
        }

        #endregion


        #region ==================== チャット関連 ====================

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


        public static async Task<bool?> PinedDeleteChat()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "ピン止め処理");


            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var response = await client.DeleteAsync($"https://api.twitch.tv/helix/chat/pins" +
                                                $"?broadcaster_id={BroadcasterId}" +
                                                $"&moderator_id={BroadcasterId}");

            if (!response.IsSuccessStatusCode)
            {
                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return false;
            }

            return true;
        }

        public static async Task<TwitchChatForm?> GetPinedChat()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            var appLogProcessName = mainWindow.AppLogPanel.ProcessStart(nameof(TwitchHelper), "ピン止め処理");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Add("Client-Id", ClientID);

            var response = await client.GetAsync($"https://api.twitch.tv/helix/chat/pins" +
                                                $"?broadcaster_id={BroadcasterId}" +
                                                $"&moderator_id={BroadcasterId}");

            if (!response.IsSuccessStatusCode)
            {
                mainWindow.AppLogPanel.ProcessEnd(nameof(TwitchHelper), appLogProcessName);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TwitchChatForm>();

            return result;
        }

        #endregion

        public static List<TwitchChatPart> CreateParts(ChatMessage chatMessage)
        {
            var result = new List<TwitchChatPart>();
            var message = chatMessage.Message;

            var emotes = chatMessage.EmoteSet.Emotes
                .OrderBy(x => x.StartIndex)
                .ToList();

            int currentIndex = 0;

            foreach (var emote in emotes)
            {
                // スタンプより前の通常文字
                if (emote.StartIndex > currentIndex)
                {
                    result.Add(new TwitchChatPart
                    {
                        Text = message.Substring(
                            currentIndex,
                            emote.StartIndex - currentIndex)
                    });
                }

                result.Add(new TwitchChatPart
                {
                    Text = emote.Name,

                    // 1.0 / 2.0 / 3.0でサイズ変更可能
                    ImageUrl =
                        $"https://static-cdn.jtvnw.net/emoticons/v2/" +
                        $"{emote.Id}/default/dark/2.0"
                });

                currentIndex = emote.EndIndex + 1;
            }

            // 最後に残った通常文字
            if (currentIndex < message.Length)
            {
                result.Add(new TwitchChatPart
                {
                    Text = message.Substring(currentIndex)
                });
            }

            // スタンプがない場合
            if (result.Count == 0)
            {
                result.Add(new TwitchChatPart
                {
                    Text = message
                });
            }

            return result;
        }


        public static List<TwitchChatPart> CreateParts(string chatMessage)
        {
            var result = new List<TwitchChatPart>();
            var message = chatMessage;

            // スタンプがない場合
            if (result.Count == 0)
            {
                result.Add(new TwitchChatPart
                {
                    Text = message
                });
            }

            return result;
        }
    }
}

