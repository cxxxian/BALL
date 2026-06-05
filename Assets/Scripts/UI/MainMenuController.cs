using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement mainPanel;
    private VisualElement campaignPanel;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;
        
        mainPanel = root.Q<VisualElement>("MainMenuPanel");
        campaignPanel = root.Q<VisualElement>("CampaignPanel");

        // 绑定按钮
        var btnCampaign = root.Q<Button>("BtnCampaign");
        if (btnCampaign != null) btnCampaign.clicked += ShowCampaign;

        var btnBack = root.Q<Button>("BtnBack");
        if (btnBack != null) btnBack.clicked += ShowMainMenu;

        var btnLaunch = root.Q<Button>("BtnLaunch");
        if (btnLaunch != null) btnLaunch.clicked += LaunchGame;

        var btnEndless = root.Q<Button>("BtnEndless");
        if (btnEndless != null) btnEndless.clicked += LaunchGame; // 暂时直接进游戏
        
        // 绑定退出
        var btnQuit = root.Q<Button>("BtnQuit");
        if (btnQuit != null) btnQuit.clicked += Application.Quit;

        // 生成假卡片与拖拽逻辑
        var scroll = root.Q<ScrollView>("LevelScrollView");
        if (scroll != null)
        {
            // 隐藏滚动条（双重保险）
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            // 增加鼠标/触摸丝滑拖拽功能
            bool isDragging = false;
            Vector2 startPos = Vector2.zero;
            Vector2 startOffset = Vector2.zero;

            scroll.RegisterCallback<PointerDownEvent>(e => {
                isDragging = true;
                startPos = e.position;
                startOffset = scroll.scrollOffset;
                scroll.CapturePointer(e.pointerId);
            });

            scroll.RegisterCallback<PointerMoveEvent>(e => {
                if (isDragging) {
                    Vector2 delta = (Vector2)e.position - startPos;
                    scroll.scrollOffset = new Vector2(startOffset.x - delta.x, startOffset.y);
                }
            });

            scroll.RegisterCallback<PointerUpEvent>(e => {
                if (isDragging) {
                    isDragging = false;
                    scroll.ReleasePointer(e.pointerId);
                }
            });

            scroll.RegisterCallback<PointerCaptureOutEvent>(e => {
                isDragging = false;
            });

            scroll.Clear();
            for (int i = 1; i <= 3; i++)
            {
                var card = new VisualElement();
                card.AddToClassList("level-card");
                
                var deco = new VisualElement();
                deco.AddToClassList("card-deco");
                card.Add(deco);

                var num = new Label($"STAGE 0{i}");
                num.AddToClassList("card-num");
                card.Add(num);

                var name = new Label(i == 1 ? "石巨像之怒" : (i == 2 ? "电磁核心之灾" : "未解锁星区"));
                name.AddToClassList("card-name");
                card.Add(name);
                
                var stars = new Label(i == 1 ? "★★★" : "[ 🔒 ]");
                stars.AddToClassList("card-stars");
                card.Add(stars);

                scroll.Add(card);
            }
        }
    }

    private void ShowCampaign()
    {
        mainPanel.style.display = DisplayStyle.None;
        campaignPanel.style.display = DisplayStyle.Flex;
    }

    private void ShowMainMenu()
    {
        mainPanel.style.display = DisplayStyle.Flex;
        campaignPanel.style.display = DisplayStyle.None;
    }

    private void LaunchGame()
    {
        // 假设核心玩法场景叫 SampleScene
        SceneManager.LoadScene("SampleScene");
    }
}
