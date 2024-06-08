using UnityEngine;

namespace PopupAsylum.UIEffects.Examples
{
	public class TiltWindow : MonoBehaviour
	{
		public Vector2 range = new Vector2(5f, 3f);

		private Quaternion _start;
		private Vector2 _angle = Vector2.zero;

		void Start()
		{
			_start = transform.localRotation;
		}

		void Update()
		{
			Vector3 pos = Input.mousePosition;

			float halfWidth = Screen.width * 0.5f;
			float halfHeight = Screen.height * 0.5f;
			float x = Mathf.Clamp((pos.x - halfWidth) / halfWidth, -1f, 1f);
			float y = Mathf.Clamp((pos.y - halfHeight) / halfHeight, -1f, 1f);
			_angle = Vector2.Lerp(_angle, new Vector2(x, y), Time.deltaTime * 5f);

			transform.localRotation = _start * Quaternion.Euler(-_angle.y * range.y, _angle.x * range.x, 0f);
		}
	}
}
