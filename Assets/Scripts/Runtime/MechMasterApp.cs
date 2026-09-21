using System;
using System.Collections.Generic;
using MechMaster.Domain;
using MechMaster.Runtime.UI;
using UnityEngine;

namespace MechMaster.Runtime
{
    public enum ExplosionViewMode
    {
        None,
        Global,
        Local
    }

    [DefaultExecutionOrder(-100)]
    public sealed class MechMasterApp : MonoBehaviour
    {
        private GameObject modelInstance;
        private BikeModelView bikeModelView;
        private FeedbackAudio feedbackAudio;
        private VoiceNarrator narrator;

        public static MechMasterApp Instance { get; private set; }

        public DisassemblyPlan Plan { get; private set; }
        public PartDefinition SelectedPart { get; private set; }
        public string StatusMessage { get; private set; }
        public bool NarrationEnabled => narrator != null && narrator.Enabled;
        public bool NarrationAvailable => narrator != null && narrator.Available;
        public string NarrationStatus => narrator == null ? "语音初始化中" : narrator.Status;
        public ExplosionViewMode ExplosionMode { get; private set; }
        public bool IsGlobalExplosionActive =>
            ExplosionMode == ExplosionViewMode.Global
            && bikeModelView != null
            && bikeModelView.GlobalExplosionActive;
        public bool IsLocalExplosionMode => ExplosionMode == ExplosionViewMode.Local;

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
            if (Application.isEditor)
            {
                QualitySettings.antiAliasing = 8;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            }
            feedbackAudio = gameObject.AddComponent<FeedbackAudio>();
            narrator = gameObject.AddComponent<VoiceNarrator>();
            narrator.Enabled = LocalProgressStore.LoadNarrationEnabled();
            Camera sceneCamera = EnsureSceneEnvironment();
            PartInteractionController interaction = gameObject.AddComponent<PartInteractionController>();
            interaction.Initialize(sceneCamera);
            gameObject.AddComponent<PrototypeUI>();
            CreatePlan(LocalProgressStore.LoadDifficulty(), true);
            Debug.Log(
                "MECH_MASTER_RENDER_INFO screen=" + Screen.width + "x" + Screen.height
                + ", current=" + Screen.currentResolution.width + "x"
                + Screen.currentResolution.height
                + ", dpi=" + Screen.dpi
                + ", quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]
                + ", msaa=" + QualitySettings.antiAliasing);
        }

        public void SetDifficulty(DifficultyLevel difficulty)
        {
            if (Plan != null && Plan.Difficulty == difficulty)
            {
                return;
            }

            PartInteractionController.Instance?.CancelGesture();
            narrator.Stop();
            CreatePlan(difficulty, true);
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
                if (ExplosionMode == ExplosionViewMode.Local && bikeModelView != null)
                {
                    MechanicalPartView partView = bikeModelView.FindPart(part.Id);
                    if (partView != null && partView.IsRemoved)
                    {
                        StatusMessage = "“" + part.DisplayName
                            + "”已在收纳区，需装回后才能使用局部爆炸。";
                    }
                    else
                    {
                        bool exploded = bikeModelView.ToggleLocalExplosion(part.Id, false);
                        StatusMessage = exploded
                            ? "已将“" + part.DisplayName + "”向外展开；再次点击可收回。"
                            : "已收回“" + part.DisplayName + "”。";
                        Camera.main?.GetComponent<OrbitCameraController>()
                            ?.FrameContentsPreservingView(
                                bikeModelView.GetExplosionFramingPoints());
                    }
                }
                else
                {
                    StatusMessage = "已选中“" + part.DisplayName
                        + "”；可直接拖动，并按任意顺序拆装。";
                }
                feedbackAudio.PlayPickup();
                SpeakPartNarration(part);
                NotifyStateChanged();
                return;
            }
        }

        public void Operate(string partId)
        {
            ClearExplosionInternal(true);
            AssemblyMode operationMode = Plan.Mode;
            OperationResult result = Plan.TryOperate(partId);
            StatusMessage = result.Message;
            if (result.Part != null)
            {
                SelectedPart = result.Part;
            }

            if (result.Succeeded)
            {
                if (Plan.Mode == AssemblyMode.Disassemble)
                {
                    StatusMessage = result.Message + " 已放入“"
                        + BicycleAssemblyInfo.DisplayName(result.Part.AssemblyId)
                        + "”分类托盘。";
                }

                if (operationMode == AssemblyMode.Disassemble)
                {
                    feedbackAudio.PlayDisassembled();
                }
                else
                {
                    feedbackAudio.PlayAssembled();
                }
                bikeModelView.Refresh(Plan, false);
                SpeakNarration(StatusMessage);
            }
            else
            {
                feedbackAudio.PlayBlocked();
            }

            SaveAndNotify();
        }

        public void SetTrayHover(
            string assemblyId,
            bool correctAssembly,
            bool ready)
        {
            if (bikeModelView != null)
            {
                bikeModelView.SetTrayHighlight(assemblyId, correctAssembly, ready);
            }
        }

        public void RejectTrayDrop(string partId, string hoveredAssemblyId)
        {
            PartDefinition part = FindPart(partId);
            if (part == null)
            {
                return;
            }

            StatusMessage = string.IsNullOrEmpty(hoveredAssemblyId)
                ? "请把“" + part.DisplayName + "”拖到下方“"
                  + BicycleAssemblyInfo.DisplayName(part.AssemblyId) + "”分类槽位，变色后再松手。"
                : "这里是“" + BicycleAssemblyInfo.DisplayName(hoveredAssemblyId)
                  + "”槽位；“" + part.DisplayName + "”应放入“"
                  + BicycleAssemblyInfo.DisplayName(part.AssemblyId) + "”槽位。";
            feedbackAudio.PlayBlocked();
            NotifyStateChanged();
        }

        public void RejectAssemblyDrop(string partId)
        {
            PartDefinition part = FindPart(partId);
            if (part == null)
            {
                return;
            }

            StatusMessage = "组装时请把“" + part.DisplayName
                + "”从分类托盘拖回上方整车区域后再松手。";
            feedbackAudio.PlayBlocked();
            NotifyStateChanged();
        }

        public void ToggleMode()
        {
            if (Plan.Mode == AssemblyMode.Disassemble)
            {
                if (!Plan.IsDisassemblyComplete)
                {
                    StatusMessage = "请先完成拆解，再开始组装。";
                    feedbackAudio.PlayBlocked();
                    NotifyStateChanged();
                    return;
                }

                Plan.SetMode(AssemblyMode.Assemble);
                StatusMessage = "进入组装模式，可从托盘中任选零件装回。";
                FrameStorage();
            }
            else
            {
                if (!Plan.IsAssemblyComplete)
                {
                    StatusMessage = "请先完成本轮组装。";
                    feedbackAudio.PlayBlocked();
                    NotifyStateChanged();
                    return;
                }

                Plan.SetMode(AssemblyMode.Disassemble);
                StatusMessage = "进入拆解模式。";
                FrameWholeBike();
            }

            feedbackAudio.PlayModeSwitch();
            SaveAndNotify();
        }

        public void ResetCurrentPlan()
        {
            LocalProgressStore.ClearProgress(Plan.Difficulty);
            CreatePlan(Plan.Difficulty, false);
        }

        public void SetInteractionViewMode(bool viewMode)
        {
            if (!viewMode)
            {
                ClearExplosionInternal(true);
            }

            PartInteractionController.SetViewMode(viewMode);
            StatusMessage = viewMode
                ? "旋转视角：拖动观察，轻点零件查看讲解。"
                : "拆装零件：直接把机械单元拖入对应分类区。";
            NotifyStateChanged();
        }

        public void ToggleGlobalExplosion()
        {
            if (bikeModelView == null)
            {
                return;
            }

            PartInteractionController.Instance?.CancelGesture();
            if (IsGlobalExplosionActive)
            {
                ClearExplosionInternal(false);
                Camera.main?.GetComponent<OrbitCameraController>()?.FrameWholeBike();
                StatusMessage = "全局爆炸视图已收回。";
            }
            else
            {
                ClearExplosionInternal(true);
                ExplosionMode = ExplosionViewMode.Global;
                PartInteractionController.SetViewMode(true);
                bikeModelView.SetGlobalExplosion(true, false);
                if (bikeModelView.ExplosionTargetCount == 0)
                {
                    ExplosionMode = ExplosionViewMode.None;
                    StatusMessage = "当前没有留在整车上的机械单元可供爆炸查看。";
                }
                else
                {
                    Camera.main?.GetComponent<OrbitCameraController>()?.FrameContents(
                        bikeModelView.GetExplosionFramingPoints());
                    StatusMessage = "全局爆炸视图：全部机械单元已向外展开。";
                }
            }

            feedbackAudio.PlayModeSwitch();
            NotifyStateChanged();
        }

        public void ToggleLocalExplosionMode()
        {
            if (bikeModelView == null)
            {
                return;
            }

            PartInteractionController.Instance?.CancelGesture();
            if (ExplosionMode == ExplosionViewMode.Local)
            {
                ClearExplosionInternal(false);
                StatusMessage = "局部爆炸模式已关闭。";
            }
            else
            {
                ClearExplosionInternal(true);
                ExplosionMode = ExplosionViewMode.Local;
                PartInteractionController.SetViewMode(true);
                StatusMessage = "局部爆炸模式：轻点零件展开，再次点击收回；拖动可旋转视角。";
            }

            feedbackAudio.PlayModeSwitch();
            NotifyStateChanged();
        }

        public void SetNarrationEnabled(bool enabled)
        {
            narrator.Enabled = enabled;
            StatusMessage = enabled ? (narrator.Available ? "中文讲解已开启。" : narrator.Status) : "中文讲解已关闭。";
            if (enabled) ReplayNarration();
            SaveAndNotify();
        }

        public void ReplayNarration()
        {
            if (SelectedPart != null)
                SpeakPartNarration(SelectedPart);
        }

        public void FrameWholeBike()
        {
            PartInteractionController.Instance?.CancelGesture();
            bool hadExplosion = ExplosionMode != ExplosionViewMode.None;
            ClearExplosionInternal(true);
            Camera.main?.GetComponent<OrbitCameraController>()?.FrameWholeBike();
            if (hadExplosion)
            {
                StatusMessage = "爆炸视图已收回，整车已归位。";
                NotifyStateChanged();
            }
        }

        public void FrameStorage()
        {
            PartInteractionController.Instance?.CancelGesture();
            bool hadExplosion = ExplosionMode != ExplosionViewMode.None;
            ClearExplosionInternal(true);
            if (modelInstance == null) return;
            var points = new List<Vector3>();
            foreach (Renderer renderer in modelInstance.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                    points.Add(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            }
            Camera.main?.GetComponent<OrbitCameraController>()?.FrameContents(points.ToArray());
            if (hadExplosion)
            {
                StatusMessage = "爆炸视图已收回，正在查看零件收纳区。";
                NotifyStateChanged();
            }
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
            ExplosionMode = ExplosionViewMode.None;
            Plan = EngineeringBicycleCatalogLoader.CreatePlan(difficulty);
            if (restoreProgress)
            {
                RestoreProgress();
            }

            SelectedPart = Plan.ExpectedPart ?? Plan.Steps[0];
            StatusMessage = DifficultyDisplayName(difficulty)
                + "已就绪：零件可自由选择，直接拖入分类托盘即可拆下。";
            LoadModel();
            SaveAndNotify();
        }

        private void RestoreProgress()
        {
            string[] savedPartIds = LocalProgressStore.LoadRemovedPartIds(Plan.Difficulty);
            int restoredParts = 0;
            if (savedPartIds.Length > 0)
            {
                foreach (string partId in savedPartIds)
                {
                    PartDefinition part = FindPart(partId);
                    if (part != null)
                    {
                        Plan.TryOperate(part.Id);
                        restoredParts++;
                    }
                }

                // Plan IDs changed when the old 14/195/338 plans were regrouped.
                // Ignore that stale progress instead of restoring assembly mode
                // with an empty new plan.
                if (restoredParts == 0)
                {
                    LocalProgressStore.ClearProgress(Plan.Difficulty);
                }
            }
            else
            {
                RestoreLegacyProgressCount();
            }

            if (Plan.RemovedCount > 0
                && LocalProgressStore.LoadMode(Plan.Difficulty) == AssemblyMode.Assemble)
            {
                Plan.SetMode(AssemblyMode.Assemble);
            }
        }

        private void RestoreLegacyProgressCount()
        {
            int removedCount = Mathf.Clamp(
                LocalProgressStore.LoadRemovedCount(Plan.Difficulty),
                0,
                Plan.Steps.Count);

            for (int index = 0; index < removedCount; index++)
            {
                PartDefinition part = Plan.Steps[index];
                Plan.TryOperate(part.Id);
            }
        }

        private void LoadModel()
        {
            if (modelInstance != null)
            {
                Destroy(modelInstance);
            }

            modelInstance = new GameObject("BicycleEngineering_Runtime");
            foreach (string resourcePath in EngineeringBicycleCatalogLoader.ModuleResourcePaths)
            {
                GameObject prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                {
                    StatusMessage = "未找到工程自行车模块：" + resourcePath;
                    Debug.LogError(StatusMessage);
                    Destroy(modelInstance);
                    modelInstance = null;
                    return;
                }

                GameObject module = Instantiate(prefab, modelInstance.transform, false);
                module.name = prefab.name;
            }
            modelInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelInstance.transform.localScale = Vector3.one;
            Renderer[] modelRenderers = modelInstance.GetComponentsInChildren<Renderer>();
            Bounds modelBounds = modelRenderers.Length == 0
                ? new Bounds(new Vector3(0, 0.55f, 0), new Vector3(2f, 1.1f, 0.7f))
                : modelRenderers[0].bounds;
            var framingPoints = new List<Vector3>(modelRenderers.Length * 8);
            foreach (Renderer renderer in modelRenderers)
            {
                Bounds bounds = renderer.bounds;
                modelBounds.Encapsulate(bounds);
                for (int i = 0; i < 8; i++)
                    framingPoints.Add(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            }
            bikeModelView = modelInstance.AddComponent<BikeModelView>();
            bikeModelView.Bind(Plan);
            bikeModelView.Refresh(Plan, true);

            OrbitCameraController orbit = Camera.main.GetComponent<OrbitCameraController>();
            if (orbit != null)
            {
                Transform target = new GameObject("CameraTarget").transform;
                target.SetParent(modelInstance.transform, false);
                target.position = modelBounds.center;
                orbit.Initialize(target, modelBounds, framingPoints.ToArray());
                if (Plan.Mode == AssemblyMode.Assemble) FrameStorage();
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
            sceneCamera.backgroundColor = WorkshopTheme.Background;
            sceneCamera.nearClipPlane = 0.03f;
            sceneCamera.farClipPlane = 100f;
            sceneCamera.fieldOfView = 42f;
            sceneCamera.allowHDR = true;
            sceneCamera.allowMSAA = true;
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
            LocalProgressStore.Save(Plan, NarrationEnabled);
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void ClearExplosionInternal(bool immediate)
        {
            bikeModelView?.ClearInspectionExplosion(immediate);
            ExplosionMode = ExplosionViewMode.None;
        }

        private PartDefinition FindPart(string partId)
        {
            if (Plan == null)
            {
                return null;
            }

            foreach (PartDefinition part in Plan.Steps)
            {
                if (string.Equals(part.Id, partId, StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static string DifficultyDisplayName(DifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case DifficultyLevel.Simple:
                    return "简单模式";
                case DifficultyLevel.Standard:
                    return "进阶模式";
                default:
                    return "探索模式";
            }
        }

        private void SpeakPartNarration(PartDefinition part)
        {
            SpeakNarration(part.DisplayName + "。" + part.GetKnowledge(Plan.Difficulty));
        }

        private void SpeakNarration(string text)
        {
            narrator.Speak(CompactNarration(text));
        }

        private static string CompactNarration(string text)
        {
            string value = (text ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            const int maxCharacters = 50;
            if (value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters - 1).TrimEnd() + "…";
        }
    }
}
