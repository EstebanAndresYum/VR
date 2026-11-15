using UnityEngine;
using Bhaptics.SDK2;

public class TimeWarningController : MonoBehaviour
{
    [SerializeField] private string eventName = "time_warning";
    [SerializeField] private bool enableHaptics = false;
    [SerializeField, Min(0f)] private float startSeconds = 35f;

    [SerializeField] private float remainingSeconds;
    [SerializeField] private bool isRunning;

    private int currentBand;
    private float warningCountdown;
    private int activeRequestId = -1;

    public void StartTimer()
    {
        StopTimer();

        remainingSeconds = Mathf.Max(0f, startSeconds);
        currentBand = 0;
        warningCountdown = 0f;
        isRunning = remainingSeconds > 0f;
    }

    public void StopTimer()
    {
        isRunning = false;
        StopCurrentPulse();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        remainingSeconds = Mathf.Max(
            0f,
            remainingSeconds - Time.deltaTime
        );

        if (remainingSeconds <= 0f)
        {
            StopTimer();
            Debug.Log("시간 종료", this);
            return;
        }

        int band = GetWarningBand();

        if (band == 0)
        {
            return;
        }

        warningCountdown -= Time.deltaTime;

        if (band != currentBand)
        {
            currentBand = band;
            warningCountdown = 0f;
        }

        if (warningCountdown > 0f)
        {
            return;
        }

        float interval;
        float intensity;

        switch (band)
        {
            case 10:
                interval = 0.55f;
                intensity = 1.00f;
                break;

            case 20:
                interval = 1.00f;
                intensity = 0.80f;
                break;

            default:
                interval = 1.50f;
                intensity = 0.65f;
                break;
        }

        warningCountdown = interval;
        StopCurrentPulse();

        if (!enableHaptics || !BhapticsSDK2.IsInitialized)
        {
            return;
        }

        activeRequestId = BhapticsLibrary.Play(eventName, intensity: intensity);

        if (activeRequestId < 0)
        {
            Debug.LogWarning(
                $"진동 요청 실패. 연결과 이벤트 이름 확인: {eventName}",
                this
            );
        }
    }

    private int GetWarningBand()
    {
        if (remainingSeconds <= 10f) return 10;
        if (remainingSeconds <= 20f) return 20;
        if (remainingSeconds <= 30f) return 30;

        return 0;
    }

    private void StopCurrentPulse()
    {
        if (activeRequestId < 0)
        {
            return;
        }

        BhapticsLibrary.StopInt(activeRequestId);
        activeRequestId = -1;
    }

    private void OnDisable()
    {
        StopTimer();
    }

    private void OnGUI()
    {
        GUI.Box(
            new Rect(20, 20, 260, 160),
            "남은 시간 경고"
        );

        GUI.Label(
            new Rect(35, 55, 230, 25),
            $"남은 시간: {remainingSeconds:F1}초"
        );

        GUI.Label(
            new Rect(35, 80, 230, 25),
            isRunning ? "진행 중" : "정지 / 종료"
        );

        if (GUI.Button(new Rect(35, 115, 105, 40), "시작 / 재시작"))
        {
            StartTimer();
        }

        if (GUI.Button(new Rect(150, 115, 105, 40), "종료"))
        {
            StopTimer();
        }
    }
}