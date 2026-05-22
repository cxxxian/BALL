using UnityEngine;
using UnityEngine.Events;

public enum GameState { Idle, Playing, BallRespawning, BuffSelection, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Config")]
    public GameConfig config;

    public GameState State { get; private set; } = GameState.Idle;
    public int Lives { get; private set; }
    public int Score { get; private set; }
    public int Wave { get; private set; } = 0;

    public UnityEvent<int> onLivesChanged = new UnityEvent<int>();
    public UnityEvent<int> onScoreChanged = new UnityEvent<int>();
    public UnityEvent<int> onWaveChanged = new UnityEvent<int>();
    public UnityEvent onGameOver = new UnityEvent();
    public UnityEvent onGameStart = new UnityEvent();
    public UnityEvent onBallLost = new UnityEvent();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartGame()
    {
        Lives = config.initialLives;
        Score = 0;
        Wave = 0;
        State = GameState.Playing;
        onGameStart.Invoke();
        onLivesChanged.Invoke(Lives);
        onScoreChanged.Invoke(Score);
        onWaveChanged.Invoke(Wave);
    }

    public void BallFellDown()
    {
        if (State != GameState.Playing) return;
        Lives--;
        onLivesChanged.Invoke(Lives);
        if (Lives <= 0)
        {
            TriggerGameOver();
        }
        else
        {
            State = GameState.BallRespawning;
            onBallLost.Invoke();
        }
    }

    public void OnBallRespawned()
    {
        if (State == GameState.BallRespawning)
            State = GameState.Playing;
    }

    public void AddScore(int points)
    {
        Score += points;
        onScoreChanged.Invoke(Score);
    }

    public void CompleteWave()
    {
        Wave++;
        onWaveChanged.Invoke(Wave);
        State = GameState.BuffSelection;
    }

    public void OnBuffSelectionDone()
    {
        if (State == GameState.BuffSelection)
            State = GameState.Playing;
    }

    public void TriggerGameOver()
    {
        State = GameState.GameOver;
        onGameOver.Invoke();
    }

    public bool IsPlaying() => State == GameState.Playing;
}
