using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("References")]
    public GameObject gruntPrefab;
    public Transform spawnAreaMin;
    public Transform spawnAreaMax;

    [Header("Wave Settings")]
    public float timeBetweenWaves = 3f;
    public float spawnInterval = 0.4f;

    private List<EnemyBase> _activeEnemies = new List<EnemyBase>();
    private int _currentWave = 0;
    private bool _waveInProgress = false;

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
    }

    private void OnGameStart()
    {
        _activeEnemies.Clear();
        _currentWave = 0;
        _waveInProgress = false;
        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying())
            {
                yield return null;
                continue;
            }
            if (!_waveInProgress)
            {
                yield return StartCoroutine(SpawnWave(_currentWave));
                yield return new WaitUntil(() => AllEnemiesDead());
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying())
                    GameManager.Instance.CompleteWave();
                yield return new WaitForSeconds(timeBetweenWaves);
                _currentWave++;
            }
            yield return null;
        }
    }

    private IEnumerator SpawnWave(int waveIndex)
    {
        _waveInProgress = true;
        _activeEnemies.Clear();

        int gruntCount = Mathf.Min(4 + waveIndex, 12);
        float minX = spawnAreaMin != null ? spawnAreaMin.position.x : -3.5f;
        float maxX = spawnAreaMax != null ? spawnAreaMax.position.x : 3.5f;
        float minY = spawnAreaMin != null ? spawnAreaMin.position.y : 5f;
        float maxY = spawnAreaMax != null ? spawnAreaMax.position.y : 6.5f;

        int cols = Mathf.Min(gruntCount, 4);
        int rows = Mathf.CeilToInt((float)gruntCount / cols);
        float xStep = (maxX - minX) / Mathf.Max(cols - 1, 1);
        float yStep = 0.8f;

        int spawned = 0;
        for (int r = 0; r < rows && spawned < gruntCount; r++)
        {
            for (int c = 0; c < cols && spawned < gruntCount; c++)
            {
                float x = cols > 1 ? minX + c * xStep : (minX + maxX) * 0.5f;
                float y = maxY - r * yStep;
                SpawnGrunt(new Vector3(x, y, 0));
                spawned++;
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        _waveInProgress = false;
    }

    private void SpawnGrunt(Vector3 pos)
    {
        if (gruntPrefab == null) return;
        GameObject go = Instantiate(gruntPrefab, pos, Quaternion.identity);
        EnemyBase enemy = go.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            _activeEnemies.Add(enemy);
            enemy.onDeath.AddListener(OnEnemyDied);
        }
    }

    private void OnEnemyDied(EnemyBase enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    private bool AllEnemiesDead()
    {
        _activeEnemies.RemoveAll(e => e == null);
        return _activeEnemies.Count == 0;
    }
}
