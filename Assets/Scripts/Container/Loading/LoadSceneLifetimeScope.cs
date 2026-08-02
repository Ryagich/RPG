using Container.Project;
using UI.UIElements;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Loading
{
    public sealed class LoadSceneLifetimeScope : LifetimeScope
    {
        [SerializeField] private LoadSceneUI loadSceneUi;

        protected override void Awake()
        {
            var projectScope = ProjectLifetimeScope.Instance;
            if (projectScope == null)
            {
                Debug.LogError("ProjectLifetimeScope not found.");
                return;
            }

            parentReference.Object = projectScope;
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (loadSceneUi == null)
            {
                Debug.LogError("LoadSceneUI is not assigned to LoadSceneLifetimeScope.", this);
                return;
            }

            builder.RegisterComponent(loadSceneUi);
        }
    }
}
