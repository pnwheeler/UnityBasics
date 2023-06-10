using UnityEngine;
using TMPro;

public class FrameRateCounter : MonoBehaviour
{
    // Field to display text
    [SerializeField]
    TextMeshProUGUI display;
    
    // Field to select mode: frames per sec or milliseconds
    public enum DisplayMode { FPS, MS }
    [SerializeField]
    DisplayMode displayMode = DisplayMode.FPS;

    // Field to adjust timestep
    [SerializeField, Range(0.1f, 2f)]
    float sampleDuration = 1f;

    // Num frames
    int frames;
    // Elapsed time, duration of most and least performant frames
    float duration, bestDuration = float.MaxValue, worstDuration;

    private void Update()
    {
        // Updates 
        float frameDuration = Time.unscaledDeltaTime;
        frames += 1;
        duration += frameDuration;
        if (frameDuration < bestDuration) bestDuration = frameDuration;
        if (frameDuration > worstDuration) worstDuration = frameDuration;
        // Update UI when duration exceeds time window
        if (duration >= sampleDuration) {
            if (displayMode == DisplayMode.FPS) {
                display.SetText(
                   "FPS\nHigh : {0:0}\nAvg : {1:0}\nLow : {2:0}",
                   1f / bestDuration,
                   frames / duration,
                   1f / worstDuration
               );
            } else {
                display.SetText(
                   "MS\nHigh : {0:1}\nAvg : {1:1}\nLow : {2:1}",
                   1000f * bestDuration,
                   1000f * duration / frames,
                   1000f * worstDuration
               );
            }
            // Reset counters
            frames = 0;
            duration = 0f;
            bestDuration = float.MaxValue;
            worstDuration = 0f;
        }
        
    }
}