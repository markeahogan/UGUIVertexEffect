using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    [CustomEditor(typeof(SkewUIEffect))]
    public class SkewUIEffectEditor : Editor
    {
        private void OnSceneGUI()
        {
            if (!InternalEditorUtility.GetIsInspectorExpanded(target)) return;
            var skewUI = target as SkewUIEffect;

            var rect = skewUI.transform as RectTransform;
            var vert = UIVertex.simpleVert;
            vert.position = new Vector3(-rect.pivot.x * rect.rect.width + rect.rect.width / 2, -rect.pivot.y * rect.rect.height + rect.rect.height);
            skewUI.ModifyVertex(rect, ref vert);

            var topOS = vert.position;
            Vector3 topWS = rect.TransformPoint(topOS);
            Vector3 newTopPos = Handles.FreeMoveHandle(topWS, Quaternion.identity, HandleUtility.GetHandleSize(topWS) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);
            newTopPos = rect.InverseTransformPoint(newTopPos);

            skewUI.SkewX += (newTopPos.x - topOS.x) / rect.rect.width;

            vert.position = new Vector3(-rect.pivot.x * rect.rect.width + rect.rect.width, -rect.pivot.y * rect.rect.height + rect.rect.height/2);
            skewUI.ModifyVertex(rect, ref vert);

            var rightOS = vert.position;
            Vector3 rightWS = rect.TransformPoint(rightOS);
            Vector3 newRightPos = Handles.FreeMoveHandle(rightWS, Quaternion.identity, HandleUtility.GetHandleSize(rightWS) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);
            newRightPos = rect.InverseTransformPoint(newRightPos);

            skewUI.SkewY += (newRightPos.y - rightOS.y) / rect.rect.height;
        }
    }
}
