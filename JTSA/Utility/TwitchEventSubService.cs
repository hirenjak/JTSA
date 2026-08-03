using JTSA.Forms;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Extensions;

namespace JTSA.Utility
{
    public sealed class TwitchEventSubService : IAsyncDisposable
    {
        private readonly ServiceProvider serviceProvider;
        private readonly EventSubWebsocketClient eventSubClient;
        private readonly TwitchAPI twitchApi;

        private readonly string broadcasterUserId;

        private bool isSubscribed;
        private bool isDisposed;

        public event Action<ChannelPointForm>? ChannelPointRedeemed;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="userAccessToken"></param>
        /// <param name="broadcasterUserId"></param>
        public TwitchEventSubService(TwitchAPI api, string broadcasterId)
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddTwitchLibEventSubWebsockets();

            serviceProvider = services.BuildServiceProvider();

            eventSubClient = serviceProvider.GetRequiredService<EventSubWebsocketClient>();

            twitchApi = api;
            broadcasterUserId = broadcasterId;

            RegisterEvents();
        }


        /// <summary>
        /// 
        /// </summary>
        private void RegisterEvents()
        {
            eventSubClient.WebsocketConnected += OnWebsocketConnected;
            eventSubClient.WebsocketDisconnected += OnWebsocketDisconnected;
            eventSubClient.WebsocketReconnected += OnWebsocketReconnected;
            eventSubClient.ErrorOccurred += OnErrorOccurred;

            eventSubClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemptionAdd;
        }

        public async Task ConnectAsync()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(TwitchEventSubService));
            }

            await eventSubClient.ConnectAsync();
        }

        public async Task DisconnectAsync()
        {
            if (isDisposed)
                return;

            await eventSubClient.DisconnectAsync();
        }


        /// <summary>
        /// EventSub接続完了
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
        {
            /*
             * Twitch側からの再接続要求の場合は、
             * 既存の購読が引き継がれるため再購読しない。
             */
            if (e.IsRequestedReconnect)
                return;

            isSubscribed = false;

            await SubscribeChannelPointsAsync();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task SubscribeChannelPointsAsync()
        {
            if (isSubscribed)
                return;

            if (string.IsNullOrWhiteSpace(broadcasterUserId))
            {
                Debug.WriteLine(
                    "EventSub購読失敗: BroadcasterIdが空です。");

                return;
            }

            if (string.IsNullOrWhiteSpace(eventSubClient.SessionId))
            {
                Debug.WriteLine(
                    "EventSub購読失敗: SessionIdが空です。");

                return;
            }

            if (string.IsNullOrWhiteSpace(twitchApi.Settings.AccessToken))
            {
                Debug.WriteLine(
                    "EventSub購読失敗: AccessTokenが空です。");

                return;
            }

            var condition = new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterUserId
            };

            try
            {
                Debug.WriteLine(
                    $"EventSub購読開始 " +
                    $"BroadcasterId={broadcasterUserId}, " +
                    $"SessionId={eventSubClient.SessionId}");

                var result =
                    await twitchApi.Helix.EventSub
                        .CreateEventSubSubscriptionAsync(
                            type:
                                "channel.channel_points_custom_reward_redemption.add",
                            version: "1",
                            condition: condition,
                            method: EventSubTransportMethod.Websocket,
                            websocketSessionId:
                                eventSubClient.SessionId,
                            accessToken:
                                twitchApi.Settings.AccessToken);

                foreach (var subscription in result.Subscriptions)
                {
                    Debug.WriteLine(
                        $"EventSub購読結果: " +
                        $"Id={subscription.Id}, " +
                        $"Type={subscription.Type}, " +
                        $"Status={subscription.Status}");
                }

                isSubscribed =
                    result.Subscriptions.Count() > 0;
            }
            catch (Exception ex)
            {
                isSubscribed = false;

                Debug.WriteLine(
                    $"EventSub購読失敗: {ex}");
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private Task OnChannelPointsCustomRewardRedemptionAdd(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            var redemption = e.Payload.Event;

            var form = new ChannelPointForm
            {
                RedemptionId = redemption.Id,

                BroadcasterUserId = redemption.BroadcasterUserId,

                UserId = redemption.UserId,
                UserLogin = redemption.UserLogin,
                UserName = redemption.UserName,

                RewardId = redemption.Reward.Id,
                RewardTitle = redemption.Reward.Title,
                RewardCost = redemption.Reward.Cost,
                RewardPrompt = redemption.Reward.Prompt,

                UserInput = redemption.UserInput,
                Status = redemption.Status
            };

            ChannelPointRedeemed?.Invoke(form);

            return Task.CompletedTask;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnWebsocketDisconnected(object? sender, WebsocketDisconnectedArgs e)
        {
            Debug.WriteLine("EventSubが切断されました。");

            isSubscribed = false;

            /*
             * Twitch側から指定された再接続ではなく、
             * 通信断などによる切断時に再接続する。
             */
            while (!isDisposed)
            {
                try
                {
                    bool connected =
                        await eventSubClient.ReconnectAsync();

                    if (connected)
                    {
                        Debug.WriteLine(
                            "EventSub再接続完了");

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"EventSub再接続エラー: {ex.Message}");
                }

                await Task.Delay(3000);
            }
        }

        private Task OnWebsocketReconnected(object? sender, WebsocketReconnectedArgs e)
        {
            Debug.WriteLine(
                $"EventSub再接続完了: " +
                $"{eventSubClient.SessionId}");

            return Task.CompletedTask;
        }

        private Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
        {
            Debug.WriteLine(
                $"EventSubエラー: {e.Exception}");

            return Task.CompletedTask;
        }


        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            eventSubClient.WebsocketConnected -=
                OnWebsocketConnected;

            eventSubClient.WebsocketDisconnected -=
                OnWebsocketDisconnected;

            eventSubClient.WebsocketReconnected -=
                OnWebsocketReconnected;

            eventSubClient.ErrorOccurred -=
                OnErrorOccurred;

            eventSubClient
                    .ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemptionAdd;

            try
            {
                await eventSubClient.DisconnectAsync();
            }
            catch
            {
                // 終了処理中の切断エラーは無視
            }

            await serviceProvider.DisposeAsync();
        }
    }
}