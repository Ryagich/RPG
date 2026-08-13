using CameraScripts;
using GameModes;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer.Unity;

namespace Movement
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerMovementController : IStartable
    {
        private readonly Transform transform;
        private readonly PlayerMovement playerMovement;
        private readonly CameraMotor cameraMotor;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        
        public PlayerMovementController
            (
                Transform transform,
                PlayerMovement playerMovement,
                CameraMotor cameraMotor,
                ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
                ISubscriber<PlayerMoveMessage> playerMoveSubscriber
            )
        {
            this.transform = transform;
            this.playerMovement = playerMovement;
            this.cameraMotor = cameraMotor;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;

            playerMoveSubscriber.Subscribe(OnMove);
        }

        public void Start()
        {
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
        }

        private void OnGameModeChanged(GameModeChangedMessage msg)
        {
            if (AllowsPlayerMovement(msg.GameMode))
            {
                playerMovement.ChangeState(true);
                cameraMotor.ChangeGameplayTarget(transform);
            }
            else
            {
                playerMovement.ChangeState(false);
            }
        }
        
        private void OnMove(PlayerMoveMessage msg)
        {
            playerMovement.SetMovementInput(msg.Direction, msg.IsRunning);
        }

        private static bool AllowsPlayerMovement(GameMode mode)
        {
            return mode is GameMode.Game or GameMode.Inventory;
        }
    }
}
