using UnityEngine;
using BeastSoccer.Data;

namespace BeastSoccer.Ball
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class BallPhysics : MonoBehaviour
    {
        private Rigidbody2D rb;
        private void Awake() { rb = GetComponent<Rigidbody2D>(); }
        private void Start() { rb.gravityScale = 0f; rb.linearDamping = GameConfig.Instance.ballLinearDrag; }
    }
}
