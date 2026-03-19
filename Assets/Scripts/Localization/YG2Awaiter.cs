using System.Threading.Tasks;
using YG;

namespace Localization
{
    public static class YG2Awaiter
    {
        private static TaskCompletionSource<bool> _tcs;

        public static Task WaitForSDKDataAsync()
        {
            // Если SDK уже инициализирован — не ждём
            if (YG2.isSDKEnabled)
                return Task.CompletedTask;

            _tcs ??= new TaskCompletionSource<bool>();

            void Handler()
            {
                YG2.onGetSDKData -= Handler;
                _tcs.TrySetResult(true);
            }

            YG2.onGetSDKData += Handler;
            return _tcs.Task;
        }
    }
}