using System;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Controls a projectile's parabolic flight from a start position to a predicted end position.
    /// Attach to a projectile GameObject with a SpriteRenderer.
    /// Call Launch() to begin flight; the onArrive callback fires when the projectile reaches its destination.
    /// </summary>
    public class ProjectileController : MonoBehaviour
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private float flightDuration;
        private float arcHeight;
        private Action onArrive;

        private float elapsed;
        private bool isFlying;

        /// <summary>
        /// Begin parabolic flight toward the predicted landing position.
        /// </summary>
        /// <param name="start">World position where the projectile spawns.</param>
        /// <param name="predictedEnd">Predicted landing position (accounts for target movement).</param>
        /// <param name="duration">Total flight time in seconds.</param>
        /// <param name="arc">Peak height of the parabolic arc above the start-end line.</param>
        /// <param name="onArriveCallback">Invoked when the projectile reaches the landing position.</param>
        public void Launch(Vector3 start, Vector3 predictedEnd, float duration, float arc, Action onArriveCallback)
        {
            startPos = start;
            endPos = predictedEnd;
            flightDuration = Mathf.Max(duration, 0.01f);
            arcHeight = arc;
            onArrive = onArriveCallback;
            elapsed = 0f;
            isFlying = true;

            transform.position = start;
        }

        private void Update()
        {
            if (!isFlying) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            // Horizontal: linear interpolation from start to end
            Vector3 flatPos = Vector3.Lerp(startPos, endPos, t);

            // Vertical: parabolic arc offset — peaks at t=0.5
            float arcOffset = arcHeight * 4f * t * (1f - t);
            flatPos.y += arcOffset;

            // Calculate tangent direction for rotation
            if (t < 1f)
            {
                // Approximate tangent by looking at next frame's position
                float tNext = Mathf.Clamp01((elapsed + Time.deltaTime) / flightDuration);
                Vector3 flatNext = Vector3.Lerp(startPos, endPos, tNext);
                float arcNext = arcHeight * 4f * tNext * (1f - tNext);
                flatNext.y += arcNext;

                Vector3 tangent = flatNext - flatPos;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            transform.position = flatPos;

            // Arrived at destination
            if (t >= 1f)
            {
                isFlying = false;
                transform.position = endPos;
                onArrive?.Invoke();
            }
        }

        /// <summary>
        /// Reset state when retrieved from object pool.
        /// </summary>
        public void ResetState()
        {
            isFlying = false;
            elapsed = 0f;
            onArrive = null;
            transform.rotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            isFlying = false;
            onArrive = null;
        }
    }
}
