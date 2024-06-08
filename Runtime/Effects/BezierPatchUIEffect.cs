using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PopupAsylum.UIEffects
{
	/// <summary>
	/// Warps UI using a bezier patch
	/// </summary>
    public class BezierPatchUIEffect : UIEffect
	{
		[SerializeField]
		BezierPatch4x4 _bezierPatch;

		[SerializeField, Tooltip("The number of divisions needed for the effect, less is faster but more angular")]
		Vector2Int _divisions = new Vector2Int(10, 10);

        /// <summary>
        /// Used to test that the bezier patch has really changed, to avoid marking as dirty unnecessarily
        /// Animation is supported via OnDidApplyAnimationProperties which can be called when the bezier patch didnt change
        /// </summary>
        BezierPatch4x4 _lastBezierPatch;

        public BezierPatch4x4 BezierPatch4X4
		{
			get
			{
				return _bezierPatch;
			}
			set
			{
				_bezierPatch = value;
				SetDirty();
			}
		}

		public override Space UIVertexSpace => Space.Local;
		public override bool AffectsPosition => true;

		private void Reset()
		{
			_bezierPatch = BezierPatch4x4.Identity;
			SetDirty();
		}

		public void SetDirty()
        {
			if (_lastBezierPatch.Equals(_bezierPatch)) return;

			_lastBezierPatch = _bezierPatch;
			MarkAsDirty();
        }

		/// <summary>
		/// Adds divisions in a simple grid layout
		/// </summary>
        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            base.AddDivisions(graphicTransform, list);
			GridSubdivide(graphicTransform, list, _divisions);
		}

		public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            SizeAndOrigin(out var size, out var origin);
            vertex = Warp(size, origin, vertex);
        }

        private UIVertex Warp(Vector2 size, Vector2 origin, UIVertex vertex)
        {
            // normalize the vertex position
            Vector2 xy = vertex.position;
            xy = (xy - origin) / size;
            xy.y = 1 - xy.y;

            // calculate the bezier
            Vector3 bezierPoint = _bezierPatch.GetPoint(xy);

            // unnormalize that back to the rect's dimensions
            var pos = Vector3.Scale(bezierPoint, new Vector3(size.x, size.y, 1));
            pos += (Vector3)origin;

            // apply to the vertex
            vertex.position = new Vector3(pos.x, pos.y, pos.z + vertex.position.z);
            return vertex;
        }

        /// <summary>
        /// Called when an Animation/Animator modifies this component
        /// </summary>
        private void OnDidApplyAnimationProperties()
		{
			SetDirty();
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
    }

	/// <summary>
	/// Represents control points for a 4x4 Bezier surface
	/// </summary>
	[System.Serializable]
	public struct BezierPatch4x4
    {
		const float A = 1 / 3f;
		const float B = 2 / 3f;

		[SerializeField]
		public Vector3 m00, m01, m02, m03,
			m10, m11, m12, m13,
			m20, m21, m22, m23,
			m30, m31, m32, m33;

		/// <summary>
		/// An unwarped bezier patch
		/// </summary>
		public static BezierPatch4x4 Identity => new BezierPatch4x4()
		{
			m00 = new Vector3(0, 1, 0),
			m01 = new Vector3(A, 1, 0),
			m02 = new Vector3(B, 1, 0),
			m03 = new Vector3(1, 1, 0),
			m10 = new Vector3(0, B, 0),
			m11 = new Vector3(A, B, 0),
			m12 = new Vector3(B, B, 0),
			m13 = new Vector3(1, B, 0),
			m20 = new Vector3(0, A, 0),
			m21 = new Vector3(A, A, 0),
			m22 = new Vector3(B, A, 0),
			m23 = new Vector3(1, A, 0),
			m30 = new Vector3(0, 0, 0),
			m31 = new Vector3(A, 0, 0),
			m32 = new Vector3(B, 0, 0),
			m33 = new Vector3(1, 0, 0)
		};

        public override bool Equals(object obj)
        {
            return obj is BezierPatch4x4 x &&
                   m00.Equals(x.m00) &&
                   m01.Equals(x.m01) &&
                   m02.Equals(x.m02) &&
                   m03.Equals(x.m03) &&
                   m10.Equals(x.m10) &&
                   m11.Equals(x.m11) &&
                   m12.Equals(x.m12) &&
                   m13.Equals(x.m13) &&
                   m20.Equals(x.m20) &&
                   m21.Equals(x.m21) &&
                   m22.Equals(x.m22) &&
                   m23.Equals(x.m23) &&
                   m30.Equals(x.m30) &&
                   m31.Equals(x.m31) &&
                   m32.Equals(x.m32) &&
                   m33.Equals(x.m33);
        }

        public override int GetHashCode()
        {
            int hashCode = 1860550904;
            hashCode = hashCode * -1521134295 + m00.GetHashCode();
            hashCode = hashCode * -1521134295 + m01.GetHashCode();
            hashCode = hashCode * -1521134295 + m02.GetHashCode();
            hashCode = hashCode * -1521134295 + m03.GetHashCode();
            hashCode = hashCode * -1521134295 + m10.GetHashCode();
            hashCode = hashCode * -1521134295 + m11.GetHashCode();
            hashCode = hashCode * -1521134295 + m12.GetHashCode();
            hashCode = hashCode * -1521134295 + m13.GetHashCode();
            hashCode = hashCode * -1521134295 + m20.GetHashCode();
            hashCode = hashCode * -1521134295 + m21.GetHashCode();
            hashCode = hashCode * -1521134295 + m22.GetHashCode();
            hashCode = hashCode * -1521134295 + m23.GetHashCode();
            hashCode = hashCode * -1521134295 + m30.GetHashCode();
            hashCode = hashCode * -1521134295 + m31.GetHashCode();
            hashCode = hashCode * -1521134295 + m32.GetHashCode();
            hashCode = hashCode * -1521134295 + m33.GetHashCode();
            return hashCode;
        }

		/// <summary>
		/// Given normalized xy coords returns the position of point using bezier curves
		/// </summary>
        public Vector3 GetPoint(Vector2 xy)
		{
			var p0 = GetPoint(xy.x, m00, m01, m02, m03);
			var p1 = GetPoint(xy.x, m10, m11, m12, m13);
			var p2 = GetPoint(xy.x, m20, m21, m22, m23);
			var p3 = GetPoint(xy.x, m30, m31, m32, m33);
			return GetPoint(xy.y, p0, p1, p2, p3);
		}

		/// <summary>
		/// Standard cubic bezier
		/// </summary>
		private Vector3 GetPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			float c = 1.0f - t;

			float bb0 = c * c * c;
			float bb1 = 3 * t * c * c;
			float bb2 = 3 * t * t * c;
			float bb3 = t * t * t;

			return p0 * bb0 + p1 * bb1 + p2 * bb2 + p3 * bb3;
		}
	}

#if UNITY_EDITOR
	/// <summary>
	/// Draws handles for each of the bezier control points in the scene view
	/// </summary>
	[CustomEditor(typeof(BezierPatchUIEffect)), CanEditMultipleObjects]
	class BezierPatchEditor : Editor
    {
		protected virtual void OnSceneGUI()
		{
			BezierPatchUIEffect effect = (BezierPatchUIEffect)target;
			if (!effect.isActiveAndEnabled) return;

			EditorGUI.BeginChangeCheck();
			BezierPatch4x4 newBezier = Draw(effect.BezierPatch4X4);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(effect, "BezierPatch4X4");
				effect.BezierPatch4X4 = newBezier;
				EditorUtility.SetDirty(effect);
			}
		}

		/// <summary>
		/// Draws the 16 handles for the bezier patch, with interconnecting lines
		/// </summary>
		BezierPatch4x4 Draw(BezierPatch4x4 bezierPatch)
		{
			Handles.color = Color.blue;
			BezierPatchUIEffect example = (BezierPatchUIEffect)target;
			var rectTransform = example.transform as RectTransform;

			var size = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
			var origin = -rectTransform.pivot * size;

			bezierPatch.m00 = DrawHandle(rectTransform, size, origin, bezierPatch.m00);
			bezierPatch.m01 = DrawHandle(rectTransform, size, origin, bezierPatch.m01);
			bezierPatch.m02 = DrawHandle(rectTransform, size, origin, bezierPatch.m02);
			bezierPatch.m03 = DrawHandle(rectTransform, size, origin, bezierPatch.m03);
			bezierPatch.m10 = DrawHandle(rectTransform, size, origin, bezierPatch.m10);
			bezierPatch.m11 = DrawHandle(rectTransform, size, origin, bezierPatch.m11);
			bezierPatch.m12 = DrawHandle(rectTransform, size, origin, bezierPatch.m12);
			bezierPatch.m13 = DrawHandle(rectTransform, size, origin, bezierPatch.m13);
			bezierPatch.m20 = DrawHandle(rectTransform, size, origin, bezierPatch.m20);
			bezierPatch.m21 = DrawHandle(rectTransform, size, origin, bezierPatch.m21);
			bezierPatch.m22 = DrawHandle(rectTransform, size, origin, bezierPatch.m22);
			bezierPatch.m23 = DrawHandle(rectTransform, size, origin, bezierPatch.m23);
			bezierPatch.m30 = DrawHandle(rectTransform, size, origin, bezierPatch.m30);
			bezierPatch.m31 = DrawHandle(rectTransform, size, origin, bezierPatch.m31);
			bezierPatch.m32 = DrawHandle(rectTransform, size, origin, bezierPatch.m32);
			bezierPatch.m33 = DrawHandle(rectTransform, size, origin, bezierPatch.m33);

			DrawLine(rectTransform, size, origin, bezierPatch.m00, bezierPatch.m01, bezierPatch.m02, bezierPatch.m03);
			DrawLine(rectTransform, size, origin, bezierPatch.m10, bezierPatch.m11, bezierPatch.m12, bezierPatch.m13);
			DrawLine(rectTransform, size, origin, bezierPatch.m20, bezierPatch.m21, bezierPatch.m22, bezierPatch.m23);
			DrawLine(rectTransform, size, origin, bezierPatch.m30, bezierPatch.m31, bezierPatch.m32, bezierPatch.m33);

			DrawLine(rectTransform, size, origin, bezierPatch.m00, bezierPatch.m10, bezierPatch.m20, bezierPatch.m30);
			DrawLine(rectTransform, size, origin, bezierPatch.m01, bezierPatch.m11, bezierPatch.m21, bezierPatch.m31);
			DrawLine(rectTransform, size, origin, bezierPatch.m02, bezierPatch.m12, bezierPatch.m22, bezierPatch.m32);
			DrawLine(rectTransform, size, origin, bezierPatch.m03, bezierPatch.m13, bezierPatch.m23, bezierPatch.m33);

			return bezierPatch;
		}

		/// <summary>
		/// Draws a single bezier control handle
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
			Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p1), ToWorldSpace(rectTransform, size, origin, p2));
			Handles.DrawLine(ToWorldSpace(rectTransform, size, origin, p2), ToWorldSpace(rectTransform, size, origin, p3));
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