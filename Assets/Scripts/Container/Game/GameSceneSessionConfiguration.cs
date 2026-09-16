namespace Container.Game
{
    /// <summary>
    /// Immutable scene-level policy supplied by the scene bootstrapper before the game scope is built.
    /// </summary>
    public sealed class GameSceneSessionConfiguration
    {
        public GameSceneSessionConfiguration(bool isGameplayScene)
        {
            IsGameplayScene = isGameplayScene;
        }

        public bool IsGameplayScene { get; }
    }
}
