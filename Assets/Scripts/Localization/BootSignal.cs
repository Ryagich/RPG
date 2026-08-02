using Cysharp.Threading.Tasks;

namespace Localization
{
    /// <summary>
    /// Project-lifetime signal that marks completion of the boot sequence.
    /// </summary>
    public sealed class BootCompletion
    {
        private readonly UniTaskCompletionSource completion = new();

        public UniTask WaitAsync() => completion.Task;

        public void Signal()
        {
            if (!completion.Task.Status.IsCompleted())
            {
                completion.TrySetResult();
            }
        }
    }
}
