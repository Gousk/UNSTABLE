using UnityEngine;

public class FpsMeter : MonoBehaviour
{
    float _dt, _fps, _ms;
    const float smooth = 0.1f;
    void Update()
    {
        _dt = Mathf.Lerp(_dt, Time.unscaledDeltaTime, smooth);
        _fps = 1f / _dt;
        _ms = _dt * 1000f;
    }
    void OnGUI()
    {
        GUI.color = Color.black; GUI.Label(new Rect(11, 11, 200, 30), $"{_fps:0} FPS  {_ms:0.0} ms");
        GUI.color = Color.white; GUI.Label(new Rect(10, 10, 200, 30), $"{_fps:0} FPS  {_ms:0.0} ms");
    }
}
