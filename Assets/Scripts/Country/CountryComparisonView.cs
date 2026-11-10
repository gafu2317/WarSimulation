using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class CountryComparisonView : MonoBehaviour
{
    public Camera viewCamera;
    public GameObject[] cities;
    public Vector3[] centers;
    public Vector3 initialFocus = new(0, 6, 80);
    public GameObject[] hiddenCountryGround = new GameObject[0];
    public int viewIndex;
    UniversalRenderPipelineAsset scenePipeline;
    RenderPipelineAsset previousPipeline;
    bool previousBackground;
    bool changedBackground;
    InputActionMap controls;

    public bool InputEnabled
    {
        get => controls != null && controls.enabled;
        set
        {
            if (value) controls?.Enable();
            else controls?.Disable();
        }
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        changedBackground = true;
        controls = new InputActionMap("Country comparison");
        controls.AddAction("Old",InputActionType.Button,"<Keyboard>/1").performed += _ => Show(0);
        controls.AddAction("New",InputActionType.Button,"<Keyboard>/2").performed += _ => Show(1);
        controls.AddAction("Both",InputActionType.Button,"<Keyboard>/3").performed += _ => Show(2);
        controls.AddAction("Swap",InputActionType.Button,"<Keyboard>/tab").performed += _ => Swap();
        controls.Enable();
        var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
            ?? QualitySettings.renderPipeline as UniversalRenderPipelineAsset
            ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (source == null) return;
        previousPipeline = QualitySettings.renderPipeline;
        scenePipeline = Instantiate(source);
        scenePipeline.name = source.name + " (Country)";
        scenePipeline.hideFlags = HideFlags.HideAndDontSave;
        scenePipeline.shadowDistance = 300;
        scenePipeline.shadowCascadeCount = 2;
        scenePipeline.mainLightShadowmapResolution = 2048;
        scenePipeline.renderScale = 1;
        QualitySettings.renderPipeline = scenePipeline;
    }

    void Start()
    {
        if (Application.isPlaying) ShowInitialView();
    }

    void OnDisable()
    {
        controls?.Disable();
        controls?.Dispose();
        controls = null;
        if (changedBackground) { Application.runInBackground = previousBackground; changedBackground = false; }
        if (scenePipeline == null) return;
        if (QualitySettings.renderPipeline == scenePipeline) QualitySettings.renderPipeline = previousPipeline;
        if (Application.isPlaying) Destroy(scenePipeline);
        else DestroyImmediate(scenePipeline);
        scenePipeline = null;
        previousPipeline = null;
    }

    public void ShowInitialView()
    {
        Show(1);
        var target = centers[1] + initialFocus;
        viewCamera.transform.position = target - viewCamera.transform.forward * 130f;
        viewCamera.GetComponent<CountryCameraController>()?.SyncStateFromTransform();
    }

    public void Show(int index)
    {
        var requested = Mathf.Clamp(index, 0, 2);
        var overview = requested == 2;
        var unavailable = overview ? cities[0] == null || cities[1] == null : cities[requested] == null;
        if (unavailable) return;
        viewIndex = requested;
        for (var i = 0; i < cities.Length; i++)
            SetCityVisible(i, overview || i == viewIndex);
        var center = overview ? (centers[0] + centers[1]) * .5f : centers[viewIndex];
        var offset = overview ? new Vector3(0, 350, -370) : new Vector3(0, 215, -245);
        viewCamera.transform.position = center + offset;
        viewCamera.transform.LookAt(center + new Vector3(0, 6, overview ? 28 : -5));
        viewCamera.fieldOfView = overview ? 50 : 40;
        viewCamera.GetComponent<EditorStyleCameraController>()?.SyncStateFromTransform();
        viewCamera.GetComponent<CountryCameraController>()?.SyncStateFromTransform();
    }

    public void Swap()
    {
        if (viewIndex == 2) { Show(1); return; }
        var previous = viewIndex;
        var next = 1 - previous;
        if (cities[next] == null) return;
        viewIndex = next;
        viewCamera.transform.position += centers[next] - centers[previous];
        for (var i = 0; i < cities.Length; i++) SetCityVisible(i, i == next);
        viewCamera.GetComponent<EditorStyleCameraController>()?.SyncStateFromTransform();
        viewCamera.GetComponent<CountryCameraController>()?.SyncStateFromTransform();
    }

    public void SetCityVisible(int index, bool visible)
    {
        var city = cities[index];
        if (city != null) city.SetActive(visible);
        if (index < hiddenCountryGround.Length && hiddenCountryGround[index] != null)
            hiddenCountryGround[index].SetActive(!visible);
    }
}
