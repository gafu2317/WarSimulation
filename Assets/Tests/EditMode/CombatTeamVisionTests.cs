using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatTeamVisionTests
{
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
