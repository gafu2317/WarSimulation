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
    public void CombatVision_ReceiveSharedObservationUpdatesLastKnownPosition()
    {
        GameObject systemGo = new GameObject("CombatCharacterSystem");
        GameObject allyGo = new GameObject("Ally");
        GameObject observerGo = new GameObject("Observer");

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

            GameObject enemyGo = new GameObject("Enemy");
            Character enemy = enemyGo.AddComponent<Character>();
            enemy.SetTeam(CombatTeam.Enemy);
            system.EnemyCharacters.Add(enemy);
            system.AssignTeamsFromLists();

            CombatVision allyVision = ally.Vision;
            allyVision.Initialize();

            Vector3 reportedPosition = new Vector3(3f, 0f, 4f);
            allyVision.ReceiveSharedObservation(enemy, reportedPosition, Time.time);

            Assert.That(allyVision.TryGetLastKnownPosition(enemy, out Vector3 lastKnownPosition), Is.True);
            Assert.That(lastKnownPosition, Is.EqualTo(reportedPosition));
            Assert.That(allyVision.IsVisible(enemy), Is.False);
            Assert.That(allyVision.HasMemoryOf(enemy), Is.True);
            Assert.That(allyVision.RememberedEnemies, Does.Contain(enemy));
        }
        finally
        {
            Object.DestroyImmediate(systemGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(observerGo);
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
            vision.Initialize();
            vision.UpdateVision();
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

    private static bool ContainsEnemy(System.Collections.Generic.IReadOnlyList<Character> enemies, Character enemy)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == enemy) return true;
        }

        return false;
    }

    private static void SetVisionLastSeenTime(CombatVision vision, Character enemy, float lastSeenAt)
    {
        FieldInfo dictionaryField = typeof(CombatVision).GetField(
            "_lastSeenTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(dictionaryField, Is.Not.Null);

        var dictionary = dictionaryField.GetValue(vision) as System.Collections.Generic.Dictionary<Character, float>;
        Assert.That(dictionary, Is.Not.Null);
        dictionary[enemy] = lastSeenAt;
    }
}
