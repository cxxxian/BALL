using System;
using UnityEngine;

public class ShopOfferCard : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Text title, status;
    [SerializeField] private UnityEngine.UI.RawImage icon;
    [SerializeField] private UnityEngine.UI.Image selectionLine;
    [SerializeField] private UnityEngine.UI.Button button;
    private BallDefinition ball;
    public void Bind(BallDefinition definition, Action<BallDefinition> select)
    {
        ball = definition; title.text = ball.displayName;
        icon.color = Color.Lerp(Color.white, ball.glowColor, 0.18f);
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => select(ball));
    }
    public void Refresh(BallDefinition selected)
    {
        selectionLine.enabled = selected == ball;
        bool owned = PlayerProfile.IsBallUnlocked(ball.ballId);
        status.text = owned ? "已拥有" : "可购买";
        button.image.color = selected == ball ? new Color(0.025f, 0.14f, 0.18f) : new Color(0.025f, 0.055f, 0.075f);
    }
}
