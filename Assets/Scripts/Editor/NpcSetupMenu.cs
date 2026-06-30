using System.Collections.Generic;
using Container;
using NPC;
using Player;
using StateMachine.Behaviours;
using StateMachine.Conditions;
using StateMachine.Graph;
using StateMachine.Graph.Model;
using TargetLock;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

namespace EditorTools
{
    public static class NpcSetupMenu
    {
        private const string MenuPath = "Tools/RPG/NPC/Create Base NPC Assets";
        private const string SetupNavMeshMenuPath = "Tools/RPG/NPC/Setup NavMesh For Active Scene";
        private const string StateMachineFolderPath = "Assets/StateMachines/NPC";
        private const string StatesFolderPath = StateMachineFolderPath + "/States";
        private const string TransitionsFolderPath = StateMachineFolderPath + "/Transitions";
        private const string ConditionsFolderPath = StateMachineFolderPath + "/Conditions";
        private const string BehavioursFolderPath = StateMachineFolderPath + "/Behaviours";
        private const string NpcConfigFolderPath = "Assets/Configs/NPC";
        private const string GraphPath = "Assets/StateMachines/NpcBaseStateMachineGraph.asset";
        private const string IdleStatePath = StatesFolderPath + "/NpcIdleState.asset";
        private const string DeathStatePath = StatesFolderPath + "/NpcDeathState.asset";
        private const string MoveToItemStatePath = StatesFolderPath + "/NpcMoveToInterestedItemState.asset";
        private const string PickupWaitStatePath = StatesFolderPath + "/NpcPickupItemWaitState.asset";
        private const string PickupStatePath = StatesFolderPath + "/NpcPickupItemState.asset";
        private const string ReturnHomeStatePath = StatesFolderPath + "/NpcReturnHomeState.asset";
        private const string DeathTransitionPath = TransitionsFolderPath + "/NpcIdleToDeathTransition.asset";
        private const string IdleToMoveTransitionPath = TransitionsFolderPath + "/NpcIdleToMoveToItemTransition.asset";
        private const string MoveToWaitTransitionPath = TransitionsFolderPath + "/NpcMoveToItemToPickupWaitTransition.asset";
        private const string WaitToPickupTransitionPath = TransitionsFolderPath + "/NpcPickupWaitToPickupTransition.asset";
        private const string PickupToReturnTransitionPath = TransitionsFolderPath + "/NpcPickupToReturnHomeTransition.asset";
        private const string PickupToMoveTransitionPath = TransitionsFolderPath + "/NpcPickupToMoveToItemTransition.asset";
        private const string ReturnToIdleTransitionPath = TransitionsFolderPath + "/NpcReturnHomeToIdleTransition.asset";
        private const string MissingItemToReturnTransitionPath = TransitionsFolderPath + "/NpcMissingItemToReturnHomeTransition.asset";
        private const string MissingItemToNextPickupTransitionPath = TransitionsFolderPath + "/NpcMissingItemToNextPickupTransition.asset";
        private const string LostCompetitionToReturnTransitionPath = TransitionsFolderPath + "/NpcLostCompetitionToReturnHomeTransition.asset";
        private const string HpDepletedConditionPath = ConditionsFolderPath + "/NpcHpDepletedCondition.asset";
        private const string HasInterestingItemConditionPath = ConditionsFolderPath + "/NpcHasInterestingItemCondition.asset";
        private const string ReachedInterestedItemConditionPath = ConditionsFolderPath + "/NpcReachedInterestedItemCondition.asset";
        private const string PickupWaitElapsedConditionPath = ConditionsFolderPath + "/NpcPickupWaitElapsedCondition.asset";
        private const string InterestedItemMissingConditionPath = ConditionsFolderPath + "/NpcInterestedItemMissingCondition.asset";
        private const string SwitchToNextPickupConditionPath = ConditionsFolderPath + "/NpcSwitchToNextPickupCondition.asset";
        private const string LostCompetitionConditionPath = ConditionsFolderPath + "/NpcLostItemCompetitionCondition.asset";
        private const string PickupCompletedConditionPath = ConditionsFolderPath + "/NpcPickupCompletedCondition.asset";
        private const string ChainPickupFoundConditionPath = ConditionsFolderPath + "/NpcChainPickupFoundCondition.asset";
        private const string ReachedHomeConditionPath = ConditionsFolderPath + "/NpcReachedHomeCondition.asset";
        private const string DeathBehaviourPath = BehavioursFolderPath + "/NpcDeathBehaviour.asset";
        private const string ScanVisibleItemsBehaviourPath = BehavioursFolderPath + "/NpcScanVisibleItemsBehaviour.asset";
        private const string MoveToItemBehaviourPath = BehavioursFolderPath + "/NpcMoveToInterestedItemBehaviour.asset";
        private const string WaitPickupBehaviourPath = BehavioursFolderPath + "/NpcWaitPickupBehaviour.asset";
        private const string PickupInterestedItemBehaviourPath = BehavioursFolderPath + "/NpcPickupInterestedItemBehaviour.asset";
        private const string ReturnHomeBehaviourPath = BehavioursFolderPath + "/NpcReturnHomeBehaviour.asset";
        private const string NpcVisionConfigPath = NpcConfigFolderPath + "/NpcVisionConfig.asset";
        private const string NpcItemPickupConfigPath = NpcConfigFolderPath + "/NpcItemPickupConfig.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Scopes/Player.prefab";
        private const string NpcPrefabPath = "Assets/Prefabs/Scopes/NPC.prefab";
        private const string ProjectLifetimeScopePrefabPath = "Assets/Resources/Project/ProjectLifetimeScope.prefab";

        [MenuItem(MenuPath)]
        public static void CreateBaseNpcAssets()
        {
            EnsureFolder(StateMachineFolderPath);
            EnsureFolder(StatesFolderPath);
            EnsureFolder(TransitionsFolderPath);
            EnsureFolder(ConditionsFolderPath);
            EnsureFolder(BehavioursFolderPath);
            EnsureFolder(NpcConfigFolderPath);

            var idleState = LoadOrCreate<State>(IdleStatePath);
            var deathState = LoadOrCreate<State>(DeathStatePath);
            var moveToItemState = LoadOrCreate<State>(MoveToItemStatePath);
            var pickupWaitState = LoadOrCreate<State>(PickupWaitStatePath);
            var pickupState = LoadOrCreate<State>(PickupStatePath);
            var returnHomeState = LoadOrCreate<State>(ReturnHomeStatePath);
            var deathTransition = LoadOrCreate<Transition>(DeathTransitionPath);
            var idleToMoveTransition = LoadOrCreate<Transition>(IdleToMoveTransitionPath);
            var moveToWaitTransition = LoadOrCreate<Transition>(MoveToWaitTransitionPath);
            var waitToPickupTransition = LoadOrCreate<Transition>(WaitToPickupTransitionPath);
            var pickupToReturnTransition = LoadOrCreate<Transition>(PickupToReturnTransitionPath);
            var pickupToMoveTransition = LoadOrCreate<Transition>(PickupToMoveTransitionPath);
            var returnToIdleTransition = LoadOrCreate<Transition>(ReturnToIdleTransitionPath);
            var missingItemToReturnTransition = LoadOrCreate<Transition>(MissingItemToReturnTransitionPath);
            var missingItemToNextPickupTransition = LoadOrCreate<Transition>(MissingItemToNextPickupTransitionPath);
            var lostCompetitionToReturnTransition = LoadOrCreate<Transition>(LostCompetitionToReturnTransitionPath);
            var hpDepletedCondition = LoadOrCreate<HpDepletedCondition>(HpDepletedConditionPath);
            var hasInterestingItemCondition = LoadOrCreate<NpcHasInterestingItemCondition>(HasInterestingItemConditionPath);
            var reachedInterestedItemCondition = LoadOrCreate<NpcReachedInterestedItemCondition>(ReachedInterestedItemConditionPath);
            var pickupWaitElapsedCondition = LoadOrCreate<NpcPickupWaitElapsedCondition>(PickupWaitElapsedConditionPath);
            var interestedItemMissingCondition = LoadOrCreate<NpcInterestedItemMissingCondition>(InterestedItemMissingConditionPath);
            var switchToNextPickupCondition = LoadOrCreate<NpcSwitchToNextPickupCondition>(SwitchToNextPickupConditionPath);
            var lostCompetitionCondition = LoadOrCreate<NpcLostItemCompetitionCondition>(LostCompetitionConditionPath);
            var pickupCompletedCondition = LoadOrCreate<NpcPickupCompletedCondition>(PickupCompletedConditionPath);
            var chainPickupFoundCondition = LoadOrCreate<NpcChainPickupFoundCondition>(ChainPickupFoundConditionPath);
            var reachedHomeCondition = LoadOrCreate<NpcReachedHomeCondition>(ReachedHomeConditionPath);
            var deathBehaviour = LoadOrCreate<NpcDeathBehaviour>(DeathBehaviourPath);
            var scanVisibleItemsBehaviour = LoadOrCreate<NpcScanVisibleItemsBehaviour>(ScanVisibleItemsBehaviourPath);
            var moveToItemBehaviour = LoadOrCreate<NpcMoveToInterestedItemBehaviour>(MoveToItemBehaviourPath);
            var waitPickupBehaviour = LoadOrCreate<NpcWaitPickupBehaviour>(WaitPickupBehaviourPath);
            var pickupInterestedItemBehaviour = LoadOrCreate<NpcPickupInterestedItemBehaviour>(PickupInterestedItemBehaviourPath);
            var returnHomeBehaviour = LoadOrCreate<NpcReturnHomeBehaviour>(ReturnHomeBehaviourPath);
            var visionConfig = LoadOrCreate<NpcVisionConfig>(NpcVisionConfigPath);
            var itemPickupConfig = LoadOrCreate<NpcItemPickupConfig>(NpcItemPickupConfigPath);
            var graph = LoadOrCreate<StateMachineGraph>(GraphPath);

            ConfigureTransition(deathTransition, deathState, hpDepletedCondition);
            ConfigureTransition(idleToMoveTransition, moveToItemState, hasInterestingItemCondition);
            ConfigureTransition(moveToWaitTransition, pickupWaitState, reachedInterestedItemCondition);
            ConfigureTransition(waitToPickupTransition, pickupState, pickupWaitElapsedCondition);
            ConfigureTransition(pickupToMoveTransition, moveToItemState, chainPickupFoundCondition);
            ConfigureTransition(pickupToReturnTransition, returnHomeState, pickupCompletedCondition);
            ConfigureTransition(returnToIdleTransition, idleState, reachedHomeCondition);
            ConfigureTransition(missingItemToNextPickupTransition, moveToItemState, switchToNextPickupCondition);
            ConfigureTransition(missingItemToReturnTransition, returnHomeState, interestedItemMissingCondition);
            ConfigureTransition(lostCompetitionToReturnTransition, returnHomeState, lostCompetitionCondition);

            deathState.Behaviours = new List<BaseBehaviour> { deathBehaviour };
            deathState.Transitions = new List<Transition>();
            EditorUtility.SetDirty(deathState);

            idleState.Behaviours = new List<BaseBehaviour> { scanVisibleItemsBehaviour };
            idleState.Transitions = new List<Transition> { deathTransition, idleToMoveTransition };
            EditorUtility.SetDirty(idleState);

            moveToItemState.Behaviours = new List<BaseBehaviour> { moveToItemBehaviour };
            moveToItemState.Transitions = new List<Transition>
            {
                deathTransition,
                missingItemToNextPickupTransition,
                missingItemToReturnTransition,
                lostCompetitionToReturnTransition,
                moveToWaitTransition
            };
            EditorUtility.SetDirty(moveToItemState);

            pickupWaitState.Behaviours = new List<BaseBehaviour> { waitPickupBehaviour };
            pickupWaitState.Transitions = new List<Transition>
            {
                deathTransition,
                missingItemToNextPickupTransition,
                missingItemToReturnTransition,
                lostCompetitionToReturnTransition,
                waitToPickupTransition
            };
            EditorUtility.SetDirty(pickupWaitState);

            pickupState.Behaviours = new List<BaseBehaviour> { pickupInterestedItemBehaviour };
            pickupState.Transitions = new List<Transition> { deathTransition, pickupToMoveTransition, pickupToReturnTransition };
            EditorUtility.SetDirty(pickupState);

            returnHomeState.Behaviours = new List<BaseBehaviour> { returnHomeBehaviour };
            returnHomeState.Transitions = new List<Transition> { deathTransition, returnToIdleTransition };
            EditorUtility.SetDirty(returnHomeState);

            graph.Nodes = new List<Node>
            {
                new(idleState) { Position = new Vector2(160f, 180f) },
                new(moveToItemState) { Position = new Vector2(520f, 80f) },
                new(pickupWaitState) { Position = new Vector2(860f, 80f) },
                new(pickupState) { Position = new Vector2(1200f, 80f) },
                new(returnHomeState) { Position = new Vector2(860f, 330f) },
                new(deathState) { Position = new Vector2(520f, 520f) }
            };
            EditorUtility.SetDirty(graph);

            CreateOrUpdateNpcPrefab(graph, visionConfig);
            AssignProjectNpcConfigs(visionConfig, itemPickupConfig);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created base NPC assets: {GraphPath}, {NpcPrefabPath}");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureTransition(Transition transition, State targetState, BaseCondition condition)
        {
            transition.Type = TransitionType.All;
            transition.Conditions.Clear();
            transition.Conditions.Add(condition);
            transition.ActionOnTransitions.Clear();
            transition.TargetState = targetState;
            EditorUtility.SetDirty(transition);
        }

        private static void CreateOrUpdateNpcPrefab(StateMachineGraph graph, NpcVisionConfig visionConfig)
        {
            var sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath) != null
                ? NpcPrefabPath
                : PlayerPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            if (root == null)
            {
                Debug.LogError($"Cannot load prefab source: {sourcePath}");
                return;
            }

            try
            {
                root.name = "NPC";

                var characterInfo = default(Object);
                var inventoryConfig = default(Object);
                var playerScope = root.GetComponent<PlayerLifetimeScope>();
                if (playerScope != null)
                {
                    var playerScopeObject = new SerializedObject(playerScope);
                    characterInfo = playerScopeObject.FindProperty("characterInfo")?.objectReferenceValue;
                    inventoryConfig = playerScopeObject.FindProperty("inventoryConfig")?.objectReferenceValue;
                    Object.DestroyImmediate(playerScope, true);
                }

                var npcScope = root.GetComponent<NpcLifetimeScope>();
                if (npcScope == null)
                {
                    npcScope = root.AddComponent<NpcLifetimeScope>();
                }

                var npcScopeObject = new SerializedObject(npcScope);
                var characterInfoProperty = npcScopeObject.FindProperty("characterInfo");
                if (characterInfoProperty != null && characterInfo != null)
                {
                    characterInfoProperty.objectReferenceValue = characterInfo;
                }

                var inventoryConfigProperty = npcScopeObject.FindProperty("inventoryConfig");
                if (inventoryConfigProperty != null && inventoryConfig != null)
                {
                    inventoryConfigProperty.objectReferenceValue = inventoryConfig;
                }

                var stateMachineProperty = npcScopeObject.FindProperty("stateMachineGraph");
                if (stateMachineProperty != null)
                {
                    stateMachineProperty.objectReferenceValue = graph;
                }

                var parentReferenceTypeNameProperty = npcScopeObject.FindProperty("parentReference.TypeName");
                if (parentReferenceTypeNameProperty != null)
                {
                    parentReferenceTypeNameProperty.stringValue = "Container.Game.GameLifetimeScope";
                }

                npcScopeObject.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<PlayerRagdollController>() == null)
                {
                    root.AddComponent<PlayerRagdollController>();
                }

                if (root.GetComponent<TargetLockTarget>() == null)
                {
                    root.AddComponent<TargetLockTarget>();
                }

                var npcVision = root.GetComponent<NpcVision>();
                if (npcVision == null)
                {
                    npcVision = root.AddComponent<NpcVision>();
                }

                var npcVisionObject = new SerializedObject(npcVision);
                var configProperty = npcVisionObject.FindProperty("config");
                if (configProperty != null)
                {
                    configProperty.objectReferenceValue = visionConfig;
                }

                npcVisionObject.ApplyModifiedPropertiesWithoutUndo();

                var npcVisionSensor = root.GetComponent<NpcVisionSensor>();
                if (npcVisionSensor == null)
                {
                    npcVisionSensor = root.AddComponent<NpcVisionSensor>();
                }

                var npcVisionSensorObject = new SerializedObject(npcVisionSensor);
                var sensorConfigProperty = npcVisionSensorObject.FindProperty("config");
                if (sensorConfigProperty != null)
                {
                    sensorConfigProperty.objectReferenceValue = visionConfig;
                }

                npcVisionSensorObject.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<NpcItemInterest>() == null)
                {
                    root.AddComponent<NpcItemInterest>();
                }

                var navMeshAgent = root.GetComponent<NavMeshAgent>();
                if (navMeshAgent == null)
                {
                    navMeshAgent = root.AddComponent<NavMeshAgent>();
                }

                ConfigureNpcNavMeshAgent(navMeshAgent);

                MoveComponentAfterTransform(npcScope);

                PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignProjectNpcConfigs(NpcVisionConfig visionConfig, NpcItemPickupConfig itemPickupConfig)
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectLifetimeScopePrefabPath);
            if (root == null)
            {
                Debug.LogError($"Cannot load prefab source: {ProjectLifetimeScopePrefabPath}");
                return;
            }

            try
            {
                var projectScope = root.GetComponent<Container.Project.ProjectLifetimeScope>();
                if (projectScope == null)
                {
                    Debug.LogError($"{ProjectLifetimeScopePrefabPath} has no ProjectLifetimeScope component.");
                    return;
                }

                var projectScopeObject = new SerializedObject(projectScope);
                var visionConfigProperty = projectScopeObject.FindProperty("<NpcVisionConfig>k__BackingField");
                if (visionConfigProperty != null)
                {
                    visionConfigProperty.objectReferenceValue = visionConfig;
                }

                var itemPickupConfigProperty = projectScopeObject.FindProperty("<NpcItemPickupConfig>k__BackingField");
                if (itemPickupConfigProperty != null)
                {
                    itemPickupConfigProperty.objectReferenceValue = itemPickupConfig;
                }

                projectScopeObject.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ProjectLifetimeScopePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(SetupNavMeshMenuPath)]
        public static void SetupNavMeshForActiveScene()
        {
            var navigationRoot = GameObject.Find("Navigation");
            if (navigationRoot == null)
            {
                navigationRoot = new GameObject("Navigation");
            }

            var surface = navigationRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = navigationRoot.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.All;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.BuildNavMesh();

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(navigationRoot.scene);
            EditorSceneManager.SaveScene(navigationRoot.scene);
            Debug.Log($"Built NavMesh for active scene: {navigationRoot.scene.path}");
        }

        private static void ConfigureNpcNavMeshAgent(NavMeshAgent agent)
        {
            agent.radius = 0.25f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.15f;
            agent.autoBraking = true;
        }

        private static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void MoveComponentAfterTransform(Component component)
        {
            if (component == null)
            {
                return;
            }

            var components = component.GetComponents<Component>();
            for (var index = 1; index < components.Length; index++)
            {
                if (components[index] == component)
                {
                    for (var move = index; move > 1; move--)
                    {
                        UnityEditorInternal.ComponentUtility.MoveComponentUp(component);
                    }

                    return;
                }
            }
        }
    }
}
