using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace World.Rendering
{
    // A single override blends intensity and length together using standard Volume interpolation.
    [Serializable]
    public sealed class BloomDirectionParameter : Vector2Parameter
    {
        public BloomDirectionParameter() : base(Vector2.one) { }

        public override Vector2 value
        {
            get => base.value;
            set => base.value = new Vector2(Mathf.Max(0f, value.x), Mathf.Clamp(value.y, 0.01f, 4f));
        }
    }
}
