using UnityEngine;
using UnityEngine.InputSystem;

namespace Coinbound
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DungeonPlayer : MonoBehaviour
    {
        [SerializeField, Min(0)] private float speed = 5;
        private Rigidbody2D body;
        private Vector2 movement;
        private bool inputEnabled;

        public DungeonHero Hero { get; private set; } = DungeonHero.First;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        public void Initialize(DungeonHero hero) => Hero = hero ?? DungeonHero.First;

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            movement = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            movement = Vector2.zero;
            if (!inputEnabled || !Application.isFocused) return;
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) movement.x--;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) movement.x++;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) movement.y++;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) movement.y--;
            }
            if (Gamepad.current != null) movement += Gamepad.current.leftStick.ReadValue();
            movement = Vector2.ClampMagnitude(movement, 1);
        }

        private void FixedUpdate() => body.linearVelocity = movement * speed;
        private void OnApplicationFocus(bool focused) { if (!focused) movement = Vector2.zero; }
        private void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }
    }
}
