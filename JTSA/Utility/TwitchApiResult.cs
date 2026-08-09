namespace JTSA.Utility
{
    /// <summary>
    /// Twitch API 呼び出しの失敗理由の分類
    /// </summary>
    public enum TwitchApiErrorKind
    {
        /// <summary> 分類できない失敗 </summary>
        Unknown = 0,

        /// <summary> API を叩く前の問題（broadcaster_id 未設定など） </summary>
        NotConfigured,

        /// <summary> 403：このアプリが作成した報酬ではないため操作できない </summary>
        NotManageable,

        /// <summary> 400：同名の報酬が既に存在する </summary>
        DuplicateTitle,

        /// <summary> 400：その他のリクエスト不正 </summary>
        InvalidRequest,

        /// <summary> 401：スコープ不足・トークン期限切れ </summary>
        Unauthorized,

        /// <summary> 429：レート制限 </summary>
        RateLimited,
    }


    /// <summary>
    /// Twitch API 呼び出しの結果。
    ///
    /// 既存の TwitchHelper は例外を握り潰して null を返すため、
    /// 呼び出し側が「なぜ失敗したか」を画面に出せない。
    /// 報酬のコピー処理はタイトル重複を検知して接尾辞を変えて再試行する必要があるため、
    /// チャンネルポイント関連ではこの型で失敗理由を返す。
    /// </summary>
    /// <typeparam name="T">成功時のデータ型</typeparam>
    public class TwitchApiResult<T>
    {
        /// <summary> 成功したか </summary>
        public bool IsSuccess { get; private init; }

        /// <summary> 成功時のデータ </summary>
        public T? Data { get; private init; }

        /// <summary> 失敗理由の分類 </summary>
        public TwitchApiErrorKind ErrorKind { get; private init; }

        /// <summary> 画面・ログに出すための失敗理由 </summary>
        public string ErrorMessage { get; private init; } = "";


        public static TwitchApiResult<T> Success(T data)
            => new() { IsSuccess = true, Data = data };


        public static TwitchApiResult<T> Failure(TwitchApiErrorKind kind, string message)
            => new() { IsSuccess = false, ErrorKind = kind, ErrorMessage = message };


        /// <summary>
        /// TwitchLib が投げた例外を失敗理由へ分類する。
        /// TwitchLib は HTTP ステータスごとに専用の例外型を投げ、
        /// メッセージにレスポンスボディ（Twitch のエラーコード文字列）を含めてくる。
        /// </summary>
        /// <param name="ex">TwitchLib が投げた例外</param>
        /// <returns>分類済みの失敗結果</returns>
        public static TwitchApiResult<T> FromException(Exception ex)
        {
            var message = ex.Message ?? "";

            // 例外の型名で判定する（TwitchLib.Api.Core.Exceptions 配下）
            var kind = ex.GetType().Name switch
            {
                "BadScopeException" => TwitchApiErrorKind.Unauthorized,
                "BadTokenException" => TwitchApiErrorKind.Unauthorized,
                "TokenExpiredException" => TwitchApiErrorKind.Unauthorized,
                "InvalidCredentialException" => TwitchApiErrorKind.Unauthorized,
                "TooManyRequestsException" => TwitchApiErrorKind.RateLimited,
                "BadRequestException" => ClassifyBadRequest(message),
                "BadParameterException" => ClassifyBadRequest(message),
                _ => ClassifyByMessage(message),
            };

            return Failure(kind, ToUserMessage(kind, message));
        }


        /// <summary>
        /// 400 系のうち、タイトル重複だけは呼び出し側がリトライできるよう区別する
        /// </summary>
        private static TwitchApiErrorKind ClassifyBadRequest(string message)
        {
            return IsDuplicateTitle(message)
                ? TwitchApiErrorKind.DuplicateTitle
                : TwitchApiErrorKind.InvalidRequest;
        }


        /// <summary>
        /// 専用の例外型に該当しない場合、メッセージ本文から推測する
        /// </summary>
        private static TwitchApiErrorKind ClassifyByMessage(string message)
        {
            if (IsDuplicateTitle(message)) return TwitchApiErrorKind.DuplicateTitle;

            if (message.Contains("403") || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                return TwitchApiErrorKind.NotManageable;

            if (message.Contains("401") || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                return TwitchApiErrorKind.Unauthorized;

            if (message.Contains("429")) return TwitchApiErrorKind.RateLimited;

            return TwitchApiErrorKind.Unknown;
        }


        /// <summary>
        /// Twitch はタイトル重複時に CREATE_CUSTOM_REWARD_DUPLICATE_REWARD を返す
        /// </summary>
        private static bool IsDuplicateTitle(string message)
        {
            return message.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// 分類結果を日本語の説明文にする（元のメッセージも診断用に添える）
        /// </summary>
        private static string ToUserMessage(TwitchApiErrorKind kind, string rawMessage)
        {
            var summary = kind switch
            {
                TwitchApiErrorKind.NotManageable
                    => "この報酬は Twitch の Web 画面（または他アプリ）から作成されたため、このアプリからは操作できません。",
                TwitchApiErrorKind.DuplicateTitle
                    => "同じ名前の報酬が既に存在します。",
                TwitchApiErrorKind.InvalidRequest
                    => "リクエストの内容が不正です。",
                TwitchApiErrorKind.Unauthorized
                    => "認証が切れているか、権限（スコープ）が不足しています。再認証してください。",
                TwitchApiErrorKind.RateLimited
                    => "Twitch の API 制限に達しました。しばらく待ってから再試行してください。",
                TwitchApiErrorKind.NotConfigured
                    => "配信者情報が未取得のため実行できません。",
                _ => "不明なエラーが発生しました。",
            };

            return string.IsNullOrWhiteSpace(rawMessage) ? summary : $"{summary}（{rawMessage}）";
        }
    }
}
