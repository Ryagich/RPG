using System;
using MessagePipe;
using Messages;
using UniRx;

namespace Inventory
{
    internal sealed class PlayerWeaponInputSubscriptions : IDisposable
    {
        private readonly CompositeDisposable disposables = new();

        public PlayerWeaponInputSubscriptions(
            ISubscriber<WeaponSlotInputMessage> weaponSlotInputSubscriber,
            ISubscriber<MouseDown> mouseDownSubscriber,
            ISubscriber<DodgeInputMessage> dodgeInputSubscriber,
            ISubscriber<RollInputMessage> rollInputSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
            Action<WeaponSlotInputMessage> onWeaponSlotInput,
            Action<MouseDown> onMouseDown,
            Action<DodgeInputMessage> onDodgeInput,
            Action<RollInputMessage> onRollInput,
            Action<GameModeChangedMessage> onGameModeChanged)
        {
            weaponSlotInputSubscriber.Subscribe(onWeaponSlotInput).AddTo(disposables);
            mouseDownSubscriber.Subscribe(onMouseDown).AddTo(disposables);
            dodgeInputSubscriber.Subscribe(onDodgeInput).AddTo(disposables);
            rollInputSubscriber.Subscribe(onRollInput).AddTo(disposables);
            gameModeChangedSubscriber.Subscribe(onGameModeChanged).AddTo(disposables);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
