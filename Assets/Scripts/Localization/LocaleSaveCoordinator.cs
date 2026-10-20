using Saves;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

namespace Localization
{
    /// <summary>Persists only explicit locale changes made after startup selection is complete.</summary>
    public sealed class LocaleSaveCoordinator : IStartable, System.IDisposable
    {
        private readonly BootCompletion bootCompletion;
        private readonly LocalSaveStore saveStore;

        public LocaleSaveCoordinator(BootCompletion bootCompletion, LocalSaveStore saveStore)
        {
            this.bootCompletion = bootCompletion;
            this.saveStore = saveStore;
        }

        public async void Start()
        {
            await bootCompletion.WaitAsync();
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        public void Dispose()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            if (locale == null || string.IsNullOrWhiteSpace(locale.Identifier.Code))
            {
                return;
            }

            saveStore.Data.languageCode = locale.Identifier.Code;
            saveStore.Save();
        }
    }
}
