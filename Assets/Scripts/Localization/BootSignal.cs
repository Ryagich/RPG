using Cysharp.Threading.Tasks;

namespace Localization
{
    public static class BootSignal
    {
        private static readonly UniTaskCompletionSource completion = new();

        public static UniTask WaitAsync() => completion.Task;

        public static void Signal()
        {
            if (!completion.Task.Status.IsCompleted())
                completion.TrySetResult();
        }
    }
}