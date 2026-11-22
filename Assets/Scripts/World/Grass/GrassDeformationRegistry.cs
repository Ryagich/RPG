using System.Collections.Generic;

namespace World.Grass
{
    // Scene-owned; unrelated to combat, target selection, and persistence registries.
    public sealed class GrassDeformationRegistry
    {
        private readonly HashSet<GrassDeformationSource> sources = new();
        public IReadOnlyCollection<GrassDeformationSource> Sources => sources;
        public GrassDeformationSource Anchor { get; private set; }

        public void Register(GrassDeformationSource source)
        {
            if (source == null) return;
            sources.Add(source);
            if (source.IsAnchor) Anchor = source;
        }

        public void Unregister(GrassDeformationSource source)
        {
            sources.Remove(source);
            if (Anchor == source) Anchor = null;
        }
    }
}
