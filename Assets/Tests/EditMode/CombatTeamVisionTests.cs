using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class CombatTeamVisionTests
{
    [Test]
    public void CombatVision_PreparedShareDoesNotRelayNewerMemoryFromTheSamePhase()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject senderGo = new GameObject("Sender");
        GameObject receiverGo = new GameObject("Receiver");
        GameObject enemyGo = new GameObject("Enemy");
        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character sender = senderGo.AddComponent<Character>();
            Character receiver = receiverGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            system.SetParticipants(new[] { sender, receiver }, new[] { enemy });
            sender.Vision.Initialize();
            receiver.Vision.Initialize();
            Vector3 preparedPosition = new Vector3(2f, 0f, 3f);
            Vector3 newerPosition = new Vector3(8f, 0f, 9f);

            sender.Vision.ReceiveSharedMemory(
                receiver,
                new List<CharacterMemory>
                {
                    new CharacterMemory(enemy, preparedPosition, Time.time - 0.5f),
                });
            sender.Vision.PrepareVisionShare();
            sender.Vision.ReceiveSharedMemory(
                receiver,
                new List<CharacterMemory>
                {
                    new CharacterMemory(enemy, newerPosition, Time.time),
                });

            sender.Vision.ShareVision();

            Assert.That(
                receiver.Vision.TryGetLastKnownPosition(enemy, out Vector3 receivedPosition),
                Is.True);
            Assert.That(receivedPosition, Is.EqualTo(preparedPosition));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(receiverGo);
            Object.DestroyImmediate(senderGo);
            Object.DestroyImmediate(systemGo);
        }
    }

    [Test]
    public void CharacterSystem_AssignsTeamsFromLists()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();

            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);

            system.AssignTeamsFromLists();

            Assert.That(ally.Team, Is.EqualTo(CombatTeam.Ally));
            Assert.That(enemy.Team, Is.EqualTo(CombatTeam.Enemy));
            Assert.That(system.GetEnemiesOf(ally), Does.Contain(enemy));
            Assert.That(system.GetEnemiesOf(enemy), Does.Contain(ally));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatVision_ReceiveSharedMemoryUpdatesLastKnownPosition()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject observerGo = new GameObject("Observer");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character observer = observerGo.AddComponent<Character>();
            ally.SetTeam(CombatTeam.Ally);
            observer.SetTeam(CombatTeam.Ally);
            system.AllyCharacters.Add(ally);
            system.AllyCharacters.Add(observer);
            system.AssignTeamsFromLists();

            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision allyVision = ally.Vision;
            allyVision.Initialize();

            Vector3 reportedPosition = new Vector3(3f, 0f, 4f);
            allyVision.ReceiveSharedMemory(
                observer,
                new List<CharacterMemory>
                {
                    new CharacterMemory(enemy, reportedPosition, Time.time),
                });

            Assert.That(allyVision.TryGetLastKnownPosition(enemy, out Vector3 lastKnownPosition), Is.True);
            Assert.That(lastKnownPosition, Is.EqualTo(reportedPosition));
            Assert.That(allyVision.IsVisible(enemy), Is.False);
            Assert.That(allyVision.HasMemoryOf(enemy), Is.True);
            Assert.That(allyVision.RememberedEnemies, Does.Contain(enemy));
            IReadOnlyList<CombatVisionDebugMemorySnapshot> snapshots = allyVision.GetDebugMemorySnapshots();
            Assert.That(ContainsSharedMemorySnapshot(snapshots, enemy, observer), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(observerGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatVision_TracksVisibleEnemiesAndLastKnownPosition()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            var allyCollider = allyGo.AddComponent<CapsuleCollider>();
            allyCollider.center = new Vector3(0f, 1f, 0f);
            allyCollider.height = 2f;
            var enemyCollider = enemyGo.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1f, 0f);
            enemyCollider.height = 2f;
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision vision = ally.Vision;
            vision.Initialize();
            vision.UpdateVision();

            Assert.That(vision.IsVisible(enemy), Is.True);
            Assert.That(vision.VisibleEnemies, Does.Contain(enemy));
            Assert.That(vision.TryGetLastKnownPosition(enemy, out Vector3 lastKnownPosition), Is.True);
            Assert.That(lastKnownPosition, Is.EqualTo(enemyGo.transform.position));
            Assert.That(vision.HasRecognitionOf(enemy), Is.True);
            Assert.That(enemy.Vision.IsRecognizedBy(ally), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatVision_VisionObstacleLayerBlocksLineOfSight()
    {
        GameObject observerGo = new GameObject("Observer");
        GameObject targetGo = new GameObject("Target");
        GameObject treeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            Character observer = observerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            observerGo.transform.position = Vector3.zero;
            targetGo.transform.position = new Vector3(0f, 0f, 5f);
            treeGo.transform.position = new Vector3(0f, 1f, 2.5f);
            treeGo.transform.localScale = new Vector3(1f, 2f, 1f);
            treeGo.layer = LayerMask.NameToLayer("VisionObstacle");
            Physics.SyncTransforms();

            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.False);
            Assert.That(observer.Vision.TryGetSightRay(target.transform, out _, out Vector3 end, out bool blocked), Is.True);
            Assert.That(blocked, Is.True);
            Assert.That(end.z, Is.LessThan(targetGo.transform.position.z));
        }
        finally
        {
            Object.DestroyImmediate(observerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(treeGo);
        }
    }

    [Test]
    public void CombatVision_SightRangeScalesFrom30To100WithTerrainHeight()
    {
        GameObject mapSystemGo = new GameObject("CombatMapSystem");
        GameObject observerGo = new GameObject("Observer");
        GameObject targetGo = new GameObject("Target");

        try
        {
            CombatMapSystem mapSystem = mapSystemGo.AddComponent<CombatMapSystem>();
            var heightMap = new HeightMap(2, 2, 1f);
            heightMap.SetHeight(1, 0, 10f);
            heightMap.SetHeight(1, 1, 10f);
            mapSystem.SetCurrentMap(new MapData(heightMap, new GroundStateGrid(2, 2, 1f), 1));

            Character observer = observerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            observerGo.transform.position = Vector3.zero;
            targetGo.transform.position = new Vector3(0f, 0f, 30f);
            Physics.SyncTransforms();

            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.True);

            targetGo.transform.position = new Vector3(0f, 0f, 30.1f);
            Physics.SyncTransforms();
            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.False);
            Assert.That(observer.Vision.TryGetSightRay(target.transform, out _, out Vector3 rangeEnd, out bool blocked), Is.True);
            Assert.That(blocked, Is.False);
            Assert.That(rangeEnd.z, Is.EqualTo(30f).Within(0.001f));

            observerGo.transform.position = new Vector3(1f, 10f, 0f);
            targetGo.transform.position = new Vector3(1f, 10f, 100f);
            Physics.SyncTransforms();
            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.True);

            targetGo.transform.position = new Vector3(1f, 10f, 100.1f);
            Physics.SyncTransforms();
            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(mapSystemGo);
            Object.DestroyImmediate(observerGo);
            Object.DestroyImmediate(targetGo);
        }
    }

    [Test]
    public void CombatVision_RejectsTargetBehindObserver()
    {
        GameObject observerGo = new GameObject("Observer");
        GameObject targetGo = new GameObject("Target");

        try
        {
            Character observer = observerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            observerGo.transform.position = Vector3.zero;
            observerGo.transform.rotation = Quaternion.identity;
            targetGo.transform.position = new Vector3(0f, 0f, -5f);

            Assert.That(observer.Vision.IsWithinFieldOfView(target.transform), Is.False);
            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(observerGo);
            Object.DestroyImmediate(targetGo);
        }
    }

    [Test]
    public void CombatVision_UsesRenderedTerrainHeightOutsidePlayMode()
    {
        var terrainData = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(10f, 10f, 10f),
        };
        var heights = new float[33, 33];
        heights[0, 32] = 1f;
        terrainData.SetHeights(0, 0, heights);

        GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
        GameObject mapSystemGo = new GameObject("CombatMapSystem");
        GameObject observerGo = new GameObject("Observer");
        GameObject targetGo = new GameObject("Target");

        try
        {
            mapSystemGo.AddComponent<CombatMapSystem>();
            Character observer = observerGo.AddComponent<Character>();
            Character target = targetGo.AddComponent<Character>();
            observerGo.transform.position = new Vector3(10f, 10f, 0f);
            targetGo.transform.position = new Vector3(10f, 10f, 100f);
            Physics.SyncTransforms();

            Assert.That(observer.Vision.HasLineOfSight(target.transform), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(terrainGo);
            Object.DestroyImmediate(mapSystemGo);
            Object.DestroyImmediate(observerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(terrainData);
        }
    }

    [Test]
    public void CombatVision_DoesNotSeeStealthedEnemy()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            var allyCollider = allyGo.AddComponent<CapsuleCollider>();
            allyCollider.center = new Vector3(0f, 1f, 0f);
            allyCollider.height = 2f;
            var enemyCollider = enemyGo.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1f, 0f);
            enemyCollider.height = 2f;
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(0f, 0f, 5f);
            enemy.StatusEffects.ApplyStealth(5f);
            Physics.SyncTransforms();

            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision vision = ally.Vision;
            vision.Initialize();
            vision.UpdateVision();

            Assert.That(vision.IsVisible(enemy), Is.False);
            Assert.That(vision.HasRecognitionOf(enemy), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatVision_RemembersEnemyAfterLineOfSightLost()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            var allyCollider = allyGo.AddComponent<CapsuleCollider>();
            allyCollider.center = new Vector3(0f, 1f, 0f);
            allyCollider.height = 2f;
            var enemyCollider = enemyGo.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1f, 0f);
            enemyCollider.height = 2f;
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision vision = ally.Vision;
            vision.Initialize();
            vision.UpdateVision();

            Vector3 lastSeen = enemyGo.transform.position;
            Assert.That(vision.IsVisible(enemy), Is.True);

            enemyGo.transform.position = new Vector3(0f, 0f, -5f);
            Physics.SyncTransforms();
            vision.UpdateVision();

            Assert.That(vision.IsVisible(enemy), Is.False);
            Assert.That(vision.HasMemoryOf(enemy), Is.True);
            Assert.That(vision.RememberedEnemies, Does.Contain(enemy));
            Assert.That(vision.TryGetLastKnownPosition(enemy, out Vector3 rememberedPosition), Is.True);
            Assert.That(rememberedPosition, Is.EqualTo(lastSeen));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void CombatVision_ForgetsEnemyAfterSearchTimeout()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject enemyGo = new GameObject("Enemy");

        try
        {
            CombatCharacterSystem system = systemGo.AddComponent<CombatCharacterSystem>();
            Character ally = allyGo.AddComponent<Character>();
            Character enemy = enemyGo.AddComponent<Character>();
            var allyCollider = allyGo.AddComponent<CapsuleCollider>();
            allyCollider.center = new Vector3(0f, 1f, 0f);
            allyCollider.height = 2f;
            var enemyCollider = enemyGo.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1f, 0f);
            enemyCollider.height = 2f;
            allyGo.transform.position = Vector3.zero;
            enemyGo.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            system.AllyCharacters.Add(ally);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision vision = ally.Vision;
            CombatEditModeTestUtil.WireVision(vision, system);
            vision.Initialize();
            vision.UpdateVision();
            Assert.That(vision.HasMemoryOf(enemy), Is.True);

            enemyGo.transform.position = new Vector3(0f, 0f, -5f);
            Physics.SyncTransforms();
            vision.UpdateVision();
            Assert.That(vision.IsVisible(enemy), Is.False);
            Assert.That(vision.HasMemoryOf(enemy), Is.True);

            SetVisionLastSeenTime(vision, enemy, Time.time - 15f);
            vision.UpdateVision();

            Assert.That(vision.HasMemoryOf(enemy), Is.False);
            Assert.That(ContainsEnemy(vision.RememberedEnemies, enemy), Is.False);
            Assert.That(vision.TryGetLastKnownPosition(enemy, out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    private static bool ContainsEnemy(IReadOnlyList<Character> enemies, Character enemy)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == enemy) return true;
        }

        return false;
    }

    private static bool ContainsSharedMemorySnapshot(
        IReadOnlyList<CombatVisionDebugMemorySnapshot> snapshots,
        Character target,
        Character sharedFrom)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            CombatVisionDebugMemorySnapshot snapshot = snapshots[i];
            if (snapshot.Target == target &&
                snapshot.Source == CombatVisionMemorySource.Shared &&
                snapshot.SharedFrom == sharedFrom)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetVisionLastSeenTime(CombatVision vision, Character enemy, float lastSeenAt)
    {
        FieldInfo dictionaryField = typeof(CombatVision).GetField(
            "_memories",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(dictionaryField, Is.Not.Null);

        var dictionary = dictionaryField.GetValue(vision) as Dictionary<Character, CharacterMemory>;
        Assert.That(dictionary, Is.Not.Null);
        Assert.That(dictionary.TryGetValue(enemy, out CharacterMemory memory), Is.True);
        memory.LastSeenTime = lastSeenAt;
    }
}
