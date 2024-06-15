using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Distorts UI using its corners patch
    /// </summary>
    public class DistortUIEffect : UIEffect
    {
        public override Space UIVertexSpace => Space.Local;
        public override bool AffectsPosition => true;

        [SerializeField]
        DistortionPatch _distortionPatch;

        [SerializeField, Tooltip("The number of divisions needed for the effect, less is faster but more angular")]
        Vector2Int _divisions = new Vector2Int(5, 5);

        /// <summary>
        /// Used to test that the bezier patch has really changed, to avoid marking as dirty unnecessarily
        /// Animation is supported via OnDidApplyAnimationProperties which can be called when the bezier patch didnt change
        /// </summary>
        DistortionPatch _lastDistortionPatch;

        public DistortionPatch DistortionPatch
        {
            get
            {
                return _distortionPatch;
            }
            set
            {
                _distortionPatch = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Called when an Animation/Animator modifies this component
        /// </summary>
        private void OnDidApplyAnimationProperties()
        {
            SetDirty();
        }

        private void Reset()
        {
            _distortionPatch = DistortionPatch.Identity;
            SetDirty();
        }

        public void SetDirty()
        {
            if (_lastDistortionPatch.Equals(_distortionPatch)) return;

            _lastDistortionPatch = _distortionPatch;
            MarkAsDirty();
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            SizeAndOrigin(out var size, out var origin);
            vertex = Warp(size, origin, vertex);
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            SizeAndOrigin(out var size, out var origin);
            var count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = Warp(size, origin, verts[i]);
            }
        }

        private UIVertex Warp(Vector2 size, Vector2 origin, UIVertex vertex)
        {
            // normalize the vertex position
            Vector2 xy = vertex.position;
            xy = (xy - origin) / size;
            xy.y = 1 - xy.y;

            // calculate the bezier
            Vector3 bezierPoint = _distortionPatch.GetPoint(xy);

            // unnormalize that back to the rect's dimensions
            var pos = Vector3.Scale(bezierPoint, new Vector3(size.x, size.y, 1));
            pos += (Vector3)origin;

            // apply to the vertex
            vertex.position = new Vector3(pos.x, pos.y, pos.z + vertex.position.z);
            return vertex;
        }

        /// <summary>
        /// Adds divisions in a simple grid layout
        /// </summary>
        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            base.AddDivisions(graphicTransform, list);
            GridSubdivide(graphicTransform, list, _divisions);
        }
    }

    /// <summary>
	/// Represents control points for a 4x4 Bezier surface
	/// </summary>
	[System.Serializable]
    public struct DistortionPatch
    {
        [SerializeField]
        public Vector3 topLeft, topRight, bottomLeft, bottomRight;

        /// <summary>
        /// An unwarped bezier patch
        /// </summary>
        public static DistortionPatch Identity => new DistortionPatch()
        {
            topLeft = new Vector3(0, 1, 0),
            topRight = new Vector3(1, 1, 0),
            bottomLeft = new Vector3(0, 0, 0),
            bottomRight = new Vector3(1, 0, 0)
        };

        public override bool Equals(object obj)
        {
            return obj is DistortionPatch x &&
                   topLeft.Equals(x.topLeft) &&
                   topRight.Equals(x.topRight) &&
                   bottomLeft.Equals(x.bottomLeft) &&
                   bottomRight.Equals(x.bottomRight);
        }

        public override int GetHashCode()
        {
            int hashCode = 1860550904;
            hashCode = hashCode * -1521134295 + topLeft.GetHashCode();
            hashCode = hashCode * -1521134295 + topRight.GetHashCode();
            hashCode = hashCode * -1521134295 + bottomLeft.GetHashCode();
            hashCode = hashCode * -1521134295 + bottomRight.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// Given normalized xy coords returns the position of point using bezier curves
        /// </summary>
        public Vector3 GetPoint(Vector2 xy)
        {
            var p0 = GetPoint(xy.x, topLeft, topRight);
            var p1 = GetPoint(xy.x, bottomLeft, bottomRight);
            return GetPoint(xy.y, p0, p1);
        }

        private Vector3 GetPoint(float t, Vector3 p0, Vector3 p1)
        {
            return Vector3.Lerp(p0, p1, t);
        }
    }

#if UNITY_EDITOR
	/// <summary>
	/// Draws handles for each of the bezier control points in the scene view
	/// </summary>
	[CustomEditor(typeof(DistortUIEffect)), CanEditMultipleObjects]
	class DistortPatchEditor : Editor
    {
		protected virtual void OnSceneGUI()
		{
			DistortUIEffect effect = (DistortUIEffect)target;
			if (!effect.isActiveAndEnabled) return;

			EditorGUI.BeginChangeCheck();
			DistortionPatch newBezier = Draw(effect.DistortionPatch);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(effect, "DistortionPatch");
				effect.DistortionPatch = newBezier;
				EditorUtility.SetDirty(effect);
			}
		}

        /// <summary>
        /// Draws the handles for the patch, with interconnecting lines
        /// </summary>
        DistortionPatch Draw(DistortionPatch bezierPatch)
		{
			Handles.color = Color.blue;
            DistortUIEffect example = (DistortUIEffect)target;
			var rectTransform = example.transform as RectTransform;

			var size = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
			var origin = -rectTransform.pivot * size;

			bezierPatch.topLeft = DrawHandle(rectTransform, size, origin, bezierPatch.topLeft);
			bezierPatch.topRight = DrawHandle(rectTransform, size, origin, bezierPatch.topRight);
			bezierPatch.bottomLeft = DrawHandle(rectTransform, size, origin, bezierPatch.bottomLeft);
			bezierPatch.bottomRight = DrawHandle(rectTransform, size, origin, bezierPatch.bottomRight);

			DrawLine(rectTransform, size, origin, bezierPatch.topLeft, bezierPatch.topRight, bezierPatch.bottomLeft, bezierPatch.bottomRight);

			return bezierPatch;
		}

		/// <summary>
		/// Draws a single control handle
		/// </summary>
		private static Vector3 DrawHandle(RectTransform rectTransform, Vector2 size, Vector2 origin, Vector3 point)
        {
			float scale = 0.66f;
            Vector3 worldSpace = ToWorldSpace(rectTransform, size, origin, point);
            Handles.matrix = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.identity;
            worldSpace = Handles.PositionHandle(worldSpace / scale, rectTransform.rotation) * scale;
			Handles.matrix = Matrix4x4.identity;
			Vector3 scaledPoint = rectTransform.InverseTransformPoint(worldSpace);
            point = new Vector3((scaledPoint.x - origin.x) / size.x, (scaledPoint.y - origin.y) / size.y, scaledPoint.z);
            return point;
        }

		/// <summary>
		/// Draws 3 lines between 4 points
		/// </summary>
		private static void DrawLine(RectTransform rectTransform, Vector2 size, Vector2 origin, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
			Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p0), ToWorldSpace(rectTransform, size, origin, p1));
			Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p1), ToWorldSpace(rectTransform, size, origin, p3));
			Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p3), ToWorldSpace(rectTransform, size, origin, p2));
            Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p2), ToWorldSpace(rectTransform, size, origin, p0));
        }

        private static Vector3 ToWorldSpace(RectTransform rectTransform, Vector2 size, Vector2 origin, Vector3 point)
        {
            var scaledPoint = new Vector3(point.x * size.x + origin.x, point.y * size.y + origin.y, point.z); ;
            var worldSpace = rectTransform.TransformPoint(scaledPoint);
            return worldSpace;
        }
    }
#endif
}
