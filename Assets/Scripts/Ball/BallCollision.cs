using UnityEngine;
using BeastSoccer.Player;

namespace BeastSoccer.Ball
{
    [RequireComponent(typeof(BallControl))]
    public class BallCollision : MonoBehaviour
    {
        private BallControl ball;
        private void Awake() { ball = GetComponent<BallControl>(); }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            var p = collision.collider.GetComponentInParent<PlayerController>();
            if (p != null) ball.RegisterPhysicalTouch(p);
        }
    }
}
