using UnityEngine;

public class PassthroughManager : MonoBehaviour
{
    void Start()
    {
        // Enable passthrough
        OVRManager.instance.isInsightPassthroughEnabled = true;

        // Set up passthrough layer
        OVRPassthroughLayer passthroughLayer = gameObject.AddComponent<OVRPassthroughLayer>();
        passthroughLayer.textureOpacity = 1f;
        passthroughLayer.edgeRenderingEnabled = true;
        passthroughLayer.edgeColor = new Color(0f, 0.5f, 1f, 1f);

        // Make camera background transparent
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = new Color(0, 0, 0, 0);
    }
}