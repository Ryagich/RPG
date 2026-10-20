using System.Threading;
using Cysharp.Threading.Tasks;
using Saves;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

namespace Localization
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Bootloader : IStartable
    {
        private readonly BootCompletion bootCompletion;
        private readonly LocalSaveStore saveStore;

        public Bootloader(BootCompletion bootCompletion, LocalSaveStore saveStore)
        {
            this.bootCompletion = bootCompletion;
            this.saveStore = saveStore;
        }

        public async void Start()
        {
            await StartAsync();
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            saveStore.Load();
            await LocalizationSettings.InitializationOperation;

            string languageCode = saveStore.Data.languageCode;
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                Locale systemLocale = new SystemLocaleSelector().GetStartupLocale(LocalizationSettings.AvailableLocales);
                if (systemLocale != null)
                {
                    LocalizationSettings.SelectedLocale = systemLocale;
                    languageCode = systemLocale.Identifier.Code;
                    saveStore.Data.languageCode = languageCode;
                    saveStore.Save();
                }
            }

            await LocalizationHelper.InvalidateAsync(languageCode);
            LocalizationAwaiter.SignalReady();
            bootCompletion.Signal();
        }
    }
}
