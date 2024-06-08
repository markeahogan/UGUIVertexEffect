using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Modifies the FrameBounds of the graphic and shows the position and rotate gizmos in the modified graphics position
    /// </summary>
    [CustomEditor(typeof(UIMeshModifier))]
    public class UGUIMeshModifierEditor : Editor
    {
        private bool HasFrameBounds() => InternalEditorUtility.GetIsInspectorExpanded(target);

        private void OnDisable()
        {
            Tools.hidden = false;
        }

        public Bounds OnGetFrameBounds()
        {
            var meshEffector = target as UIMeshModifier;
            return meshEffector.CalculateWorldSpaceBounds();
        }

        private void OnSceneGUI()
        {
            if (!InternalEditorUtility.GetIsInspectorExpanded(target)) return;

            var meshEffector = target as UIMeshModifier;
            var pose = meshEffector.CalculateWorldSpacePose();
            var position = pose.position;
            var rotation = pose.rotation;

            if (Tools.current == Tool.Move)
            {
                Tools.hidden = true;
                EditorApplication.delayCall += () => Tools.hidden = false;

                position = Handles.PositionHandle(position, rotation);
            }
            else if (Tools.current == Tool.Rotate)
            {
                Tools.hidden = true;
                EditorApplication.delayCall += () => Tools.hidden = false;

                rotation = Handles.RotationHandle(rotation, position);
            }
            else
            {
                Tools.hidden = false;
            }

            var newPose = new Pose(position, rotation);
            var delta = GetInverseTransformedBy(newPose, pose);

            if (delta != Pose.identity)
            {
                Undo.RecordObject(meshEffector.transform, "UIMeshModifier");
                var currentPose = new Pose(meshEffector.transform.position, meshEffector.transform.rotation);
                var result = delta.GetTransformedBy(currentPose);
                meshEffector.transform.SetPositionAndRotation(result.position, result.rotation);
            }
        }
        
        public static Pose GetInverseTransformedBy(Pose pose, Pose lhs)
        {
            return new Pose
            {
                position = Quaternion.Inverse(lhs.rotation) * (pose.position - lhs.position),
                rotation = Quaternion.Inverse(lhs.rotation) * pose.rotation
            };
        }
    }
}