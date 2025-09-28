using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays episode information: elapsed time, steps, stall countdown, control mode.
/// Designed to be lightweight and update at a throttled rate.
/// </summary>
[DisallowMultipleComponent]
public class EpisodeHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public BoardAgent agent;
    [SerializeField] public TiltController tiltController;

    [Header("UI")]
    [SerializeField] public Text label; // assigned by setup service
    [SerializeField] public string title = "Episode";
    [SerializeField] public bool showHeader = true;
    [SerializeField] public string numberFormat = "F1";
    [SerializeField] public float updateHz = 10f;

    private float nextUpdateAt;
    private float episodeStartTime;
    private int lastStepCount;

    void OnEnable()
    {
        episodeStartTime = Time.time;
        lastStepCount = 0;
    }

    void Update()
    {
        if (label == null || agent == null) return;

        // Detect new episode via step reset
        if (agent.StepCount < lastStepCount)
        {
            episodeStartTime = Time.time;
        }
        lastStepCount = agent.StepCount;

        if (Time.time < nextUpdateAt) return;
        nextUpdateAt = Time.time + (updateHz > 0f ? (1f / updateHz) : 0f);

        float elapsed = Time.time - episodeStartTime;
        int maxStep = agent.MaxStep;
        int step = agent.StepCount;
        // int stepsRemaining = Mathf.Max(0, maxStep - step);
        float stallT = agent.TimeSinceLastProgress;
        float stallLimit = agent.StallTimeoutSeconds;
        bool stalled = stallT > 1f; // show after brief pause
        float stallRemain = Mathf.Max(0f, stallLimit - stallT);

        System.Text.StringBuilder sb = new System.Text.StringBuilder(160);
        if (showHeader && !string.IsNullOrEmpty(title))
        {
            if (label.supportRichText) sb.Append("<b>").Append(title).Append("</b>\n");
            else sb.Append(title).Append("\n");
        }
        sb.Append("Time: ").Append(elapsed.ToString(numberFormat)).Append("s\n");
        sb.Append("Steps: ").Append(step).Append('/').Append(maxStep);
        if (stalled)
        {
            sb.Append("\nStall: ").Append(stallRemain.ToString(numberFormat)).Append("s");
        }
        float pct = agent.PathCompletion01 * 100f;
        sb.Append("\nProgress: ").Append(pct.ToString("F0")).Append("%");

        label.text = sb.ToString();
    }
}


