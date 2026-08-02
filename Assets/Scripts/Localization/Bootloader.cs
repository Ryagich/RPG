using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using YG;
using YG.Insides;

// using YG;
// using YG.Insides;

namespace Localization
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Bootloader : IStartable
    {
        private readonly BootCompletion bootCompletion;

        public Bootloader(BootCompletion bootCompletion)
        {
            this.bootCompletion = bootCompletion;
        }

        public async void Start()
        {
            await StartAsync();
        }
        
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            Debug.Log($"Bootloader starting: YG2Enabled={YG2.isSDKEnabled}");
            
            await YG2Awaiter.WaitForSDKDataAsync();
            
            Debug.Log("Waiting for SDK data");

            YG2.InitMetrica();
            YG2.GetAuth();
            YG2.GetLanguage();
            
            Debug.Log($"Configuring language: '{YG2.lang}'");
            YGInsides.LoadProgress();
            
            await LocalizationHelper.InvalidateAsync(YG2.lang);
            LocalizationAwaiter.SignalReady();
            YG2.GameReadyAPI();

            if (!YG2.saves.GameReadyMetricSend)
            {
                YG2.MetricaSend("GameReady");
                YG2.saves.GameReadyMetricSend = true;
            }
            
            bootCompletion.Signal();
        }
    }
}
