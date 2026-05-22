using UnityEngine;

public class BottomBoundary : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.BallFellDown();
        }
    }
}
