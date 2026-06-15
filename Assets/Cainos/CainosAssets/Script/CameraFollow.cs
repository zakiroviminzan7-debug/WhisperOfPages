using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float lerpSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPos = new Vector3(
                target.position.x,
                target.position.y,
                -10f
            );

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                lerpSpeed * Time.deltaTime
            );
        }
    }
}