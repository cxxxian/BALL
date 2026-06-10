using UnityEngine;

public class BottomBoundary : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other) => TryBallFall(other);
    private void OnTriggerStay2D(Collider2D other)  => TryBallFall(other);

    private static void TryBallFall(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null || !ball.CanLoseLifeFromBottom()) return;

        if (GameManager.Instance != null)
            GameManager.Instance.BallFellDown();
    }
}
