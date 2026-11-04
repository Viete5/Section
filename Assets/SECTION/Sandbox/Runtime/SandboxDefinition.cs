using System;
using UnityEngine;

namespace Section.Sandbox
{
    [CreateAssetMenu(menuName = "SECTION/Sandbox Definition")]
    public sealed class SandboxDefinition : ScriptableObject
    {
        [Header("Approved M1 geometry (metres)")]
        public float width = 14f;
        public float roomPitch = 10f;
        public float height = 5f;
        public float thickness = 0.3f;
        public float doorwayWidth = 3f;
        public Vector2 pitX = new Vector2(2.5f, 6f);
        public Vector2 pitZ = new Vector2(13f, 18f);
        public float pitBottom = -2.5f;

        [Header("Player")]
        public Vector3 spawnFeet = new Vector3(0, 0, 1.5f);
        public float playerHeight = 1.8f;
        public float playerRadius = 0.35f;
        public float eyeHeight = 1.65f;
        public float moveSpeed = 4f;
        public float jumpHeight = 0.6f;
        public float gravity = 9.81f;
        public float mouseSensitivity = 0.12f;

        public float Length => roomPitch * 4f;

        public Bounds ClearRoomBounds(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
            float from = index * roomPitch + (index == 0 ? 0 : thickness * 0.5f);
            float to = (index + 1) * roomPitch - (index == 3 ? 0 : thickness * 0.5f);
            return new Bounds(new Vector3(0, height * 0.5f, (from + to) * 0.5f), new Vector3(width, height, to - from));
        }

        public void ValidateApprovedGeometry()
        {
            bool geometry = Near(width, 14) && Near(roomPitch, 10) && Near(height, 5) && Near(thickness, .3f) &&
                Near(doorwayWidth, 3) && pitX == new Vector2(2.5f, 6) && pitZ == new Vector2(13, 18) && Near(pitBottom, -2.5f);
            bool player = spawnFeet == new Vector3(0, 0, 1.5f) && Near(playerHeight, 1.8f) && Near(playerRadius, .35f) &&
                Near(eyeHeight, 1.65f) && Near(moveSpeed, 4);
            if (!geometry || !player) throw new InvalidOperationException("Sandbox geometry/player dimensions differ from the approved M1 baseline. Review the specification before rebuilding.");
            if (!(gravity > 0 && jumpHeight > 0 && mouseSensitivity > 0) ||
                !float.IsFinite(gravity + jumpHeight + mouseSensitivity))
                throw new InvalidOperationException("Sandbox movement parameters must be finite and positive.");
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.00001f;
    }
}
