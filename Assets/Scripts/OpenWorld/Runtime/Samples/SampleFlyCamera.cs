using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OpenWorld.Samples
{
    /// <summary>
    /// サンプル用フライカメラ。WASD 移動 / QE 上下 / 右ドラッグ視点 / Shift 加速。
    /// </summary>
    public class SampleFlyCamera : MonoBehaviour
    {
        public float moveSpeed = 60f;
        public float fastMultiplier = 4f;
        public float lookSensitivity = 0.15f;

        float _yaw;
        float _pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 視点 (右ボタンドラッグ)
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // 移動
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += transform.forward;
            if (kb.sKey.isPressed) move -= transform.forward;
            if (kb.dKey.isPressed) move += transform.right;
            if (kb.aKey.isPressed) move -= transform.right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move -= Vector3.up;

            float speed = moveSpeed * (kb.leftShiftKey.isPressed ? fastMultiplier : 1f);
            transform.position += move.normalized * (speed * Time.deltaTime);
#endif
        }
    }
}
