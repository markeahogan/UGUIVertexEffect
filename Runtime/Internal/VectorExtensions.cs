using System.Runtime.CompilerServices;
using UnityEngine;

namespace PopupAsylum.UIEffects.Extensions
{
    public static class VectorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 xy(this Vector3 v) => new Vector2(v.x, v.y);
    }
}
