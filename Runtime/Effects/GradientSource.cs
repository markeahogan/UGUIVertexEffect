using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


namespace PopupAsylum.UIEffects
{
    public class GradientSource : MonoBehaviour
    {
        private static Vector3[] _localCorners = new Vector3[4];

        public Gradient gradient = new Gradient();
        public float angle = 180;

        public event Action OnChanged;

        private void OnValidate()
        {
            OnChanged?.Invoke();
        }

        internal void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            GetRange(out var direction, out var min, out var range);
            vertex = GradientVertex(graphicTransform, vertex, direction, min, range);
        }

        private UIVertex GradientVertex(RectTransform graphicTransform, UIVertex vertex, Vector3 direction, float min, float range)
        {
            UIEffect.ConvertSpace(ref vertex, graphicTransform, transform);

            float t = (Vector3.Dot(direction, vertex.position) - min) / range;
            var color = gradient.Evaluate(Mathf.Clamp01(t));
            vertex.color *= color;

            UIEffect.ConvertSpace(ref vertex, transform, graphicTransform);
            return vertex;
        }

        internal void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            GetRange(out var direction, out var min, out var range);

            var colorKeys = gradient.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                var plane = new Plane(direction, (colorKeys[i].time * range) + min - 0.01f);
                UIEffect.ConvertSpace(ref plane, transform, graphicTransform);
                list.Add(plane);
                if (gradient.mode == GradientMode.Fixed)
                {
                    var plane2 = new Plane(direction, (colorKeys[i].time * range) + min + 0.01f);
                    UIEffect.ConvertSpace(ref plane2, transform, graphicTransform);
                    list.Add(plane2);
                }
            }

            var alphaKeys = gradient.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                var plane = new Plane(direction, (alphaKeys[i].time * range) + min - 0.01f);
                UIEffect.ConvertSpace(ref plane, transform, graphicTransform);
                list.Add(plane);
                if (gradient.mode == GradientMode.Fixed)
                {
                    var plane2 = new Plane(direction, (alphaKeys[i].time * range) + min + 0.01f);
                    UIEffect.ConvertSpace(ref plane2, transform, graphicTransform);
                    list.Add(plane2);
                }
            }
        }

        private void GetRange(out Vector3 direction, out float min, out float range)
        {
            direction = Quaternion.AngleAxis(angle, Vector3.back) * Vector3.up;
            var rect = transform as RectTransform;
            rect.GetLocalCorners(_localCorners);
            min = Vector3.Dot(_localCorners[0], direction);
            float max = min;
            for (int i = 1; i < 4; i++)
            {
                var dot = Vector3.Dot(_localCorners[i], direction);
                min = Mathf.Min(min, dot);
                max = Mathf.Max(max, dot);
            }
            range = max - min;
        }

        internal void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            GetRange(out var direction, out var min, out var range);
            var count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = GradientVertex(graphicTransform, verts[i], direction, min, range);
            }
        }
    }
}