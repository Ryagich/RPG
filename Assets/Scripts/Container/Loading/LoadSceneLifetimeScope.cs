using Container.Project;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Loading
{
    public sealed class LoadSceneLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            var projectScope = Find<ProjectLifetimeScope>();
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
        }
    }
}
