using System;
using MechMaster.Domain;
using MechMaster.Runtime.UI;
using UnityEngine;

namespace MechMaster.Runtime
{
    [DefaultExecutionOrder(-100)]
    public sealed class MechMasterApp : MonoBehaviour
    {
        private const string BicycleResourceName = "BicyclePrototype";
        private GameObject modelInstance;
        private BikeModelView bikeModelView;
        private FeedbackAudio feedbackAudio;
        private VoiceNarrator narrator;

        public static MechMasterApp Instance { get; private set; }

        public DisassemblyPlan Plan { get; private set; }
        public ToolKind SelectedTool { get; private set; }
        public PartDefinition SelectedPart { get; private set; }
        public string StatusMessage { get; private set; }
        public bool NarrationEnabled => narrator != null && narrator.Enabled;

        public event Action StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null || FindObjectOfType<MechMasterApp>() != null)
            {
                return;
            }

            GameObject root = new GameObject("MechMasterApp");
            root.AddComponent<MechMasterApp>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            feedbackAudio = gameObject.AddComponent<FeedbackAudio>();
            narrator = gameObject.AddComponent<VoiceNarrator>();
            narrator.Enabled = LocalProgressStore.LoadNarrationEnabled();
            SelectedTool = LocalProgressStore.LoadTool();

            Camera sceneCamera = EnsureSceneEnvironment();
            PartInteractionController interaction = gameObject.AddComponent<PartInteractionController>();
            interaction.Initialize(sceneCamera);
            gameObject.AddComponent<PrototypeUI>();
            CreatePlan(LocalProgressStore.LoadDifficulty(), true);
        }

        public void SetDifficulty(DifficultyLevel difficulty)
        {
            if (Plan != null && Plan.Difficulty == difficulty)
            {
                return;
            }

            CreatePlan(difficulty, true);
        }

        public void SetTool(ToolKind tool)
        {
            SelectedTool = tool;
            StatusMessage = "已选择“" + DisassemblyPlan.ToolDisplayName(tool) + "”。";
            SaveAndNotify();
        }

        public void SelectPart(string partId)
        {
            if (Plan == null)
            {
                return;
            }

            foreach (PartDefinition part in Plan.Steps)
            {
                if (!string.Equals(part.Id, partId, StringComparison.Ordinal))
                {
                    continue;
                }

                SelectedPart = part;
                StatusMessage = "已选中“" + part.DisplayName + "”，拖动并松手即可操作。";
                narrator.Speak(part.DisplayName + "。" + part.GetKnowledge(Plan.Difficulty));
                NotifyStateChanged();
                return;
            }
        }

        public void Operate(string partId)
        {
            OperationResult result = Plan.TryOperate(partId, SelectedTool);
            StatusMessage = result.Message;
            if (result.Part != null)
            {
                SelectedPart = result.Part;
            }

            if (result.Succeeded)
            {
                feedbackAudio.PlaySuccess();
                bikeModelView.Refresh(Plan, false);
                narrator.Speak(result.Message);
            }
            else
            {
                feedbackAudio.PlayError();
            }

            SaveAndNotify();
        }

        public void ToggleMode()
        {
            if (Plan.Mode == AssemblyMode.Disassemble)
            {
                if (!Plan.IsDisassemblyComplete)
                {
                    StatusMessage = "请先完成拆解，再开始倒序组装。";
                    feedbackAudio.PlayError();
                    NotifyStateChanged();
                    return;
                }

                Plan.SetMode(AssemblyMode.Assemble);
                StatusMessage = "进入组装模式，请按拆解的相反顺序装回。";
            }
            else
            {
                if (!Plan.IsAssemblyComplete)
                {
                    StatusMessage = "请先完成本轮组装。";
                    feedbackAudio.PlayError();
                    NotifyStateChanged();
                    return;
                }

                Plan.SetMode(AssemblyMode.Disassemble);
                StatusMessage = "进入拆解模式。";
            }

            SaveAndNotify();
        }

        public void ResetCurrentPlan()
        {
            LocalProgressStore.ClearProgress(Plan.Difficulty);
            CreatePlan(Plan.Difficulty, false);
        }

        public void SetNarrationEnabled(bool enabled)
        {
            narrator.Enabled = enabled;
            StatusMessage = enabled ? "讲解提示已开启。" : "讲解提示已关闭。";
            SaveAndNotify();
        }

        public string GetProgressText()
        {
            if (Plan == null)
            {
                return "0 / 0";
            }

            return Plan.RemovedCount + " / " + Plan.Steps.Count;
        }

        private void CreatePlan(DifficultyLevel difficulty, bool restoreProgress)
        {
            Plan = FrontBrakeCatalog.CreatePlan(difficulty);
            if (restoreProgress)
            {
                RestoreProgress();
            }

            SelectedPart = Plan.ExpectedPart ?? Plan.Steps[0];
            StatusMessage = DifficultyDisplayName(difficulty)
                + "已就绪：选择工具，再拖动目标零件并松手。";
            LoadModel();
            SaveAndNotify();
        }

        private void RestoreProgress()
        {
            int removedCount = Mathf.Clamp(
                LocalProgressStore.LoadRemovedCount(Plan.Difficulty),
                0,
                Plan.Steps.Count);

            for (int index = 0; index < removedCount; index++)
            {
                PartDefinition part = Plan.Steps[index];
                Plan.TryOperate(part.Id, part.RequiredTool);
            }

            if (LocalProgressStore.LoadMode(Plan.Difficulty) == AssemblyMode.Assemble)
            {
                Plan.SetMode(AssemblyMode.Assemble);
            }
        }

        private void LoadModel()
        {
            if (modelInstance != null)
            {
                Destroy(modelInstance);
            }

            GameObject prefab = Resources.Load<GameObject>(BicycleResourceName);
            if (prefab == null)
            {
                StatusMessage = "未找到自行车模型 Assets/Resources/BicyclePrototype.fbx。";
                Debug.LogError(StatusMessage);
                return;
            }

            modelInstance = Instantiate(prefab);
            modelInstance.name = "BicyclePrototype_Runtime";
            modelInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelInstance.transform.localScale = Vector3.one;
            bikeModelView = modelInstance.AddComponent<BikeModelView>();
            bikeModelView.Bind(Plan);
            bikeModelView.Refresh(Plan, true);

            OrbitCameraController orbit = Camera.main.GetComponent<OrbitCameraController>();
            if (orbit != null)
            {
                Transform target = new GameObject("CameraTarget").transform;
                target.SetParent(modelInstance.transform, false);
                target.localPosition = new Vector3(0f, 0.52f, 0f);
                orbit.Initialize(target);
            }
        }

        private Camera EnsureSceneEnvironment()
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.018f, 0.027f, 0.043f, 1f);
            sceneCamera.nearClipPlane = 0.03f;
            sceneCamera.farClipPlane = 100f;
            sceneCamera.fieldOfView = 42f;
            if (sceneCamera.GetComponent<OrbitCameraController>() == null)
            {
                sceneCamera.gameObject.AddComponent<OrbitCameraController>();
            }

            if (FindObjectOfType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Key Light");
                Light lightComponent = lightObject.AddComponent<Light>();
                lightComponent.type = LightType.Directional;
                lightComponent.intensity = 1.15f;
                lightComponent.color = new Color(1f, 0.95f, 0.88f);
                lightObject.transform.rotation = Quaternion.Euler(42f, -34f, 0f);

                GameObject fillObject = new GameObject("Fill Light");
                Light fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.45f;
                fill.color = new Color(0.42f, 0.62f, 1f);
                fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.24f, 0.28f, 0.34f);
            return sceneCamera;
        }

        private void SaveAndNotify()
        {
            LocalProgressStore.Save(Plan, SelectedTool, NarrationEnabled);
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string DifficultyDisplayName(DifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case DifficultyLevel.Simple:
                    return "启蒙模式";
                case DifficultyLevel.Standard:
                    return "探索模式";
                default:
                    return "进阶模式";
            }
        }
    }
}

